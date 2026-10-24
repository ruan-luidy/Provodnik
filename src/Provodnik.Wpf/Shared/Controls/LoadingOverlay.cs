using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using HandyControl.Controls;

namespace Provodnik.Wpf.Shared.Controls
{
  /// <summary>
  /// Carregando no jeito do rnzNegativeObserver: a área escurece (Overlay.DimBrush) e o CircleProgressBar do
  /// HandyControl gira no meio, no lugar das bolinhas do LoadingCircle. Cobre a célula do Grid onde está (e as
  /// que o Grid.RowSpan/ColumnSpan pegar); não segura clique, então a barra de cima continua usável enquanto
  /// carrega. O círculo só anima visível: o LoadingCircle seguia girando escondido e redesenhava a janela à toa.
  /// Uso: <c>&lt;controls:LoadingOverlay Visibility="{Binding IsLoading, Converter=...}" /&gt;</c>.
  /// </summary>
  public sealed class LoadingOverlay : System.Windows.Controls.Border
  {
    public LoadingOverlay()
    {
      IsHitTestVisible = false;
      SetResourceReference(BackgroundProperty, "Overlay.DimBrush");

      var circle = new CircleProgressBar
      {
        Width = 50,
        Height = 50,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Background = Brushes.Transparent,
      };
      circle.SetBinding(CircleProgressBar.IsIndeterminateProperty, new Binding(nameof(IsVisible)) { Source = this, Mode = BindingMode.OneWay });
      Child = circle;
    }
  }
}
