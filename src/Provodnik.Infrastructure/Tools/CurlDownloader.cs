using Provodnik.Core.Diagnostics;
using Provodnik.Core.Downloads;
using Provodnik.Core.Text;
using Provodnik.Infrastructure.Processes;

namespace Provodnik.Infrastructure.Tools
{
  /// <summary>Onde estão o curl-impersonate e a config de navegador que ele imita (o Safari 15.5, como o original).</summary>
  public sealed record CurlPaths(string Exe, string Config, string Header);

  /// <summary>
  /// Baixa do ModDB pelo curl-impersonate do GAMMA: o CDN deles corta quem não tem a "impressão digital" TLS de um
  /// navegador. Diferente do original: continua de onde parou (-C -), desiste de conexão parada (menos de 1 KB/s por
  /// um minuto) e o código de saída vira erro com explicação.
  /// </summary>
  public sealed class CurlDownloader(Func<CurlPaths> paths, IToolConsole console)
  {
    public async Task DownloadAsync(string url, string destination, IProgress<TransferProgress>? progress, CancellationToken cancellationToken)
    {
      var curl = paths();
      if (!File.Exists(curl.Exe))
        throw new DownloadException(L.T("infra.curl.notFound", curl.Exe));

      Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
      var result = await RunAsync(curl, url, destination, resume: true, progress, cancellationToken).ConfigureAwait(false);

      // 33: o servidor não aceita continuar; 22 com arquivo começado: em geral o 416 de "já passou do fim"
      if (result.ExitCode is 33 or 22 && File.Exists(destination) && new FileInfo(destination).Length > 0)
      {
        File.Delete(destination);
        result = await RunAsync(curl, url, destination, resume: false, progress, cancellationToken).ConfigureAwait(false);
      }

      if (result.ExitCode != 0)
        throw new DownloadException($"curl {result.ExitCode}: {Explain(result.ExitCode)} {result.Tail(2)}".Trim());
    }

    private Task<ProcessResult> RunAsync(CurlPaths curl, string url, string destination, bool resume, IProgress<TransferProgress>? progress, CancellationToken cancellationToken)
    {
      var offset = resume && File.Exists(destination) ? new FileInfo(destination).Length : 0;
      console.Write("curl", offset > 0 ? L.T("infra.curl.resuming", Path.GetFileName(destination), offset / 1048576, url) : L.T("infra.curl.downloading", url));
      var arguments = new List<string>
      {
        "--config", curl.Config,
        "--header", "@" + curl.Header,
        "--location",
        "--fail",
        "--connect-timeout", "30",
        "--speed-limit", "1024",
        "--speed-time", "60",
        "--retry", "3",
        "--retry-delay", "3",
        "--retry-connrefused",
        "--output", destination,
      };
      if (resume)
        arguments.AddRange(["--continue-at", "-"]);
      arguments.Add(url);

      return ProcessRunner.RunAsync(
        curl.Exe,
        arguments,
        new ProcessOptions(
          WorkingDirectory: Path.GetDirectoryName(curl.Exe),
          OnError: line =>
          {
            // o medidor conta só a parte que falta: soma o que já estava no disco
            if (ProgressLines.TryParseCurl(line, out var received, out var total, out var speed))
              progress?.Report(new TransferProgress(offset + received, total is { } t ? offset + t : null, speed));
            else if (!line.Contains("% Total") && !line.Contains("Dload"))
              console.Write("curl", line);
          }),
        cancellationToken);
    }

    // o "curl 56:" da frente fica igual em todo idioma: o ErrorExplainer reconhece por ele
    private static string Explain(int code) => code switch
    {
      6 => L.T("infra.curl.code.dns"),
      7 => L.T("infra.curl.code.connect"),
      18 => L.T("infra.curl.code.partial"),
      22 => L.T("infra.curl.code.http"),
      28 => L.T("infra.curl.code.timeout"),
      35 or 60 => L.T("infra.curl.code.tls"),
      52 or 56 => L.T("infra.curl.code.dropped"),
      _ => string.Empty,
    };
  }
}
