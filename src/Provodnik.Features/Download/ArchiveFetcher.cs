using Provodnik.Core.Downloads;
using Provodnik.Core.Install;
using Provodnik.Core.IO;
using Provodnik.Core.Modpack;
using Provodnik.Features.Verify;

namespace Provodnik.Features.Download
{
  /// <summary>Um arquivo de downloads\ e de onde ele vem. Vários mods podem apontar pro mesmo (o zip do anomaly-compilation serve 4).</summary>
  public sealed record ArchiveSpec(string Name, ModKind Kind, string Url, string? Md5);

  /// <summary>O arquivo pronto em downloads\, com o md5 de verdade dele e um aviso quando aceitou algo fora do esperado.</summary>
  public sealed record ArchiveOutcome(string Path, string Md5, string? Warning);

  /// <summary>Atualização de um arquivo: o estado, quanto já veio e o recado.</summary>
  public delegate void ArchiveReport(ModStatus status, TransferProgress? transfer = null, string? detail = null);

  /// <summary>
  /// Garante um arquivo em downloads\, no fluxo do original (existe? md5 bate? GitHub mudou?) mas com o que faltava:
  ///   - o link do ModDB vem da fila cadenciada (<see cref="ModDbPacer"/>), de novo a cada tentativa; o download no CDN
  ///     roda em paralelo, sem segurar a vez de quem só quer o link;
  ///   - baixa num .part e continua de onde parou; o arquivo final só aparece inteiro e conferido;
  ///   - até N tentativas com espera crescente (5 s, 11 s, 24 s, 53 s) em vez de uma só;
  ///   - md5 errado duas vezes com o MESMO resultado não é download quebrado: o autor atualizou o arquivo no ModDB
  ///     antes da lista do GAMMA. Entra com aviso, que é o que o original fazia sem avisar.
  /// </summary>
  public sealed class ArchiveFetcher(
    IFileDownloader downloader,
    ModDbPacer pacer,
    HashCache hashes,
    string downloads,
    InstallOptions options,
    SemaphoreSlim modDbGate,
    SemaphoreSlim httpGate,
    SemaphoreSlim hashGate)
  {
    public async Task<ArchiveOutcome> FetchAsync(ArchiveSpec spec, string? etag, Action<string> rememberETag, ArchiveReport report, CancellationToken cancellationToken)
    {
      var path = Path.Combine(downloads, spec.Name);
      var exists = File.Exists(path);
      var conditional = false;

      if (exists)
      {
        if (spec.Kind == ModKind.GitHub && options.UpdateGitHubAddons)
        {
          // o "Force git download" do original, sem baixar à toa: com ETag o GitHub responde 304 quando nada mudou
          conditional = etag is not null;
        }
        else if (spec.Md5 is not null && options.CheckMd5)
        {
          var local = await HashAsync(path, report, cancellationToken).ConfigureAwait(false);
          if (local == spec.Md5)
            return new ArchiveOutcome(path, local, null);
          report(ModStatus.Downloading, detail: "md5 do arquivo baixado não bate, baixando de novo");
        }
        else
        {
          return new ArchiveOutcome(path, await HashAsync(path, report, cancellationToken).ConfigureAwait(false), null);
        }
      }

      try
      {
        return await DownloadAsync(spec, path, conditional ? etag : null, rememberETag, report, cancellationToken).ConfigureAwait(false);
      }
      catch (DownloadException ex) when (exists && File.Exists(path))
      {
        // não deu pra atualizar, mas tem o de antes: segue com ele
        var md5 = await HashAsync(path, report, cancellationToken).ConfigureAwait(false);
        return new ArchiveOutcome(path, md5, $"não deu pra atualizar ({ex.Message}); usando o arquivo que já estava em downloads");
      }
    }

    private async Task<ArchiveOutcome> DownloadAsync(ArchiveSpec spec, string path, string? etag, Action<string> rememberETag, ArchiveReport report, CancellationToken cancellationToken)
    {
      var part = path + ".part";
      var gate = spec.Kind == ModKind.ModDb ? modDbGate : httpGate;
      string? lastBadMd5 = null;
      Exception? last = null;

      var throttles = 0;
      for (var attempt = 1; attempt <= options.Attempts; attempt++)
      {
        // o pacer já esfria sozinho quando o bloqueio vem do pedido de link; do download, avisa ele
        var resolving = true;
        try
        {
          var url = spec.Url;
          if (spec.Kind == ModKind.ModDb)
          {
            // o link vem da fila cadenciada do ModDB; o download depois corre solto no CDN
            report(ModStatus.Pending, detail: "na fila do ModDB");
            url = await pacer.ResolveAsync(spec.Url, message => report(ModStatus.Resolving, detail: message), cancellationToken).ConfigureAwait(false);
          }
          resolving = false;

          report(ModStatus.Pending, detail: "na fila de download");
          await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
          try
          {
            // zip de branch do GitHub muda de conteúdo: pedaço de uma versão velha não pode ser continuado
            if (spec.Kind == ModKind.GitHub)
              TryDelete(part);

            report(ModStatus.Downloading, detail: attempt > 1 ? $"tentativa {attempt} de {options.Attempts}" : null);
            var result = await downloader.DownloadAsync(
              new DownloadRequest(url, part, Impersonate: spec.Kind == ModKind.ModDb, ETag: etag),
              new Progress<TransferProgress>(p => report(ModStatus.Downloading, p)),
              cancellationToken).ConfigureAwait(false);

            if (result.NotModified)
            {
              TryDelete(part);
              return new ArchiveOutcome(path, await HashAsync(path, report, cancellationToken).ConfigureAwait(false), null);
            }

            report(ModStatus.Verifying, detail: "conferindo o md5");
            var md5 = await Md5.HashAsync(part, cancellationToken).ConfigureAwait(false);
            string? warning = null;
            // GitHub de branch muda sempre: o md5 da lista só serve pra decidir se baixa, não pra recusar
            if (spec.Kind != ModKind.GitHub && spec.Md5 is { } expected && options.CheckMd5 && md5 != expected)
            {
              if (md5 != lastBadMd5)
              {
                lastBadMd5 = md5;
                TryDelete(part);
                throw new DownloadException($"md5 não bate (veio {md5[..8]}…, a lista diz {expected[..8]}…)");
              }
              warning = "o md5 não bate com a lista, mas veio igual duas vezes: o arquivo deve ter sido atualizado no ModDB";
            }

            File.Move(part, path, overwrite: true);
            hashes.Remember(path, md5);
            if (result.ETag is { Length: > 0 } newTag)
              rememberETag(newTag);
            return new ArchiveOutcome(path, md5, warning);
          }
          finally
          {
            gate.Release();
          }
        }
        catch (Exception ex) when (IsTransient(ex) && !cancellationToken.IsCancellationRequested)
        {
          last = ex;
          if (spec.Kind == ModKind.ModDb && ModDbPacer.IsThrottle(ex))
          {
            // bloqueio do ModDB não gasta tentativa (até 8 vezes): quem espera é a fila inteira, no ritmo do pacer
            if (!resolving)
              pacer.ReportThrottle(ex);
            if (++throttles <= 8)
            {
              attempt--;
              report(ModStatus.Resolving, detail: "o ModDB pediu calma; volta pra fila");
              continue;
            }
          }
          if (attempt == options.Attempts)
            break;

          var wait = TimeSpan.FromSeconds(Math.Min(60, 5 * Math.Pow(2.2, attempt - 1)) + Random.Shared.NextDouble() * 2);
          report(ModStatus.Downloading, detail: $"{ex.Message} — tentando de novo em {wait.TotalSeconds:0} s");
          await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
        }
      }
      throw new DownloadException(last?.Message ?? "o download falhou", last);
    }

    private async Task<string> HashAsync(string path, ArchiveReport report, CancellationToken cancellationToken)
    {
      report(ModStatus.Verifying, detail: "conferindo o md5");
      await hashGate.WaitAsync(cancellationToken).ConfigureAwait(false);
      try
      {
        return await hashes.GetAsync(path, cancellationToken).ConfigureAwait(false);
      }
      finally
      {
        hashGate.Release();
      }
    }

    private static bool IsTransient(Exception ex) =>
      ex is DownloadException or HttpRequestException or IOException or TimeoutException
      || ex is TaskCanceledException { InnerException: TimeoutException };

    private static void TryDelete(string path)
    {
      try
      {
        File.Delete(path);
      }
      catch (IOException)
      {
      }
    }
  }
}
