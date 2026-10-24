using System.Windows;
using System.Windows.Media;
using HandyControl.Themes;

namespace Provodnik.Wpf.Shared.Services
{
  /// <summary>
  /// Claro ou escuro, como no Negativo: o ThemeManager do HandyControl troca as cores dele na hora, e os poucos pincéis
  /// nossos feitos pro escuro (o hover branco transparente, a linha da grade, a barra de rolagem) ganham a versão do
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
