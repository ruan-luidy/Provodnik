namespace Provodnik.Core.Downloads
{
  /// <summary>Quanto já veio de um download. Total nulo quando o servidor não diz o tamanho (zip do GitHub).</summary>
  public readonly record struct TransferProgress(long Received, long? Total, double? BytesPerSecond)
  {
    public double? Fraction => Total is > 0 ? Math.Clamp((double)Received / Total.Value, 0, 1) : null;
  }

  /// <summary>
  /// Um arquivo pra baixar. Impersonate passa pelo curl-impersonate (o CDN do ModDB derruba quem não parece um
  /// navegador); ETag pede "só se mudou" (os zips do GitHub).
  /// </summary>
  public sealed record DownloadRequest(string Url, string Destination, bool Impersonate = false, string? ETag = null);

  /// <summary>NotModified: o servidor respondeu 304 e o arquivo de antes continua valendo.</summary>
  public sealed record DownloadResult(bool NotModified, string? ETag);

  public interface IFileDownloader
  {
    /// <summary>Baixa pra <see cref="DownloadRequest.Destination"/>, continuando de onde parou quando o servidor deixa.</summary>
    Task<DownloadResult> DownloadAsync(DownloadRequest request, IProgress<TransferProgress>? progress, CancellationToken cancellationToken);
  }

  /// <summary>
  /// A página "addons/start/123" do ModDB → o link direto num mirror (cdn.dbolical.com...). É o
  /// stalker-gamma-dl-cli.exe do GAMMA que sabe passar pelo Cloudflare.
  /// </summary>
  public interface IModDbResolver
  {
    Task<string> ResolveAsync(string addonUrl, CancellationToken cancellationToken);
  }

  /// <summary>Falha de download com a mensagem pronta pra tela (o código do curl, o status HTTP).</summary>
  public sealed class DownloadException(string message, Exception? inner = null) : Exception(message, inner);
}
