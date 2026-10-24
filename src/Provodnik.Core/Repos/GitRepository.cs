namespace Provodnik.Core.Repos
{
  /// <summary>Um repositório que o launcher mantém em resources\ e atualiza a cada instalação.</summary>
  public sealed record GitRepository(string Name, string Url, string Branch = "main", bool Required = false)
  {
    /// <summary>A definição do modpack (G.A.M.M.A\, a versão): sem ela não tem o que instalar.</summary>
    public static readonly GitRepository Definition = new("Stalker_GAMMA", "https://github.com/Grokitach/Stalker_GAMMA", Required: true);

    /// <summary>Os arquivos grandes que não cabem no ModDB (vários GB na primeira vez).</summary>
    public static readonly GitRepository LargeFiles = new("gamma_large_files_v2", "https://github.com/Grokitach/gamma_large_files_v2");

    /// <summary>As armas do Gunslinger por Teivaz, que vão pra pasta do mod 312.</summary>
    public static readonly GitRepository Gunslinger = new("teivaz_anomaly_gunslinger", "https://github.com/Grokitach/teivaz_anomaly_gunslinger");

    public static IReadOnlyList<GitRepository> All { get; } = [Definition, LargeFiles, Gunslinger];
  }

  /// <summary>Clona ou deixa igual ao remoto (fetch + reset --hard + clean), como o original fazia pelo git-cmd.</summary>
  public interface IGitClient
  {
    Task SyncAsync(GitRepository repository, string directory, IProgress<string>? progress, CancellationToken cancellationToken);
  }

  public sealed class GitException(string message) : Exception(message);
}
