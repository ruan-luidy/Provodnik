using System.Diagnostics;
using Provodnik.Core.Bootstrap;
using Provodnik.Core.Downloads;
using Provodnik.Core.Install;
using Provodnik.Core.IO;
using Provodnik.Core.Layout;
using Provodnik.Core.Modpack;
using Provodnik.Core.Text;

namespace Provodnik.Features.Bootstrap
{
  public enum BootstrapStep
  {
    Prepare,
    System,
    Gamma,
    Anomaly,
    ModOrganizer,
  }

  /// <summary>Uma etapa mudou: o estado, a linha de detalhe ("baixando 3,2 de 12 GB") e quanto dela já foi (0 a 1).</summary>
  public sealed record BootstrapProgress(BootstrapStep Step, StageState State, string? Detail = null, double? Fraction = null);

  /// <summary>
  /// O que a pessoa escolheu: pôr as pastas na exclusão do Defender, instalar o que falta no Windows e o exe do
  /// Проводник que vai junto pra pasta do GAMMA (nulo: não copia).
  /// </summary>
  public sealed record BootstrapOptions(bool DefenderExclusion = true, bool Dependencies = true, string? LauncherExe = null);

  public sealed record BootstrapResult(bool Succeeded, string? Error = null);

  /// <summary>
  /// Do zero até o ponto em que a instalação de sempre começa, cada passo manual da wiki do GAMMA:
  ///   1. as pastas GAMMA e Anomaly (e o 7zr.exe pra abrir o RC3);
  ///   2. o Windows: exclusão no Defender, Visual C++ e DirectX que faltarem e caminhos longos, num UAC só;
  ///   3. o GAMMA RC3 (o MO2 e o instalador) pelo Mediafire, conferido pelo SHA-256 e extraído no GAMMA;
  ///   4. o Anomaly 1.5.3 pelo ModDB, conferido e extraído no Anomaly;
  ///   5. o "First Install Initialization": o ModOrganizer.ini já apontando pro Anomaly.
  /// Passo que já está feito (o RC3 extraído, um Anomaly inteiro na pasta) é pulado, então rodar de novo continua de
  /// onde parou; download pela metade continua do .part. Arquivo que a pessoa já baixou na pasta Downloads do Windows é
  /// usado no lugar de baixar de novo. O "abrir o Anomaly até o menu" da wiki fica de fora: o que ele cria (o
  /// appdata\user.ltx) vem pronto no modpack_patches do GAMMA, que a instalação copia por cima.
  /// </summary>
  public sealed class BootstrapPipeline(IBootstrapServices services)
  {
    private const int Attempts = 3;

    // os hashes fixos (os testes trocam: não dá pra fabricar um arquivo com o hash do 7zr de verdade)
    internal string SevenZipSha256 { get; init; } = BootstrapSources.SevenZipSha256;

    internal string SevenZipExtraSha256 { get; init; } = BootstrapSources.SevenZipExtraSha256;

    internal string AnomalySha256 { get; init; } = BootstrapSources.AnomalySha256;

    public async Task<BootstrapResult> RunAsync(BootstrapTarget target, BootstrapOptions options, IProgress<BootstrapProgress> progress, CancellationToken cancellationToken)
    {
      var step = BootstrapStep.Prepare;
      try
      {
        step = BootstrapStep.Prepare;
        var sevenZip = await PrepareAsync(target, progress, cancellationToken).ConfigureAwait(false);
        step = BootstrapStep.System;
        await SystemAsync(target, options, progress, cancellationToken).ConfigureAwait(false);
        step = BootstrapStep.Gamma;
        await GammaAsync(target, sevenZip, progress, cancellationToken).ConfigureAwait(false);
        step = BootstrapStep.Anomaly;
        await AnomalyAsync(target, progress, cancellationToken).ConfigureAwait(false);
        step = BootstrapStep.ModOrganizer;
        ModOrganizer(target, options, progress);

        // o que sobrou do caminho (o 7zr, os instaladores do Windows, a pasta de extração)
        FileTree.Delete(target.WorkDir);
        return new BootstrapResult(true);
      }
      catch (OperationCanceledException)
      {
        progress.Report(new(step, StageState.Pending, L.T("features.bootstrap.cancelled")));
        throw;
      }
      catch (Exception ex) when (ex is not OutOfMemoryException)
      {
        progress.Report(new(step, StageState.Failed, ex.Message));
        return new BootstrapResult(false, ex.Message);
      }
    }

    private async Task<string?> PrepareAsync(BootstrapTarget target, IProgress<BootstrapProgress> progress, CancellationToken cancellationToken)
    {
      progress.Report(new(BootstrapStep.Prepare, StageState.Running, L.T("features.bootstrap.prepare.folders")));
      Directory.CreateDirectory(target.Gamma);
      Directory.CreateDirectory(target.Anomaly);
      Directory.CreateDirectory(target.Downloads);
      Directory.CreateDirectory(target.Tools);

      // o RC3 já extraído traz o 7z.exe dele: o 7zr só serve pra abrir o RC3
      if (target.GammaReady)
      {
        progress.Report(new(BootstrapStep.Prepare, StageState.Done, L.T("features.bootstrap.prepare.done")));
        return null;
      }

      // o 7zr (32 bits) só abre o pacote extra do 7-Zip, que traz o 7za de 64 bits: é ele que abre o RC3 (12 GB) sem
      // esbarrar no limite de memória de um processo de 32 bits
      var sevenZa = Path.Combine(target.Tools, "7za", "x64", "7za.exe");
      if (!File.Exists(sevenZa))
      {
        progress.Report(new(BootstrapStep.Prepare, StageState.Running, L.T("features.bootstrap.prepare.sevenZip")));
        var sevenZr = Path.Combine(target.Tools, "7zr.exe");
        var extra = Path.Combine(target.Tools, "7z-extra.7z");
        await ToolAsync(BootstrapSources.SevenZipUrl, sevenZr, SevenZipSha256, progress, cancellationToken).ConfigureAwait(false);
        await ToolAsync(BootstrapSources.SevenZipExtraUrl, extra, SevenZipExtraSha256, progress, cancellationToken).ConfigureAwait(false);
        await services.Extractor(sevenZr).ExtractAsync(extra, Path.Combine(target.Tools, "7za"), null, cancellationToken).ConfigureAwait(false);
        if (!File.Exists(sevenZa))
          throw new InvalidDataException(L.T("features.bootstrap.noMarker", Path.GetFileName(extra), Path.Combine("x64", "7za.exe")));
      }

      progress.Report(new(BootstrapStep.Prepare, StageState.Done, L.T("features.bootstrap.prepare.done")));
      return sevenZa;
    }

    /// <summary>Uma ferramenta pequena de versão fixa, baixada de novo se o hash não bater.</summary>
    private async Task ToolAsync(string url, string destination, string sha256, IProgress<BootstrapProgress> progress, CancellationToken cancellationToken)
    {
      if (File.Exists(destination) && await Sha256.HashAsync(destination, null, cancellationToken).ConfigureAwait(false) == sha256)
        return;
      await DownloadFreshAsync(BootstrapStep.Prepare, url, destination, progress, cancellationToken).ConfigureAwait(false);
      if (await Sha256.HashAsync(destination, null, cancellationToken).ConfigureAwait(false) != sha256)
      {
        File.Delete(destination);
        throw new InvalidDataException(L.T("features.bootstrap.prepare.sevenZipHash", Path.GetFileName(destination)));
      }
    }

    private async Task SystemAsync(BootstrapTarget target, BootstrapOptions options, IProgress<BootstrapProgress> progress, CancellationToken cancellationToken)
    {
      progress.Report(new(BootstrapStep.System, StageState.Running, L.T("features.bootstrap.system.checking")));
      var found = await services.DetectAsync(cancellationToken).ConfigureAwait(false);

      // instalador do Windows vai pra pasta de ferramentas: sai junto no fim
      string? vc = null, dx = null;
      if (options.Dependencies && !found.VcRedist)
      {
        vc = Path.Combine(target.Tools, "vc_redist.x64.exe");
        progress.Report(new(BootstrapStep.System, StageState.Running, L.T("features.bootstrap.system.downloading", "Visual C++")));
        await DownloadFreshAsync(BootstrapStep.System, BootstrapSources.VcRedistUrl, vc, progress, cancellationToken).ConfigureAwait(false);
      }
      if (options.Dependencies && !found.DirectX)
      {
        dx = Path.Combine(target.Tools, "dxwebsetup.exe");
        progress.Report(new(BootstrapStep.System, StageState.Running, L.T("features.bootstrap.system.downloading", "DirectX")));
        await DownloadFreshAsync(BootstrapStep.System, BootstrapSources.DirectXUrl, dx, progress, cancellationToken).ConfigureAwait(false);
      }

      var resultFile = Path.Combine(target.Tools, "elevated.txt");
      File.Delete(resultFile);
      var plan = new ElevatedPlan(
        options.DefenderExclusion ? [target.Gamma, target.Anomaly] : [],
        vc,
        dx,
        LongPaths: found.LongPaths == false,
        resultFile);

      var notes = new List<string>();
      var warning = false;
      if (!plan.IsEmpty)
      {
        progress.Report(new(BootstrapStep.System, StageState.Running, L.T("features.bootstrap.system.elevating")));
        var outcome = await services.RunElevatedAsync(plan, cancellationToken).ConfigureAwait(false);
        if (outcome.Declined)
        {
          warning = true;
          notes.Add(L.T("features.bootstrap.system.declined"));
        }
        else
        {
          void Note(string step, bool asked, string name)
          {
            if (!asked)
              return;
            var result = outcome.Results.GetValueOrDefault(step);
            if (ElevatedSetup.Succeeded(result))
            {
              notes.Add(L.T("features.bootstrap.system.ok", name));
              return;
            }
            warning = true;
            notes.Add(L.T("features.bootstrap.system.failed", name, result ?? "?"));
          }
          Note(ElevatedSetup.Defender, plan.DefenderExclusions.Count > 0, L.T("features.bootstrap.system.defender"));
          Note(ElevatedSetup.VcRedist, vc is not null, "Visual C++");
          Note(ElevatedSetup.DirectX, dx is not null, "DirectX");
          Note(ElevatedSetup.LongPaths, plan.LongPaths, L.T("features.bootstrap.system.longPaths"));
        }
      }
      else
      {
        notes.Add(L.T("features.bootstrap.system.nothing"));
      }

      // Norton, Avast, AVG, Bitdefender: não dá pra mexer neles daqui, só avisar
      if (found.OtherAntivirus.Count > 0)
      {
        warning = true;
        notes.Add(L.T("features.bootstrap.system.antivirus", string.Join(", ", found.OtherAntivirus)));
      }

      progress.Report(new(BootstrapStep.System, warning ? StageState.Warning : StageState.Done, string.Join(" ", notes)));
    }

    private async Task GammaAsync(BootstrapTarget target, string? sevenZip, IProgress<BootstrapProgress> progress, CancellationToken cancellationToken)
    {
      if (target.GammaReady && !File.Exists(Moving(target, BootstrapStep.Gamma)))
      {
        progress.Report(new(BootstrapStep.Gamma, StageState.Skipped, L.T("features.bootstrap.gamma.present")));
        return;
      }

      progress.Report(new(BootstrapStep.Gamma, StageState.Running, L.T("features.bootstrap.gamma.looking")));
      HostedFile? hosted = null;
      try
      {
        var files = await services.ListMediafireFolderAsync(BootstrapSources.GammaMediafireFolder, cancellationToken).ConfigureAwait(false);
        hosted = files.FirstOrDefault(f => f.Name.Equals(BootstrapSources.GammaArchive, StringComparison.OrdinalIgnoreCase))
          ?? files.Where(f => f.Name.EndsWith(".7z", StringComparison.OrdinalIgnoreCase)).MaxBy(f => f.Size);
      }
      catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidDataException or System.Text.Json.JsonException)
      {
        if (cancellationToken.IsCancellationRequested)
          throw;
      }
      // sem a lista do Mediafire, só serve o RC3 que já está no disco (sem o hash pra conferir)
      if (hosted is null)
      {
        var local = new[] { target.Downloads }.Concat(services.ExistingDownloadFolders)
          .Select(f => Path.Combine(f, BootstrapSources.GammaArchive))
          .FirstOrDefault(File.Exists)
          ?? throw new DownloadException(L.T("features.bootstrap.gamma.noListing", string.Join("  ", BootstrapSources.GammaManualMirrors)));
        hosted = new HostedFile(BootstrapSources.GammaArchive, new FileInfo(local).Length, string.Empty, null);
      }

      var archive = await ArchiveAsync(
        BootstrapStep.Gamma,
        target,
        hosted.Name,
        hosted.Sha256,
        hosted.Size,
        ct => services.ResolveLinkAsync(BootstrapSources.MediafireFilePage(hosted.QuickKey), ct),
        impersonate: false,
        progress,
        cancellationToken).ConfigureAwait(false);

      await ExtractAsync(BootstrapStep.Gamma, target, archive, sevenZip!, "ModOrganizer.exe", target.Gamma, progress, cancellationToken).ConfigureAwait(false);
      if (!target.GammaReady)
        throw new InvalidDataException(L.T("features.bootstrap.gamma.incomplete"));
      // o perfil que vem no RC3 é o GAMMA de quando o RC3 saiu, não escolha da pessoa: sem ele a primeira instalação
      // monta o perfil só com a lista atual, em vez de manter ligado o que o GAMMA já desligou
      var shipped = Path.Combine(target.Layout.Profiles, ModpackMetadata.Gamma.FolderName, "modlist.txt");
      if (File.Exists(shipped))
        File.Delete(shipped);
      progress.Report(new(BootstrapStep.Gamma, StageState.Done, L.T("features.bootstrap.done", hosted.Name)));
    }

    private async Task AnomalyAsync(BootstrapTarget target, IProgress<BootstrapProgress> progress, CancellationToken cancellationToken)
    {
      if (target.AnomalyReady && !File.Exists(Moving(target, BootstrapStep.Anomaly)))
      {
        progress.Report(new(BootstrapStep.Anomaly, StageState.Skipped, L.T("features.bootstrap.anomaly.present")));
        return;
      }

      // o CDN do ModDB só entrega pra quem parece navegador: o curl-impersonate que veio no RC3
      var archive = await ArchiveAsync(
        BootstrapStep.Anomaly,
        target,
        BootstrapSources.AnomalyArchive,
        AnomalySha256,
        size: null,
        ct => services.ResolveLinkAsync(BootstrapSources.AnomalyModDbStart, ct),
        impersonate: true,
        progress,
        cancellationToken).ConfigureAwait(false);

      await ExtractAsync(BootstrapStep.Anomaly, target, archive, target.Layout.SevenZip, "AnomalyLauncher.exe", target.Anomaly, progress, cancellationToken).ConfigureAwait(false);
      if (!target.AnomalyReady)
        throw new InvalidDataException(L.T("features.bootstrap.anomaly.incomplete"));
      progress.Report(new(BootstrapStep.Anomaly, StageState.Done, L.T("features.bootstrap.done", BootstrapSources.AnomalyArchive)));
    }

    private void ModOrganizer(BootstrapTarget target, BootstrapOptions options, IProgress<BootstrapProgress> progress)
    {
      progress.Report(new(BootstrapStep.ModOrganizer, StageState.Running));
      var layout = target.Layout;
      var existing = File.Exists(layout.ModOrganizerIni) ? File.ReadAllText(layout.ModOrganizerIni) : null;
      var version = FileVersionInfo.GetVersionInfo(layout.ModOrganizerExe) is { FileMajorPart: > 0 } info
        ? $"{info.FileMajorPart}.{info.FileMinorPart}.{info.FileBuildPart}"
        : null;
      var ini = Mo2Instance.Configure(existing, target.Gamma, target.Anomaly, ModpackMetadata.Gamma.FolderName, version);
      File.WriteAllText(layout.ModOrganizerIni, ini);

      // o MO2 só roda como instância portátil (a pasta dele) com o portable.txt do lado
      var portable = Path.Combine(target.Gamma, "portable.txt");
      if (!File.Exists(portable))
        File.WriteAllText(portable, string.Empty);

      // o Проводник vai morar no GAMMA, como o launcher original
      if (options.LauncherExe is { } exe && File.Exists(exe))
      {
        var copy = Path.Combine(target.Gamma, Path.GetFileName(exe));
        if (!string.Equals(Path.GetFullPath(exe), Path.GetFullPath(copy), StringComparison.OrdinalIgnoreCase))
          File.Copy(exe, copy, overwrite: true);
      }

      progress.Report(new(BootstrapStep.ModOrganizer, StageState.Done, L.T("features.bootstrap.mo2.done", target.Anomaly)));
    }

    /// <summary>
    /// O arquivo pronto e conferido: o que a pessoa já baixou (Downloads do Windows), o que já veio numa rodada
    /// anterior, ou baixado agora (continuando o .part). Devolve o caminho.
    /// </summary>
    private async Task<string> ArchiveAsync(
      BootstrapStep step,
      BootstrapTarget target,
      string name,
      string? sha256,
      long? size,
      Func<CancellationToken, Task<string>> link,
      bool impersonate,
      IProgress<BootstrapProgress> progress,
      CancellationToken cancellationToken)
    {
      var ours = Path.Combine(target.Downloads, name);
      var candidates = new[] { ours }.Concat(services.ExistingDownloadFolders.Select(f => Path.Combine(f, name)));
      foreach (var candidate in candidates.Where(File.Exists))
      {
        if (size is { } expected && new FileInfo(candidate).Length != expected)
          continue;
        if (await VerifyAsync(step, candidate, sha256, progress, cancellationToken).ConfigureAwait(false))
          return candidate;
        if (candidate == ours)
          File.Delete(ours);
      }

      await DownloadAsync(step, link, ours, impersonate, size, progress, cancellationToken).ConfigureAwait(false);
      if (!await VerifyAsync(step, ours, sha256, progress, cancellationToken).ConfigureAwait(false))
      {
        File.Delete(ours);
        throw new InvalidDataException(L.T("features.bootstrap.hashMismatch", name));
      }
      return ours;
    }

    /// <summary>Confere o SHA-256 (sem hash conhecido, aceita). Um .sha256 do lado guarda que já conferiu.</summary>
    private static async Task<bool> VerifyAsync(BootstrapStep step, string file, string? sha256, IProgress<BootstrapProgress> progress, CancellationToken cancellationToken)
    {
      if (sha256 is null)
        return true;
      var mark = file + ".sha256";
      var info = new FileInfo(file);
      var stamp = $"{sha256} {info.Length} {info.LastWriteTimeUtc.Ticks}";
      if (File.Exists(mark) && File.ReadAllText(mark).Trim() == stamp)
        return true;

      var name = Path.GetFileName(file);
      var hashing = new Reporter<double>(f => progress.Report(new(step, StageState.Running, L.T("features.bootstrap.verifying", name, (int)(f * 100)), f)));
      var actual = await Sha256.HashAsync(file, hashing, cancellationToken).ConfigureAwait(false);
      if (!string.Equals(actual, sha256, StringComparison.OrdinalIgnoreCase))
        return false;
      try
      {
        File.WriteAllText(mark, stamp);
      }
      catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
      {
        // na pasta Downloads da pessoa pode não deixar: só confere de novo da próxima vez
      }
      return true;
    }

    /// <summary>
    /// Baixa pra <paramref name="destination"/>.part e renomeia no fim (arquivo com o nome final é arquivo inteiro).
    /// Link que venceu (o do Mediafire dura pouco) é pedido de novo a cada tentativa.
    /// </summary>
    private async Task DownloadAsync(
      BootstrapStep step,
      Func<CancellationToken, Task<string>> link,
      string destination,
      bool impersonate,
      long? size,
      IProgress<BootstrapProgress> progress,
      CancellationToken cancellationToken)
    {
      var part = destination + ".part";
      var name = Path.GetFileName(destination);
      for (var attempt = 1; ; attempt++)
      {
        try
        {
          progress.Report(new(step, StageState.Running, L.T("features.bootstrap.linking", name)));
          var url = await link(cancellationToken).ConfigureAwait(false);
          var transfer = new Reporter<TransferProgress>(t => progress.Report(new(step, StageState.Running, Describe(name, t, size), t.Fraction)));
          await services.Downloader.DownloadAsync(new DownloadRequest(url, part, impersonate), transfer, cancellationToken).ConfigureAwait(false);
          if (size is { } expected && new FileInfo(part).Length != expected)
          {
            File.Delete(part);
            throw new DownloadException(L.T("features.bootstrap.sizeMismatch", name, ByteSize.Format(expected)));
          }
          File.Move(part, destination, overwrite: true);
          return;
        }
        catch (DownloadException ex) when (attempt < Attempts)
        {
          progress.Report(new(step, StageState.Running, L.T("features.bootstrap.retrying", ex.Message, attempt + 1, Attempts)));
          await Task.Delay(TimeSpan.FromSeconds(5 * attempt), cancellationToken).ConfigureAwait(false);
        }
      }
    }

    /// <summary>Um arquivo pequeno de novo do zero (os instaladores da Microsoft).</summary>
    private Task DownloadFreshAsync(BootstrapStep step, string url, string destination, IProgress<BootstrapProgress> progress, CancellationToken cancellationToken)
    {
      File.Delete(destination);
      File.Delete(destination + ".part");
      return DownloadAsync(step, _ => Task.FromResult(url), destination, impersonate: false, size: null, progress, cancellationToken);
    }

    /// <summary>
    /// Extrai numa pasta à parte, acha onde ficou o <paramref name="marker"/> (o arquivo pode ter uma pasta na raiz ou
    /// não) e move o conteúdo dali pro destino. O arquivo baixado pelo Проводник sai depois; o da pessoa fica.
    /// </summary>
    private async Task ExtractAsync(BootstrapStep step, BootstrapTarget target, string archive, string sevenZip, string marker, string destination, IProgress<BootstrapProgress> progress, CancellationToken cancellationToken)
    {
      var name = Path.GetFileName(archive);
      var staging = target.Staging(Path.GetFileNameWithoutExtension(archive));
      FileTree.Delete(staging);
      var extracting = new Reporter<double>(f => progress.Report(new(step, StageState.Running, L.T("features.bootstrap.extracting", name, (int)(f * 100)), f)));
      progress.Report(new(step, StageState.Running, L.T("features.bootstrap.extracting", name, 0), 0));
      await services.Extractor(sevenZip).ExtractAsync(archive, staging, extracting, cancellationToken).ConfigureAwait(false);

      var root = ExtractedRoot.Find(staging, marker) ?? throw new InvalidDataException(L.T("features.bootstrap.noMarker", name, marker));
      progress.Report(new(step, StageState.Running, L.T("features.bootstrap.moving", destination)));
      // mover parado no meio deixaria o marcador no lugar e a pasta pela metade: a marca diz pra refazer
      var moving = Moving(target, step);
      File.WriteAllText(moving, archive);
      await Task.Run(() => FileTree.MoveContents(root, destination), cancellationToken).ConfigureAwait(false);
      File.Delete(moving);
      FileTree.Delete(staging);

      if (archive.StartsWith(target.Downloads, StringComparison.OrdinalIgnoreCase))
      {
        File.Delete(archive);
        File.Delete(archive + ".sha256");
      }
    }

    private static string Moving(BootstrapTarget target, BootstrapStep step) => Path.Combine(target.WorkDir, $"{step}.moving".ToLowerInvariant());

    private static string Describe(string name, TransferProgress transfer, long? size)
    {
      var total = transfer.Total ?? size;
      var amount = total is { } t ? L.T("features.bootstrap.amountOf", ByteSize.Format(transfer.Received), ByteSize.Format(t)) : ByteSize.Format(transfer.Received);
      var speed = ByteSize.FormatSpeed(transfer.BytesPerSecond);
      return L.T("features.bootstrap.downloading", name, speed.Length > 0 ? $"{amount} · {speed}" : amount);
    }

    /// <summary>IProgress sem contexto de sincronização: chega na ordem, na thread de quem informa.</summary>
    private sealed class Reporter<T>(Action<T> report) : IProgress<T>
    {
      public void Report(T value) => report(value);
    }
  }
}
