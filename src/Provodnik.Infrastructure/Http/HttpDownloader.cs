using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using Provodnik.Core.Downloads;

namespace Provodnik.Infrastructure.Http
{
  /// <summary>
  /// Download comum pelo HttpClient (GitHub, gofile): continua o .part quando o servidor aceita Range e pergunta "só se
  /// mudou" com o ETag. O original usava o DownloadFile do VisualBasic, sem nada disso.
  /// </summary>
  public sealed class HttpDownloader(HttpClient http)
  {
    public async Task<DownloadResult> DownloadAsync(DownloadRequest request, IProgress<TransferProgress>? progress, CancellationToken cancellationToken)
    {
      Directory.CreateDirectory(Path.GetDirectoryName(request.Destination)!);
      var offset = File.Exists(request.Destination) ? new FileInfo(request.Destination).Length : 0;

      using var message = new HttpRequestMessage(HttpMethod.Get, request.Url);
      if (request.ETag is { Length: > 0 } etag && EntityTagHeaderValue.TryParse(etag, out var tag))
        message.Headers.IfNoneMatch.Add(tag);
      if (offset > 0)
        message.Headers.Range = new RangeHeaderValue(offset, null);

      using var response = await http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
      if (response.StatusCode == HttpStatusCode.NotModified)
        return new DownloadResult(NotModified: true, request.ETag);
      if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
      {
        // o .part já estava inteiro (ou maior que o arquivo novo): começa do zero na próxima tentativa
        File.Delete(request.Destination);
        throw new DownloadException("O arquivo parcial não bate com o do servidor; vai baixar de novo.");
      }
      if (!response.IsSuccessStatusCode)
        throw new DownloadException($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");

      var resumed = response.StatusCode == HttpStatusCode.PartialContent;
      if (!resumed)
        offset = 0;
      var total = response.Content.Headers.ContentLength is { } length ? offset + length : (long?)null;

      await using (var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
      await using (var file = new FileStream(request.Destination, resumed ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20, useAsync: true))
      {
        var buffer = new byte[1 << 16];
        var received = offset;
        var clock = Stopwatch.StartNew();
        var lastReport = TimeSpan.Zero;
        var windowStart = TimeSpan.Zero;
        var windowBytes = 0L;
        double? speed = null;
        int read;
        while ((read = await body.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
          await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
          received += read;
          windowBytes += read;

          var now = clock.Elapsed;
          if (now - windowStart >= TimeSpan.FromSeconds(1))
          {
            speed = windowBytes / (now - windowStart).TotalSeconds;
            windowStart = now;
            windowBytes = 0;
          }
          if (now - lastReport >= TimeSpan.FromMilliseconds(250))
          {
            lastReport = now;
            progress?.Report(new TransferProgress(received, total, speed));
          }
        }
        progress?.Report(new TransferProgress(received, total ?? received, speed));

        if (total is { } expected && received != expected)
          throw new DownloadException($"O download veio cortado ({received} de {expected} bytes).");
      }

      return new DownloadResult(NotModified: false, response.Headers.ETag?.ToString());
    }
  }
}
