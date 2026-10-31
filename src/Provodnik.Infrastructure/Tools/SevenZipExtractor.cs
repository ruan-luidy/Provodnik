using Provodnik.Core.Archives;
using Provodnik.Core.Diagnostics;
using Provodnik.Core.Text;
using Provodnik.Infrastructure.Processes;

namespace Provodnik.Infrastructure.Tools
{
  /// <summary>
  /// O 7z.exe da pasta resources do GAMMA (o mesmo que o original usa: abre zip, 7z e rar). Diferente do original, o
  /// código de saída conta: 2 pra cima é arquivo quebrado e vira erro em vez de seguir com a pasta vazia.
  /// </summary>
  public sealed class SevenZipExtractor(Func<string> sevenZipPath, IToolConsole console) : IArchiveExtractor
  {
    public async Task ExtractAsync(string archive, string destination, IProgress<double>? progress, CancellationToken cancellationToken)
    {
      var exe = sevenZipPath();
      if (!File.Exists(exe))
        throw new ArchiveException(L.T("infra.sevenZip.notFound", exe));

      Directory.CreateDirectory(destination);
      console.Write("7z", L.T("infra.sevenZip.extracting", Path.GetFileName(archive)));
      string[] arguments = ["x", archive, "-aoa", $"-o{destination}", "-y", "-bsp1", "-bso0", "-sccUTF-8"];
      var result = await ProcessRunner.RunAsync(
        exe,
        arguments,
        new ProcessOptions(
          OnOutput: line =>
          {
            if (ProgressLines.TryParseSevenZip(line, out var fraction))
              progress?.Report(fraction);
            else
              console.Write("7z", line);
          },
          OnError: line => console.Write("7z", line)),
        cancellationToken).ConfigureAwait(false);

      // 0 ok, 1 aviso (algum arquivo preso, o resto saiu), 2 erro fatal, 7 linha de comando, 8 memória
      if (result.ExitCode > 1)
        throw new ArchiveException(L.T("infra.sevenZip.failed", result.ExitCode, result.Tail()), IsDamaged(result));
    }

    // as frases do 7-Zip pra arquivo estragado (o texto dele é sempre em inglês; o tipo pode vir no meio, "as [7z] archive");
    // disco cheio também sai com código 2
    private static readonly string[] DamageMarkers =
      ["headers error", "data error", "crc failed", "unexpected end", "can not open the file as", "cannot open the file as", "can't open as archive", "is not archive"];

    internal static bool IsDamaged(ProcessResult result)
    {
      var lines = result.Errors.Concat(result.Output).ToList();
      return lines.Any(l => DamageMarkers.Any(m => l.Contains(m, StringComparison.OrdinalIgnoreCase)))
        && !lines.Any(l => l.Contains("not enough space", StringComparison.OrdinalIgnoreCase));
    }
  }
}
