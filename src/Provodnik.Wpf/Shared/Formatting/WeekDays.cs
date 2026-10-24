using System.Globalization;

namespace Provodnik.Wpf.Shared.Formatting
{
  /// <summary>
  /// As iniciais dos dias da semana na ordem do calendário do idioma (domingo primeiro em pt-BR e en-US).
  /// O cabeçalho do calendário é desenhado à mão: o do WPF procura o template dele por uma chave interna que não
  /// chega no nosso dicionário, e os dias saíam em branco.
  /// </summary>
  public static class WeekDays
  {
    public static IReadOnlyList<string> Short
    {
      get
      {
        var format = CultureInfo.CurrentCulture.DateTimeFormat;
        var first = (int)format.FirstDayOfWeek;
        return Enumerable.Range(0, 7)
          .Select(i => format.ShortestDayNames[(first + i) % 7].ToUpper(CultureInfo.CurrentCulture))
          .ToList();
      }
    }
  }
}
