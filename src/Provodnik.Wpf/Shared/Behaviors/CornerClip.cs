using System.Windows;
using System.Windows.Media;

namespace Provodnik.Wpf.Shared.Behaviors
{
  /// <summary>
  /// Recorta o conteúdo no retângulo arredondado (o CornerClip da Связь, com um raio por canto). O Border do WPF
  /// desenha o canto arredondado mas não corta o filho: o carregando e o erro por cima da tela saíam quadrados,
  /// passando pelo canto de cima à esquerda da camada da tela.
  ///   &lt;Grid behaviors:CornerClip.Radius="12,0,0,0"&gt;
  /// </summary>
  public static class CornerClip
  {
    public static readonly DependencyProperty RadiusProperty = DependencyProperty.RegisterAttached(
      "Radius", typeof(CornerRadius), typeof(CornerClip), new PropertyMetadata(new CornerRadius(0), OnRadiusChanged));

    public static CornerRadius GetRadius(DependencyObject target) => (CornerRadius)target.GetValue(RadiusProperty);

    public static void SetRadius(DependencyObject target, CornerRadius value) => target.SetValue(RadiusProperty, value);

    private static void OnRadiusChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
      if (target is not FrameworkElement element)
        return;

      element.SizeChanged -= OnSizeChanged;
      element.SizeChanged += OnSizeChanged;
      Apply(element);
    }

    private static void OnSizeChanged(object sender, SizeChangedEventArgs e) => Apply((FrameworkElement)sender);

    private static void Apply(FrameworkElement element)
    {
      var radius = GetRadius(element);
      if (element.ActualWidth <= 0 || element.ActualHeight <= 0 || radius == new CornerRadius(0))
      {
        element.Clip = null;
        return;
      }
      element.Clip = Rounded(element.ActualWidth, element.ActualHeight, radius);
    }

    private static Geometry Rounded(double width, double height, CornerRadius r)
    {
      var geometry = new StreamGeometry();
      using (var ctx = geometry.Open())
      {
        ctx.BeginFigure(new Point(r.TopLeft, 0), true, true);
        ctx.LineTo(new Point(width - r.TopRight, 0), false, false);
        if (r.TopRight > 0)
          ctx.ArcTo(new Point(width, r.TopRight), new Size(r.TopRight, r.TopRight), 0, false, SweepDirection.Clockwise, false, false);
        ctx.LineTo(new Point(width, height - r.BottomRight), false, false);
        if (r.BottomRight > 0)
          ctx.ArcTo(new Point(width - r.BottomRight, height), new Size(r.BottomRight, r.BottomRight), 0, false, SweepDirection.Clockwise, false, false);
        ctx.LineTo(new Point(r.BottomLeft, height), false, false);
        if (r.BottomLeft > 0)
          ctx.ArcTo(new Point(0, height - r.BottomLeft), new Size(r.BottomLeft, r.BottomLeft), 0, false, SweepDirection.Clockwise, false, false);
        ctx.LineTo(new Point(0, r.TopLeft), false, false);
        if (r.TopLeft > 0)
          ctx.ArcTo(new Point(r.TopLeft, 0), new Size(r.TopLeft, r.TopLeft), 0, false, SweepDirection.Clockwise, false, false);
      }
      geometry.Freeze();
      return geometry;
    }
  }
}
