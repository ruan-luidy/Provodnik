namespace Provodnik.Core.Remote
{
  /// <summary>
  /// O que o launcher consulta na internet antes de instalar:
  ///   stalker-gamma.com/api/client/v1/mods/list?key= (o mods.txt)
  ///   raw.githubusercontent.com/Grokitach/Stalker_GAMMA/main/G.A.M.M.A_definition_version.txt (a versão)
  /// </summary>
  public interface IGammaApi
  {
    Task<string> GetModListAsync(string key, CancellationToken cancellationToken);

    Task<string> GetDefinitionVersionAsync(CancellationToken cancellationToken);
  }
}
