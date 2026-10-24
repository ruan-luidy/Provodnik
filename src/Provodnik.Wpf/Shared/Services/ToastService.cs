using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Provodnik.Core.Text;
using Provodnik.Wpf.Shared.Controls;

namespace Provodnik.Wpf.Shared.Services
{
  /// <summary>
  /// Toasts do Provodnik (o UpdateToastService do rnzNegativeObserver), no lugar do Growl do HandyControl:
  ///   - dentro da janela: empilhados no topo, no centro, numa camada de adorno - por cima de tudo, até de diálogo;
  ///   - na área de trabalho: no topo da tela, no centro (a descida do toast vem de cima, como dentro da janela), numa
  ///     janelinha sempre por cima que não rouba o foco e deixa o clique passar fora do cartão. Vai pra lá
  ///     o que não tem janela boa pra entrar: o app no relógio ou minimizado, a janelinha do Finder (pequena, o toast
  ///     cobria a busca), e o <c>desktop: true</c> (aviso de versão nova) com o app sem foco.
  /// No máximo quatro de cada vez: o mais antigo sai pra dar lugar.
  /// </summary>
  public static class ToastService
  {
    private const int MaxToasts = 4;

    private static readonly TimeSpan Short = TimeSpan.FromSeconds(3);

    private static AdornerStack? _inWindow;
    private static AdornerLayer? _inWindowLayer;
    private static DesktopStack? _desktop;

    public static void Info(string message, string? title = null) => Show(DialogKind.Info, title ?? L.T("shared.toast.info"), message, Short);

    public static void Success(string message, string? title = null) => Show(DialogKind.Success, title ?? L.T("shared.toast.success"), message, Short);

    public static void Warning(string message, string? title = null) => Show(DialogKind.Warning, title ?? L.T("shared.toast.warning"), message, TimeSpan.FromSeconds(4));

    public static void Error(string message, string? title = null) => Show(DialogKind.Error, title ?? L.T("shared.toast.error"), message, TimeSpan.FromSeconds(5));

    /// <param name="lifetime">Quanto fica; null = até fechar no X.</param>
    /// <param name="desktop">Com o app minimizado ou sem foco, mostra no canto da tela em vez de dentro da janela.</param>
    public static void Show(
      DialogKind kind,
      string title,
      string message,
      TimeSpan? lifetime,
      string? actionText = null,
      Action? action = null,
      bool desktop = false)
    {
      var app = Application.Current;
      if (app is null)
        return;

      app.Dispatcher.InvokeAsync(() =>
      {
        var window = Host();
        var fits = window is not null && Fits(window);
        var away = window is null || !window.IsActive;
        IToastStack? stack = !fits || (desktop && away) ? null : InWindow(window!);
        stack ??= _desktop ??= new DesktopStack(() => _desktop = null);

        var toast = new Toast();
        toast.Closed += (_, _) => stack.Remove(toast);
        stack.Add(toast);
        toast.Show(kind, title, message, lifetime, actionText, action);
      });
    }

    // A janela ativa do app; sem nenhuma ativa, a principal se estiver na tela
    private static Window? Host()
    {
      var app = Application.Current;
      var window = app.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? app.MainWindow;
      return window is { IsLoaded: true, IsVisible: true } && window.WindowState != WindowState.Minimized ? window : null;
    }

    // Janela onde o toast (380 de largura, no topo) cabe sem tapar o que ela mostra: a grande e os diálogos dela. A
    // janelinha do Finder não cabe - ele cobria o campo de busca
    private static bool Fits(Window window) => window.ActualWidth >= 700 && window.ActualHeight >= 400;

    /// <summary>
    /// A pilha dentro da janela. Pega a ÚLTIMA camada de adorno da árvore: com um diálogo aberto, é a dele,
    /// e o toast fica por cima do fundo escurecido (mesma escolha do Negativo).
    /// </summary>
    private static AdornerStack? InWindow(Window window)
    {
      var decorators = new List<AdornerDecorator>();
      Collect(window, decorators);
      var decorator = decorators.LastOrDefault(d => d.AdornerLayer is not null && d.Child is not null);
      if (decorator?.AdornerLayer is not { } layer || decorator.Child is not { } host)
        return null;

      if (_inWindow is null || !ReferenceEquals(layer, _inWindowLayer))
      {
        if (_inWindow is not null)
          _inWindowLayer?.Remove(_inWindow);

        _inWindow = new AdornerStack(host);
        _inWindowLayer = layer;
        layer.Add(_inWindow);
      }

      return _inWindow;
    }

    private static void Collect(DependencyObject parent, List<AdornerDecorator> found)
    {
      var count = parent is Visual ? VisualTreeHelper.GetChildrenCount(parent) : 0;
      for (var i = 0; i < count; i++)
      {
        var child = VisualTreeHelper.GetChild(parent, i);
        if (child is AdornerDecorator decorator)
          found.Add(decorator);
        Collect(child, found);
      }
    }

    private interface IToastStack
    {
      void Add(Toast toast);

      void Remove(Toast toast);
    }

    /// <summary>Dentro da janela: centro, logo abaixo da barra do topo; só o cartão recebe clique.</summary>
    private sealed class AdornerStack : Adorner, IToastStack
    {
      private readonly StackPanel _panel = new() { HorizontalAlignment = HorizontalAlignment.Center };

      public AdornerStack(UIElement adorned)
        : base(adorned)
      {
        AddVisualChild(_panel);
        AddLogicalChild(_panel);
      }

      public void Add(Toast toast)
      {
        while (_panel.Children.Count >= MaxToasts)
          _panel.Children.RemoveAt(0);

        _panel.Children.Add(toast);
        InvalidateMeasure();
      }

      public void Remove(Toast toast) => _panel.Children.Remove(toast);

      protected override int VisualChildrenCount => 1;

      protected override Visual GetVisualChild(int index) => _panel;

      protected override IEnumerator LogicalChildren
      {
        get { yield return _panel; }
      }

      protected override Size MeasureOverride(Size constraint)
      {
        _panel.Measure(constraint);
        return constraint;
      }

      protected override Size ArrangeOverride(Size finalSize)
      {
        // No topo, centralizado, logo abaixo da barra de título (o toast do Negativo). O 64 do Терминал pulava a fita
        // de cotações de lá e aqui caía em cima do cabeçalho da grade.
        var width = _panel.DesiredSize.Width;
        // Em pixel inteiro: no meio de um pixel o texto do toast sai borrado
        _panel.Arrange(new Rect(Math.Round((finalSize.Width - width) / 2), 8, width, _panel.DesiredSize.Height));
        return finalSize;
      }

      /// <summary>Fora dos cartões o clique passa pra janela embaixo.</summary>
      protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters)
      {
        foreach (UIElement child in _panel.Children)
        {
          var origin = child.TranslatePoint(new Point(0, 0), this);
          if (new Rect(origin, child.RenderSize).Contains(hitTestParameters.HitPoint))
            return new PointHitTestResult(this, hitTestParameters.HitPoint);
        }

        return null;
      }
    }

    /// <summary>Janelinha no topo da área de trabalho, no centro, sempre por cima, sem foco e sem ícone na barra.</summary>
    private sealed class DesktopStack : IToastStack
    {
      // A largura máxima do toast (480) com a sombra dele dos lados
      private const double Width = 540;

      private readonly StackPanel _panel = new() { VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Center };
      private readonly Window _window;
      private readonly Action _closed;

      public DesktopStack(Action closed)
      {
        _closed = closed;
        var area = SystemParameters.WorkArea;
        _window = new Window
        {
          WindowStyle = WindowStyle.None,
          AllowsTransparency = true,
          Background = Brushes.Transparent,
          ShowInTaskbar = false,
          ShowActivated = false,
          Topmost = true,
          ResizeMode = ResizeMode.NoResize,
          Width = Width,
          Height = 520,
          Left = area.Left + Math.Round((area.Width - Width) / 2),
          Top = area.Top,
          Content = _panel,
        };
        _window.Show();
      }

      public void Add(Toast toast)
      {
        while (_panel.Children.Count >= MaxToasts)
          _panel.Children.RemoveAt(0);

        // Como dentro da janela: 8 do topo, um embaixo do outro
        toast.Margin = new Thickness(0, 8, 0, 0);
        _panel.Children.Add(toast);
      }

      public void Remove(Toast toast)
      {
        _panel.Children.Remove(toast);
        if (_panel.Children.Count == 0)
        {
          _window.Close();
          _closed();
        }
      }
    }
  }
}
