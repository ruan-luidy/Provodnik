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
        throw new ArchiveException($"7-Zip não encontrado em {exe}");

      Directory.CreateDirectory(destination);
      console.Write("7z", $"extraindo {Path.GetFileName(archive)}");
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
        throw new ArchiveException($"7-Zip parou com código {result.ExitCode}: {result.Tail()}");
    }
  }
}
