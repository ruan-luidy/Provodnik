using Provodnik.Core.Archives;
using Provodnik.Core.Downloads;

namespace Provodnik.Features.Bootstrap
{
  /// <summary>Um arquivo numa pasta pública do Mediafire: a chave da página, o tamanho e o SHA-256 que a API dá.</summary>
  public sealed record HostedFile(string Name, long Size, string QuickKey, string? Sha256);

  /// <summary>O que o Windows já tem e o que falta antes de instalar.</summary>
  public sealed record Prerequisites(bool VcRedist, bool DirectX, bool? LongPaths, IReadOnlyList<string> OtherAntivirus);

  /// <summary>Como terminou o PowerShell de administrador. Declined: a pessoa disse não ao UAC.</summary>
  public sealed record ElevatedOutcome(bool Declined, IReadOnlyDictionary<string, string> Results);

  /// <summary>
  /// O que a instalação do zero usa do mundo de fora, ligado pelo Wpf: o download comum (Impersonate passa pelo
  /// curl-impersonate do GAMMA recém-extraído), o 7-Zip escolhido, a API do Mediafire, o link de uma página com
  /// Cloudflare (o navegador embutido), o PowerShell de administrador e o que o Windows já tem.
  /// </summary>
  public interface IBootstrapServices
  {
    IFileDownloader Downloader { get; }

    IArchiveExtractor Extractor(string sevenZipExe);

    Task<IReadOnlyList<HostedFile>> ListMediafireFolderAsync(string folderKey, CancellationToken cancellationToken);

    /// <summary>A página do arquivo (Mediafire, start do ModDB) → o link direto que o navegador recebeu.</summary>
    Task<string> ResolveLinkAsync(string pageUrl, CancellationToken cancellationToken);

    /// <summary>Roda o <see cref="ElevatedSetup.Script"/> do plano como administrador e lê o resultado de cada passo.</summary>
    Task<ElevatedOutcome> RunElevatedAsync(ElevatedPlan plan, CancellationToken cancellationToken);

    Task<Prerequisites> DetectAsync(CancellationToken cancellationToken);

    /// <summary>As pastas onde procurar um arquivo que a pessoa já baixou (a Downloads do Windows).</summary>
    IReadOnlyList<string> ExistingDownloadFolders { get; }
  }
}
