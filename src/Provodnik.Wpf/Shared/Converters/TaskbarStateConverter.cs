using System.Globalization;
using System.Windows.Data;
using System.Windows.Shell;

namespace Provodnik.Wpf.Shared.Converters
{
  /// <summary>Rodando → a barra verde no ícone da barra de tarefas; parado → nada.</summary>
  public sealed class TaskbarStateConverter : IValueConverter
  {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
      value is true ? TaskbarItemProgressState.Normal : TaskbarItemProgressState.None;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
      throw new NotSupportedException();
  }
}
