using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Provodnik.Wpf.Shared.Converters
{
  /// <summary>
  /// "Esta é a última coluna visível?", pra célula e cabeçalho dela saírem sem o separador da direita: ele ficava
  /// colado na borda do painel e a linha saía dupla. A grade do DataGrid não deixa tirar a linha de uma coluna só,
  /// então o separador é a borda da célula (ItemGridCell) e do cabeçalho, e este conversor desliga a da ponta.
  /// Valores: a coluna, o DisplayIndex (é ele que avisa quando as colunas são arrastadas) e o DataGrid. Sem coluna
  /// (o cabeçalho que sobra depois da última) conta como ponta.
  /// </summary>
  /// <remarks>
  /// Por MultiBinding e não por propriedade anexada na coluna: caminho com propriedade anexada
  /// ("Column.(controls:X.Y)") num estilo de ResourceDictionary não resolve o prefixo em tempo de execução.
  /// </remarks>
  public sealed class LastColumnConverter : IMultiValueConverter
  {
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
      if (values.Length < 3 || values[0] is not DataGridColumn column)
        return true;

      if (values[2] is not DataGrid grid)
        return false;

      var last = grid.Columns.Where(c => c.Visibility == Visibility.Visible).MaxBy(c => c.DisplayIndex);
      return ReferenceEquals(column, last);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
      throw new NotSupportedException();
  }
}
