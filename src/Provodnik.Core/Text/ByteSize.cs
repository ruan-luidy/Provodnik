using System.Globalization;

namespace Provodnik.Core.Text
{
  /// <summary>Tamanho em bytes pra tela ("12,4 MB") e o tamanho do jeito que o curl escreve ("55.6M").</summary>
  public static class ByteSize
  {
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    public static string Format(double bytes)
    {
      var unit = 0;
      while (Math.Abs(bytes) >= 1024 && unit < Units.Length - 1)
      {
        bytes /= 1024;
        unit++;
      }
      return unit == 0 ? $"{bytes:0} B" : $"{bytes.ToString(bytes >= 100 ? "0" : "0.0", CultureInfo.CurrentCulture)} {Units[unit]}";
    }

    public static string FormatSpeed(double? bytesPerSecond) => bytesPerSecond is > 0 ? $"{Format(bytesPerSecond.Value)}/s" : string.Empty;

    /// <summary>"123", "512k", "55.6M", "1.2G" (o medidor do curl, potências de 1024).</summary>
    public static bool TryParseCurl(string text, out long bytes)
    {
      bytes = 0;
      if (string.IsNullOrEmpty(text))
        return false;

      var multiplier = char.ToUpperInvariant(text[^1]) switch
      {
        'K' => 1024L,
        'M' => 1024L * 1024,
        'G' => 1024L * 1024 * 1024,
        'T' => 1024L * 1024 * 1024 * 1024,
        'P' => 1024L * 1024 * 1024 * 1024 * 1024,
        _ => 1L,
      };
      var number = multiplier == 1 ? text : text[..^1];
      if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        return false;

      bytes = (long)(value * multiplier);
      return true;
    }
  }
}
