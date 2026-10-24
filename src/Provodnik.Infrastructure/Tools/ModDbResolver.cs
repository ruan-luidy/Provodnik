using Provodnik.Core.Diagnostics;
using Provodnik.Core.Downloads;
using Provodnik.Infrastructure.Processes;

namespace Provodnik.Infrastructure.Tools
{
  /// <summary>
  /// O stalker-gamma-dl-cli.exe que vem no GAMMA (feito em Rust, com TLS de Chrome e o desafio do Cloudflare): recebe
  /// a página "addons/start/123" e devolve na última linha o link direto do mirror. O original aceitava a resposta
  /// errada e baixava " "; aqui sem link é erro.
  /// </summary>
  public sealed class ModDbResolver(Func<string> cliPath, IToolConsole console) : IModDbResolver
  {
    public async Task<string> ResolveAsync(string addonUrl, CancellationToken cancellationToken)
    {
      var cli = cliPath();
      if (!File.Exists(cli))
        throw new DownloadException($"stalker-gamma-dl-cli não encontrado em {cli}");

      console.Write("dl-cli", $"pedindo o link de {addonUrl}");
      ProcessResult result;
      try
      {
        result = await ProcessRunner.RunAsync(
          cli,
          [addonUrl],
          new ProcessOptions(
            WorkingDirectory: Path.GetDirectoryName(cli),
            OnOutput: line => console.Write("dl-cli", line),
            OnError: line => console.Write("dl-cli", line),
            Timeout: TimeSpan.FromMinutes(2)),
          cancellationToken).ConfigureAwait(false);
      }
      catch (TimeoutException ex)
      {
        throw new DownloadException("O ModDB não respondeu a tempo (2 min).", ex);
      }

      var link = result.Output
        .Select(l => l.Trim())
        .LastOrDefault(l => l.StartsWith("http", StringComparison.OrdinalIgnoreCase));
      if (link is null)
        throw new DownloadException($"O ModDB não devolveu o link de download. {result.Tail(2)}".Trim());

      return link;
    }
  }
}
