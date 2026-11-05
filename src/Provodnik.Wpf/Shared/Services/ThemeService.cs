using System.Windows;
using System.Windows.Media;
using HandyControl.Themes;

namespace Provodnik.Wpf.Shared.Services
{
  /// <summary>
  /// Claro ou escuro, como no Negativo: o ThemeManager do HandyControl troca as cores dele na hora, e os poucos pincéis
  /// nossos feitos pro escuro (as camadas e linhas de branco fraco, o cartão dos menus, a barra de rolagem) ganham a versão do
  /// tema por cima, no Application.Resources. Quem usa eles tem que pegar por DynamicResource.
  /// </summary>
  public static class ThemeService
  {
    private static SettingsStore? _settings;

    public static bool IsDark => ThemeManager.Current.ApplicationTheme != ApplicationTheme.Light;

    public static event EventHandler? Changed;

    /// <summary>Na partida, antes de abrir janela: o tema guardado.</summary>
    public static void Initialize(SettingsStore settings)
    {
      _settings = settings;
      Apply(settings.Current.DarkTheme);
    }

    /// <summary>O menu da conta e o da bandeja: troca e guarda pro app abrir igual.</summary>
    public static void Toggle()
    {
      var dark = !IsDark;
      Apply(dark);
      _settings?.Update(s => s with { DarkTheme = dark });
    }

    private static void Apply(bool dark)
    {
      ThemeManager.Current.ApplicationTheme = dark ? ApplicationTheme.Dark : ApplicationTheme.Light;

      // No escuro clareia (branco), no claro escurece (preto); a barra de rolagem é cinza nos dois, do lado oposto
      var ink = dark ? Colors.White : Colors.Black;
      Set("Hover.RowBrush", ink, dark ? 0.06 : 0.05);
      Set("Grid.LineBrush", ink, dark ? 0.05 : 0.07);
      Set("ScrollBar.ThumbBrush", dark ? Color.FromRgb(0x4A, 0x4A, 0x4A) : Color.FromRgb(0xC2, 0xC2, 0xC2));
      Set("ScrollBar.ThumbHoverBrush", dark ? Color.FromRgb(0x6F, 0x6F, 0x6F) : Color.FromRgb(0xA0, 0xA0, 0xA0));
      Set("ScrollBar.ThumbPressedBrush", dark ? Color.FromRgb(0x9A, 0x9A, 0x9A) : Color.FromRgb(0x80, 0x80, 0x80));

      // As camadas e as linhas (Provodnik.xaml): branco fraco no escuro, preto fraco no claro
      Set("Pane.BackgroundBrush", ink, dark ? 0.07 : 0.03);
      Set("Card.BackgroundBrush", ink, dark ? 0.08 : 0.04);
      Set("Divider.Brush", ink, 0.08);
      Set("Grid.DividerBrush", ink, dark ? 0.05 : 0.06);
      Set("Progress.TrackBrush", ink, dark ? 0.10 : 0.08);
      Set("Button.BackgroundBrush", ink, dark ? 0.09 : 0.06);
      Set("Button.HoverBrush", ink, dark ? 0.14 : 0.10);
      Set("Button.PressedBrush", ink, dark ? 0.055 : 0.04);
      Set("Nav.SelectedBrush", ink, dark ? 0.10 : 0.07);
      Set("CheckBox.BorderBrush", ink, 0.45);
      Set("CheckBox.HoverBorderBrush", ink, 0.70);

      // O cartão dos menus, diálogos e tooltips: o cinza do menu do Windows 11 em cada tema
      Set("Flyout.BackgroundBrush", dark ? Color.FromRgb(0x2B, 0x2B, 0x2E) : Color.FromRgb(0xF9, 0xF9, 0xF9));
      Set("Flyout.BorderBrush", ink, dark ? 0.12 : 0.10);
      Set("Dialog.FooterBrush", Colors.Black, dark ? 0.20 : 0.04);

      // O desenho do quadradinho dos avisos: claro no escuro, mais fechado no claro pra ler no fundo branco
      Set("Success.TextBrush", dark ? Color.FromRgb(0x3D, 0xCB, 0x5E) : Color.FromRgb(0x0F, 0x7B, 0x0F));
      Set("Accent.TextBrush", dark ? Color.FromRgb(0x6F, 0xA3, 0xDC) : Color.FromRgb(0x2A, 0x62, 0xA3));
      Set("Warning.TextBrush", dark ? Color.FromRgb(0xE9, 0xAF, 0x20) : Color.FromRgb(0x9D, 0x5D, 0x00));
      Set("Danger.TextBrush", dark ? Color.FromRgb(0xE5, 0x52, 0x5E) : Color.FromRgb(0xC4, 0x2B, 0x1C));
      Changed?.Invoke(null, EventArgs.Empty);
    }

    private static void Set(string key, Color color, double opacity = 1)
    {
      var brush = new SolidColorBrush(color) { Opacity = opacity };
      brush.Freeze();
      Application.Current.Resources[key] = brush;
    }
  }
}
