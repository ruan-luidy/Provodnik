using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Provodnik.Wpf.Shared.Controls
{
  /// <summary>
  /// A moldura de todo diálogo do app (o DialogFrame do rnzNegativeObserver): cartão com a animação de entrada,
  /// cabeçalho cinza com título, subtítulo e o X de fechar, o corpo, e o rodapé cinza com os botões.
  /// <para>Uso: <c>&lt;controls:DialogFrame Title="..." Subtitle="..." CloseClick="Cancel_Click"&gt;corpo
  /// &lt;controls:DialogFrame.Footer&gt;botões&lt;/controls:DialogFrame.Footer&gt;&lt;/controls:DialogFrame&gt;</c>.
  /// Sem <see cref="Footer"/>, o rodapé some; com <see cref="ShowClose"/> falso, o X some.</para>
  /// </summary>
  [TemplatePart(Name = PartClose, Type = typeof(ButtonBase))]
  public class DialogFrame : ContentControl
  {
    private const string PartClose = "PART_Close";
    private ButtonBase? _close;

    static DialogFrame()
    {
      DefaultStyleKeyProperty.OverrideMetadata(typeof(DialogFrame), new FrameworkPropertyMetadata(typeof(DialogFrame)));
    }

    public static readonly DependencyProperty TitleProperty =
      DependencyProperty.Register(nameof(Title), typeof(object), typeof(DialogFrame), new PropertyMetadata(null));

    /// <summary>Título do cabeçalho: texto ou elemento.</summary>
    public object? Title
    {
      get => GetValue(TitleProperty);
      set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty SubtitleProperty =
      DependencyProperty.Register(nameof(Subtitle), typeof(object), typeof(DialogFrame), new PropertyMetadata(null));

    /// <summary>Linha de baixo do título: texto ou elemento. Vazio = some.</summary>
    public object? Subtitle
    {
      get => GetValue(SubtitleProperty);
      set => SetValue(SubtitleProperty, value);
    }

    public static readonly DependencyProperty FooterProperty =
      DependencyProperty.Register(nameof(Footer), typeof(object), typeof(DialogFrame), new PropertyMetadata(null));

    /// <summary>Conteúdo do rodapé cinza (os botões). Nulo = sem rodapé.</summary>
    public object? Footer
    {
      get => GetValue(FooterProperty);
      set => SetValue(FooterProperty, value);
    }

    public static readonly DependencyProperty ShowCloseProperty =
      DependencyProperty.Register(nameof(ShowClose), typeof(bool), typeof(DialogFrame), new PropertyMetadata(true));

    /// <summary>Mostra o X no cabeçalho. Ligado por padrão.</summary>
    public bool ShowClose
    {
      get => (bool)GetValue(ShowCloseProperty);
      set => SetValue(ShowCloseProperty, value);
    }

    public static readonly DependencyProperty BodyPaddingProperty =
      DependencyProperty.Register(nameof(BodyPadding), typeof(Thickness), typeof(DialogFrame), new PropertyMetadata(new Thickness(20, 16, 20, 16)));

    /// <summary>Respiro do corpo. Lista ou grade que encosta nas bordas usa 0.</summary>
    public Thickness BodyPadding
    {
      get => (Thickness)GetValue(BodyPaddingProperty);
      set => SetValue(BodyPaddingProperty, value);
    }

    public static readonly DependencyProperty CloseCommandProperty =
      DependencyProperty.Register(nameof(CloseCommand), typeof(ICommand), typeof(DialogFrame), new PropertyMetadata(null));

    /// <summary>Comando do X, para diálogo que fecha pela ViewModel.</summary>
    public ICommand? CloseCommand
    {
      get => (ICommand?)GetValue(CloseCommandProperty);
      set => SetValue(CloseCommandProperty, value);
    }

    public static readonly RoutedEvent CloseClickEvent =
      EventManager.RegisterRoutedEvent(nameof(CloseClick), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(DialogFrame));

    /// <summary>Clique no X. O diálogo liga no mesmo handler do Cancelar.</summary>
    public event RoutedEventHandler CloseClick
    {
      add => AddHandler(CloseClickEvent, value);
      remove => RemoveHandler(CloseClickEvent, value);
    }

    public override void OnApplyTemplate()
    {
      if (_close is not null)
        _close.Click -= OnCloseClick;

      base.OnApplyTemplate();
      _close = GetTemplateChild(PartClose) as ButtonBase;
      if (_close is not null)
        _close.Click += OnCloseClick;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
      e.Handled = true;
      RaiseEvent(new RoutedEventArgs(CloseClickEvent, this));
      if (CloseCommand?.CanExecute(null) == true)
        CloseCommand.Execute(null);
    }
  }
}
