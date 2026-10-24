using System.Text.RegularExpressions;

namespace Provodnik.Core.Text
{
  /// <summary>
  /// Lê o progresso que as ferramentas escrevem no console. Cada linha chega separada por \r ou \n (o medidor do curl e
  /// o do 7-Zip reescrevem a mesma linha com \r).
  /// </summary>
  public static partial class ProgressLines
  {
    /// <summary>
    /// Uma linha do medidor do curl:
    ///   " 45  123M   45 55.6M    0     0  10.2M      0  0:00:12  0:00:05  0:00:07 10.5M"
    /// % total · total · % recebido · recebido · ... · média · ... · velocidade atual.
    /// </summary>
    public static bool TryParseCurl(string line, out long received, out long? total, out double? speed)
    {
      received = 0;
      total = null;
      speed = null;
      var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
      if (parts.Length < 12 || !int.TryParse(parts[0], out var percent) || percent is < 0 or > 100)
        return false;
      if (!ByteSize.TryParseCurl(parts[3], out received))
        return false;

      if (ByteSize.TryParseCurl(parts[1], out var size) && size > 0)
        total = size;
      if (ByteSize.TryParseCurl(parts[11], out var current) || ByteSize.TryParseCurl(parts[6], out current))
        speed = current;
      return true;
    }

    /// <summary>" 45% 12 - pasta\arquivo.ltx" (7z com -bsp1) → 0,45.</summary>
    public static bool TryParseSevenZip(string line, out double fraction)
    {
      var match = SevenZipPercent().Match(line);
      fraction = match.Success ? int.Parse(match.Groups[1].Value) / 100.0 : 0;
      return match.Success;
    }

    /// <summary>"Receiving objects:  45% (1234/2742), 1.20 MiB | 2.40 MiB/s" → ("Receiving objects", 0,45).</summary>
    public static bool TryParseGit(string line, out string phase, out double fraction)
    {
      var match = GitPercent().Match(line);
      phase = match.Success ? match.Groups[1].Value.Trim() : string.Empty;
      fraction = match.Success ? int.Parse(match.Groups[2].Value) / 100.0 : 0;
      return match.Success;
    }

    [GeneratedRegex(@"^\s*(\d{1,3})%")]
    private static partial Regex SevenZipPercent();

    [GeneratedRegex(@"^(?:remote:\s*)?([A-Za-z][A-Za-z ]+):\s+(\d{1,3})%")]
    private static partial Regex GitPercent();
  }
}
