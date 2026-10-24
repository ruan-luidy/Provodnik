using System.Windows;
using System.Windows.Controls;

namespace Provodnik.Wpf.Shared.Controls
{
  /// <summary>
  /// Número pequeno com "−" e "+" (quantos downloads ao mesmo tempo): no lugar do NumericUpDown do HandyControl, que
  /// vinha com caixa sólida, borda e as setinhas dele. Sem roda do mouse: dentro de lista que rola, ela mudava o
  /// número em vez de rolar.
  /// </summary>
  public partial class Stepper : UserControl
  {
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
      nameof(Value),
      typeof(int),
      typeof(Stepper),
      new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnLimitsChanged));

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
      nameof(Minimum), typeof(int), typeof(Stepper), new PropertyMetadata(1, OnLimitsChanged));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
      nameof(Maximum), typeof(int), typeof(Stepper), new PropertyMetadata(8, OnLimitsChanged));

    public Stepper()
    {
      InitializeComponent();
      Refresh();
    }

    public int Value
    {
      get => (int)GetValue(ValueProperty);
      set => SetValue(ValueProperty, value);
    }

    public int Minimum
    {
      get => (int)GetValue(MinimumProperty);
      set => SetValue(MinimumProperty, value);
    }

    public int Maximum
    {
      get => (int)GetValue(MaximumProperty);
      set => SetValue(MaximumProperty, value);
    }

    private static void OnLimitsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((Stepper)d).Refresh();

    // na ponta, o botão daquele lado apaga
    private void Refresh()
    {
      Down.IsEnabled = Value > Minimum;
      Up.IsEnabled = Value < Maximum;
    }

    private void Step(int delta) => Value = Math.Clamp(Value + delta, Minimum, Maximum);

    private void Down_Click(object sender, RoutedEventArgs e) => Step(-1);

    private void Up_Click(object sender, RoutedEventArgs e) => Step(+1);
  }
}
