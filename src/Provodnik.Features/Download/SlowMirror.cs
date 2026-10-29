using Provodnik.Core.Downloads;

namespace Provodnik.Features.Download
{
  /// <summary>
  /// O ModDB sorteia o espelho a cada link pedido, e daqui uns dão 3 MB/s e outros 100 KB/s (um arquivo de 500 MB
  /// passava de uma hora). Depois de <see cref="Grace"/> baixando, olha uma vez a média: abaixo de
  /// <see cref="MinBytesPerSecond"/> com muito ainda por vir, vale largar e pedir outro link (o .part continua).
  /// </summary>
  public sealed class SlowMirror(Func<long>? clock = null)
  {
    public static readonly TimeSpan Grace = TimeSpan.FromSeconds(30);

    public const double MinBytesPerSecond = 300 * 1024;

    /// <summary>Com menos que isso pra vir, trocar demora mais que terminar.</summary>
    public const long MinRemaining = 32L * 1024 * 1024;

    private readonly Func<long> _clock = clock ?? (() => Environment.TickCount64);
    private long? _start;
    private long _startBytes;
    private bool _decided;

    /// <summary>Verdadeiro uma vez só, quando o espelho se mostrou lento.</summary>
    public bool IsSlow(TransferProgress progress)
    {
      var now = _clock();
      if (_start is not { } start)
      {
        // a medida começa no primeiro byte, sem contar o que o .part já tinha
        _start = now;
        _startBytes = progress.Received;
        return false;
      }
      if (_decided || now - start < Grace.TotalMilliseconds)
        return false;

      _decided = true;
      var speed = (progress.Received - _startBytes) / ((now - start) / 1000d);
      return progress.Total is { } total && total - progress.Received > MinRemaining && speed < MinBytesPerSecond;
    }
  }
}
