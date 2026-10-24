using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using MahApps.Metro.IconPacks;
using Provodnik.Wpf.Shared.Icons;

namespace Provodnik.Wpf.Shared.Controls
{
  /// <summary>
  /// Um aviso passageiro (o toast do rnzNegativeObserver): círculo colorido pelo tom, título, mensagem, X e, se
  /// tiver, um botão de ação. Some sozinho depois de uns segundos - o relógio para com o mouse em cima. Quem
  /// cria, empilha e remove é o <see cref="Services.ToastService"/>.
  /// </summary>
  public partial class Toast : UserControl
  {
    private DispatcherTimer? _timer;
    private Action? _action;
    private bool _closing;

    public Toast()
    {
      InitializeComponent();
      MouseEnter += (_, _) => _timer?.Stop();
      MouseLeave += (_, _) => _timer?.Start();
    }

    /// <summary>Terminou de sair (o host tira da tela).</summary>
    public event EventHandler? Closed;

    /// <param name="seconds">Quanto fica na tela; null = até o usuário fechar.</param>
    public void Show(DialogKind kind, string title, string message, TimeSpan? seconds, string? actionText = null, Action? action = null)
    {
      // Os ícones e as cores do Negativo
      var (icon, brush) = kind switch
      {
        DialogKind.Success => (PackIconPhosphorIconsKind.CheckCircleFill, "SuccessBrush"),
        DialogKind.Warning => (PackIconPhosphorIconsKind.WarningFill, "WarningBrush"),
        DialogKind.Error => (PackIconPhosphorIconsKind.WarningCircleFill, "DangerBrush"),
        _ => (PackIconPhosphorIconsKind.InfoFill, "PrimaryBrush"),
      };
      IconPath.Data = PhosphorExtension.Get(icon);
      IconDisc.Background = TryFindResource(brush) as Brush ?? Brushes.SteelBlue;
      TitleText.Text = title;
      MessageText.Text = message;
      MessageText.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;

      // Erro sem ação própria ganha o "OK" embaixo, como no Negativo (o botão só fecha)
      if (actionText is null && kind == DialogKind.Error)
        actionText = "OK";
      _action = action;
      ActionsBar.Visibility = actionText is null ? Visibility.Collapsed : Visibility.Visible;
      ActionText.Text = actionText;

      ((Storyboard)Resources["SlideIn"]).Begin(this);

      if (seconds is { } lifetime)
      {
        _timer = new DispatcherTimer { Interval = lifetime };
        _timer.Tick += (_, _) => Hide();
        if (!IsMouseOver)
          _timer.Start();
      }
    }

    public void Hide()
    {
      if (_closing)
        return;

      _closing = true;
      _timer?.Stop();
      var slideOut = ((Storyboard)Resources["SlideOut"]).Clone();
      slideOut.Completed += (_, _) => Closed?.Invoke(this, EventArgs.Empty);
      slideOut.Begin(this);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Hide();

    private void Action_Click(object sender, RoutedEventArgs e)
    {
      _action?.Invoke();
      Hide();
    }
  }
}
