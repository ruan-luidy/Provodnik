using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Provodnik.Wpf.Shared.Converters
{
  /// <summary>
  /// Quantidade (o Count de uma lista) em visibilidade: aparece com algum item. Com <see cref="Inverted"/>, aparece
  /// só com a lista vazia - é o do estado vazio por cima da tabela.
  ///   Visibility="{Binding Results.Count, Converter={StaticResource WhenAny}}"
  ///   Visibility="{Binding Results.Count, Converter={StaticResource WhenEmpty}}"
  /// </summary>
  public sealed class CountToVisibilityConverter : IValueConverter
  {
    public bool Inverted { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
      var any = value is int count && count > 0;
      return any != Inverted ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
      throw new NotSupportedException();
  }
}
