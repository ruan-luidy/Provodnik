using System.Diagnostics;
using Provodnik.Core.Downloads;
using Provodnik.Core.Install;
using Provodnik.Core.Modpack;
using Provodnik.Features.Download;
using Provodnik.Features.Verify;

namespace Provodnik.Tests
{
  public sealed class ModDbPacerTests : IDisposable
  {
    private readonly string _downloads = Path.Combine(Path.GetTempPath(), "provodnik-pacer", Guid.NewGuid().ToString("N"));

    public ModDbPacerTests() => Directory.CreateDirectory(_downloads);

    public void Dispose() => Directory.Delete(_downloads, recursive: true);

    [Fact]
    public async Task Pedidos_de_link_vao_um_por_vez_com_respiro()
    {
      var resolver = new ScriptedResolver();
      var pacer = new ModDbPacer(resolver, TimeSpan.FromMilliseconds(150));
      var clock = Stopwatch.StartNew();

      await Task.WhenAll(Enumerable.Range(0, 4).Select(i => pacer.ResolveAsync($"https://www.moddb.com/addons/start/{i}", _ => { }, CancellationToken.None)));

      Assert.Equal(1, resolver.MaxConcurrent);
      Assert.True(clock.Elapsed >= TimeSpan.FromMilliseconds(400), $"levou {clock.Elapsed.TotalMilliseconds} ms");
    }

    [Fact]
    public async Task Bloqueio_esfria_a_fila_e_nao_gasta_tentativa()
    {
      // o ModDB recusa duas vezes e depois entrega; com 1 tentativa só, o mod ainda tem que entrar
      var resolver = new ScriptedResolver { Throttles = 2 };
      var pacer = new ModDbPacer(resolver, TimeSpan.Zero, firstCooldown: TimeSpan.FromMilliseconds(100));
      var fetcher = new ArchiveFetcher(
        new WritingDownloader(),
        pacer,
        HashCache.Load(Path.Combine(_downloads, "hashes.json")),
        _downloads,
        new InstallOptions { Attempts = 1 },
        new SemaphoreSlim(3),
        new SemaphoreSlim(4),
        new SemaphoreSlim(2));
      var waits = new List<string>();

      var outcome = await fetcher.FetchAsync(
        new ArchiveSpec("som.zip", ModKind.ModDb, "https://www.moddb.com/addons/start/1", null),
        null,
        _ => { },
        (status, _, detail) =>
        {
          if (detail is not null)
            waits.Add(detail);
        },
        CancellationToken.None);

      Assert.True(File.Exists(outcome.Path));
      Assert.Equal(3, resolver.Calls);
      Assert.Equal(2, pacer.Cooldowns);
      Assert.Contains(waits, w => w.Contains("calma"));
    }

    private sealed class ScriptedResolver : IModDbResolver
    {
      private int _running;

      public int Throttles { get; set; }

      public int Calls { get; private set; }

      public int MaxConcurrent { get; private set; }

      public async Task<string> ResolveAsync(string addonUrl, CancellationToken cancellationToken)
      {
        var now = Interlocked.Increment(ref _running);
        MaxConcurrent = Math.Max(MaxConcurrent, now);
        try
        {
          Calls++;
          await Task.Delay(10, cancellationToken);
          if (Throttles-- > 0)
            throw new DownloadException("O ModDB não devolveu o link de download. challenge");
          return addonUrl + "?cdn";
        }
        finally
        {
          Interlocked.Decrement(ref _running);
        }
      }
    }

    private sealed class WritingDownloader : IFileDownloader
    {
      public Task<DownloadResult> DownloadAsync(DownloadRequest request, IProgress<TransferProgress>? progress, CancellationToken cancellationToken)
      {
        File.WriteAllText(request.Destination, request.Url);
        return Task.FromResult(new DownloadResult(false, null));
      }
    }
  }
}
