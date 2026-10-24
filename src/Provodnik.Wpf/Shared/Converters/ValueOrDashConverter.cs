using System.Globalization;
using System.Windows.Data;

namespace Provodnik.Wpf.Shared.Converters
{
  /// <summary>
  /// O número de um cartão, ou "—" enquanto não tem resultado: os cartões ficam na tela desde o começo (a tela não
  /// pula quando os números chegam) sem mostrar um 0 que parece resposta.
  ///   &lt;MultiBinding Converter="{StaticResource ValueOrDash}"&gt;&lt;Binding Path="Outdated" /&gt;&lt;Binding Path="HasResults" /&gt;&lt;/MultiBinding&gt;
  /// </summary>
  public sealed class ValueOrDashConverter : IMultiValueConverter
  {
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture) =>
      values.Length == 2 && values[1] is true ? System.Convert.ToString(values[0], culture) ?? "—" : "—";

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
      throw new NotSupportedException();
  }
}
