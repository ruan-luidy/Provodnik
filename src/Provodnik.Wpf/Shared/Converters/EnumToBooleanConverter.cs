using System.Globalization;
using System.Windows.Data;

namespace Provodnik.Wpf.Shared.Converters
{
  /// <summary>
  /// Liga um grupo de RadioButtons numa propriedade enum: cada botão diz o seu valor no ConverterParameter.
  ///   IsChecked="{Binding Timeframe, Converter={StaticResource EnumToBoolean}, ConverterParameter={x:Static models:Timeframe.OneDay}}"
  /// Marcar o botão grava o valor dele; desmarcar não faz nada (quem desmarca é o vizinho sendo marcado).
  /// O HandyControl não tem esse conversor.
  /// </summary>
  public sealed class EnumToBooleanConverter : IValueConverter
  {
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
      value is not null && value.Equals(parameter);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
      value is true ? parameter : Binding.DoNothing;
  }
}
