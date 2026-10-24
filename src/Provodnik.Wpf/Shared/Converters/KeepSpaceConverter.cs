using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Provodnik.Wpf.Shared.Converters
{
  /// <summary>
  /// Aparece ou fica invisível SEM sair do lugar (Hidden, não Collapsed): a regra das telas é que nada muda de altura
  /// quando uma faixa, um texto de progresso ou um botão aparece ou some - o resto da tela dava um pulinho. Vale com
  /// bool (true aparece), texto (não vazio), número (maior que zero) ou objeto (não nulo).
  ///   Visibility="{Binding HasStatus, Converter={StaticResource KeepSpace}}"
  /// </summary>
  public sealed class KeepSpaceConverter : IValueConverter
  {
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
      var shown = value switch
      {
        bool flag => flag,
        string text => text.Length > 0,
        int count => count > 0,
        null => false,
        _ => true,
      };
      return shown ? Visibility.Visible : Visibility.Hidden;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
      throw new NotSupportedException();
  }
}
