namespace Provodnik.Core.Archives
{
  /// <summary>Extrai zip, 7z e rar (o 7z.exe do GAMMA). Progresso de 0 a 1.</summary>
  public interface IArchiveExtractor
  {
    Task ExtractAsync(string archive, string destination, IProgress<double>? progress, CancellationToken cancellationToken);
  }

  /// <summary>
  /// Arquivo que não abriu: corrompido, cortado no meio do download ou formato desconhecido. Damaged: o defeito é do
  /// arquivo (CRC, cabeçalho, fim antes da hora), então baixar de novo resolve; disco cheio ou pasta presa não.
  /// </summary>
  public sealed class ArchiveException(string message, bool damaged = false) : Exception(message)
  {
    public bool Damaged { get; } = damaged;
  }
}
