using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Windows.Threading;
using MahApps.Metro.IconPacks;
using Provodnik.Core.Text;
using Provodnik.Wpf.Shared.Icons;
using Provodnik.Wpf.Shared.Services;

namespace Provodnik.Wpf.Shared.Controls
{
  /// <summary>Tom do diálogo: decide ícone e cor.</summary>
  public enum DialogKind
  {
    Info,
    Success,
    Warning,
    Error,
  }

  /// <summary>O cartão do diálogo. O visual está no XAML; quem abre e fecha é o <see cref="DialogService"/>.</summary>
  public partial class AlertDialog : UserControl
  {
    private readonly string? _details;

    public AlertDialog(DialogKind kind, string title, string message, string primaryText, string? secondaryText = null, string? details = null, bool destructive = false)
    {
      InitializeComponent();

      TitleText.Text = title;
      MessageText.Text = message;
      PrimaryButton.Content = primaryText;
      _details = string.IsNullOrWhiteSpace(details) ? null : details;
      DetailsLink.Visibility = _details is null ? Visibility.Collapsed : Visibility.Visible;

      if (secondaryText is not null)
      {
        SecondaryButton.Content = secondaryText;
        SecondaryButton.Visibility = Visibility.Visible;
      }

      // O quadradinho no tom do aviso: o fundo suave e o desenho na cor cheia
      var (icon, tone) = kind switch
      {
        DialogKind.Success => (PackIconPhosphorIconsKind.CheckCircleBold, "Success"),
        DialogKind.Warning => (PackIconPhosphorIconsKind.WarningBold, "Warning"),
        DialogKind.Error => (PackIconPhosphorIconsKind.WarningOctagonBold, "Danger"),
        _ => (PackIconPhosphorIconsKind.InfoBold, "Accent"),
      };
      HeaderIcon.Data = PhosphorExtension.Get(icon);
      IconTile.SetResourceReference(Border.BackgroundProperty, tone + ".SoftBrush");
      HeaderIcon.SetResourceReference(Shape.FillProperty, tone + ".TextBrush");

      // Confirmar o que não tem volta: o botão principal no vermelho
      if (destructive)
        PrimaryButton.Style = (Style)FindResource("Action.Dialog.Danger");

      Loaded += OnLoaded;
      PreviewKeyDown += OnPreviewKeyDown;
    }

    public event EventHandler? PrimaryClicked;
    public event EventHandler? SecondaryClicked;

    /// <summary>Foco no botão principal depois que o overlay terminou de montar: Enter funciona na hora.</summary>
    private void OnLoaded(object sender, RoutedEventArgs e) =>
      Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () =>
      {
        FocusManager.SetFocusedElement(this, PrimaryButton);
        Keyboard.Focus(PrimaryButton);
      });

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
      if (e.Key == Key.Enter && Keyboard.FocusedElement is not TextBox)
      {
        PrimaryButton_Click(PrimaryButton, e);
        e.Handled = true;
      }
      else if (e.Key == Key.Escape)
      {
        if (SecondaryButton.Visibility == Visibility.Visible)
          SecondaryButton_Click(SecondaryButton, e);
        else
          PrimaryButton_Click(PrimaryButton, e);
        e.Handled = true;
      }
    }

    /// <summary>"ver detalhes": o texto técnico aparece ali mesmo, e o cartão alarga pra caber.</summary>
    private void DetailsLink_Click(object sender, RoutedEventArgs e)
    {
      var open = DetailsCard.Visibility != Visibility.Visible;
      DetailsText.Text = _details;
      DetailsCard.Visibility = CopyButton.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
      DetailsLinkText.Text = L.T(open ? "shared.details.hide" : "shared.details.show");
      Width = open ? 560 : 420;
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
      if (_details is null)
        return;

      Clipboard.SetText(_details);
      ToastService.Success(L.T("shared.details.copied"));
    }

    private void PrimaryButton_Click(object sender, RoutedEventArgs e) => PrimaryClicked?.Invoke(this, EventArgs.Empty);

    private void SecondaryButton_Click(object sender, RoutedEventArgs e) => SecondaryClicked?.Invoke(this, EventArgs.Empty);
  }
}
