using Provodnik.Core.Downloads;
using Provodnik.Infrastructure.Tools;

namespace Provodnik.Infrastructure.Http
{
  /// <summary>O ModDB vai pelo curl-impersonate; o resto pelo HttpClient.</summary>
  public sealed class FileDownloader(CurlDownloader curl, HttpDownloader http) : IFileDownloader
  {
    public async Task<DownloadResult> DownloadAsync(DownloadRequest request, IProgress<TransferProgress>? progress, CancellationToken cancellationToken)
    {
      if (!request.Impersonate)
        return await http.DownloadAsync(request, progress, cancellationToken).ConfigureAwait(false);

      await curl.DownloadAsync(request.Url, request.Destination, progress, cancellationToken).ConfigureAwait(false);
      return new DownloadResult(NotModified: false, ETag: null);
    }
  }
}
