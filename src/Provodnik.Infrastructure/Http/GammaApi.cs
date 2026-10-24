using Provodnik.Core.Remote;

namespace Provodnik.Infrastructure.Http
{
  /// <summary>
  /// As duas consultas do launcher: a lista de mods (stalker-gamma.com) e a versão da definição (o GitHub do Grok).
  /// Resposta que não parece a lista (página de erro, HTML do Cloudflare) vira erro em vez de virar um mods.txt
  /// estragado.
  /// </summary>
  public sealed class GammaApi(HttpClient http) : IGammaApi
  {
    private const string ModListUrl = "https://stalker-gamma.com/api/client/v1/mods/list?key=";
    private const string VersionUrl = "https://raw.githubusercontent.com/Grokitach/Stalker_GAMMA/main/G.A.M.M.A_definition_version.txt";

    public async Task<string> GetModListAsync(string key, CancellationToken cancellationToken)
    {
      var text = await GetWithRetryAsync(ModListUrl + Uri.EscapeDataString(key.Replace(" ", string.Empty)), cancellationToken).ConfigureAwait(false);
      if (text.TrimStart().StartsWith('<') || !text.Contains('\t'))
        throw new HttpRequestException("A lista de mods veio num formato estranho (o site pode estar fora do ar).");
      return text;
    }

    public async Task<string> GetDefinitionVersionAsync(CancellationToken cancellationToken) =>
      (await GetWithRetryAsync(VersionUrl, cancellationToken).ConfigureAwait(false)).Trim();

    private async Task<string> GetWithRetryAsync(string url, CancellationToken cancellationToken)
    {
      for (var attempt = 1; ; attempt++)
      {
        try
        {
          return await http.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (attempt < 3 && ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
          await Task.Delay(TimeSpan.FromSeconds(2 * attempt), cancellationToken).ConfigureAwait(false);
        }
      }
    }
  }
}
