namespace Provodnik.Core.Archives
{
  /// <summary>Extrai zip, 7z e rar (o 7z.exe do GAMMA). Progresso de 0 a 1.</summary>
  public interface IArchiveExtractor
  {
    Task ExtractAsync(string archive, string destination, IProgress<double>? progress, CancellationToken cancellationToken);
  }

  /// <summary>Arquivo que não abriu: corrompido, cortado no meio do download ou formato desconhecido.</summary>
  public sealed class ArchiveException(string message) : Exception(message);
}
