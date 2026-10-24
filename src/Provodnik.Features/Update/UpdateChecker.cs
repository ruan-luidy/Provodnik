using Provodnik.Core.Layout;
using Provodnik.Core.Remote;
using Provodnik.Core.Text;

namespace Provodnik.Features.Update
{
  /// <summary>
  /// O "G.A.M.M.A. addons status" e o "ModDB addons status" do topo do original: a versão da definição contra a do
  /// GitHub, e o mods.txt local contra o do site. Nulo quando não deu pra saber.
  /// </summary>
  public sealed record UpdateStatus(string? InstalledVersion, string? OnlineVersion, bool? ModListChanged, string? Error)
  {
    public bool DefinitionOutdated => OnlineVersion is not null && !string.Equals(InstalledVersion, OnlineVersion, StringComparison.Ordinal);

    public bool HasUpdate => DefinitionOutdated || ModListChanged == true;
  }

  public sealed class UpdateChecker(IGammaApi api)
  {
    public async Task<UpdateStatus> CheckAsync(GammaLayout layout, string key, CancellationToken cancellationToken)
    {
      var installed = File.Exists(layout.VersionFile) ? (await File.ReadAllTextAsync(layout.VersionFile, cancellationToken).ConfigureAwait(false)).Trim() : null;
      string? online = null;
      bool? changed = null;
      var errors = new List<string>();

      try
      {
        online = await api.GetDefinitionVersionAsync(cancellationToken).ConfigureAwait(false);
      }
      catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
      {
        errors.Add(L.T("features.update.versionError", ex.Message));
      }

      try
      {
        var remote = await api.GetModListAsync(key, cancellationToken).ConfigureAwait(false);
        var local = File.Exists(layout.ModListFile) ? await File.ReadAllTextAsync(layout.ModListFile, cancellationToken).ConfigureAwait(false) : null;
        changed = local is null || Normalize(local) != Normalize(remote);
      }
      catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
      {
        errors.Add(L.T("features.update.modListError", ex.Message));
      }

      return new UpdateStatus(installed, online, changed, errors.Count > 0 ? string.Join(" · ", errors) : null);
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n").TrimEnd('\n');
  }
}
