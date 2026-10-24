using System.Collections.Concurrent;
using System.Diagnostics;
using Provodnik.Core.Archives;
using Provodnik.Core.Desktop;
using Provodnik.Core.Downloads;
using Provodnik.Core.Install;
using Provodnik.Core.IO;
using Provodnik.Core.Layout;
using Provodnik.Core.Modpack;
using Provodnik.Core.Remote;
using Provodnik.Core.Repos;
using Provodnik.Core.Text;
using Provodnik.Features.Download;
using Provodnik.Features.Launch;
using Provodnik.Features.ModOrganizer;
using Provodnik.Features.Verify;

namespace Provodnik.Features.Install
{
  /// <summary>
  /// A instalação/atualização inteira, na ordem do botão "Install / Update G.A.M.M.A." do launcher original
  /// (repositórios → definição → lista → addons → extras → perfil → patches → atalhos), com as diferenças que motivaram
  /// o Проводник:
  ///   - downloads em paralelo (ModDB limitado pra não acordar o Cloudflare), cada arquivo baixado uma vez só mesmo
  ///     quando vários mods usam ele, e a extração andando junto com os downloads;
  ///   - só reextrai o mod cujo arquivo mudou (o md5 instalado fica no state.json);
  ///   - falha de um mod não para os outros, não estraga a pasta dele e entra no relatório pra "tentar de novo";
  ///   - cada etapa confere se deu certo antes de seguir.
  /// </summary>
  public sealed class InstallPipeline(
    IGammaApi api,
    IGitClient git,
    IFileDownloader downloader,
    IModDbResolver resolver,
    IArchiveExtractor extractor,
    IShortcutWriter shortcuts,
    ISystemInfo system)
  {
    /// <summary>O exe do Проводник, pro atalho da área de trabalho (nulo: não cria).</summary>
    public string? LauncherPath { get; set; }

    public async Task<InstallReport> RunAsync(GammaLayout layout, InstallOptions options, IInstallReporter reporter, CancellationToken cancellationToken)
    {
      var clock = Stopwatch.StartNew();
      var run = new Run(layout, options, reporter);
      run.State = run.StateStore.Load() with { LastStarted = DateTime.Now };

      try
      {
        await RunStagesAsync(run, cancellationToken).ConfigureAwait(false);
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
        reporter.Log(LogLevel.Warning, "Instalação cancelada. O que já entrou fica; é só rodar de novo pra continuar.");
        return run.Report(clock.Elapsed, cancelled: true);
      }
      catch (InstallAbortedException ex)
      {
        reporter.Log(LogLevel.Error, ex.Message);
        run.Failures.Add(new ModFailure(0, "Instalação", ex.Message));
      }
      catch (Exception ex) when (ex is not OperationCanceledException)
      {
        reporter.Log(LogLevel.Error, $"Erro inesperado: {ex}");
        run.Failures.Add(new ModFailure(0, "Instalação", ex.Message));
      }
      finally
      {
        var done = run.Failures.IsEmpty && !cancellationToken.IsCancellationRequested;
        if (done)
          run.State = run.State with { LastCompleted = DateTime.Now };
        TrySave(run);
      }

      var report = run.Report(clock.Elapsed, cancelled: false);
      reporter.Log(
        report.Succeeded ? LogLevel.Success : LogLevel.Warning,
        report.Succeeded
          ? $"Pronto em {clock.Elapsed:hh\\:mm\\:ss}: {report.Installed} instalados, {report.UpToDate} já estavam em dia."
          : $"Terminou em {clock.Elapsed:hh\\:mm\\:ss} com {report.Failures.Count} falha(s). Use \"Tentar de novo\" pra repetir só elas.");
      return report;
    }

    private async Task RunStagesAsync(Run run, CancellationToken cancellationToken)
    {
      Prepare(run, system);

      if (run.Options.SkipPreparation)
      {
        run.Reporter.Stage(InstallStage.Repositories, StageState.Skipped);
        run.Reporter.Stage(InstallStage.Definition, StageState.Skipped);
        run.Reporter.Stage(InstallStage.ModList, StageState.Skipped);
        run.List = LoadLocalList(run);
      }
      else
      {
        await SyncRepositoriesAsync(run, cancellationToken).ConfigureAwait(false);
        CopyDefinition(run);
        run.List = await FetchModListAsync(run, cancellationToken).ConfigureAwait(false);
      }

      await InstallModsAsync(run, cancellationToken).ConfigureAwait(false);
      InstallExtras(run, cancellationToken);
      SetUpProfile(run);
      PatchAnomaly(run);
      CreateShortcuts(run);
      VerifyActiveMods(run);
    }

    /// <summary>
    /// A "regra dos 5xx mods" da comunidade, conferida: todo mod ligado no modlist do perfil tem que ter pasta com
    /// conteúdo. Os que faltam e têm número ("312- ...") entram como falha daquele mod, então o "tentar de novo" pega
    /// eles; os outros (vêm do modpack_addons e dos arquivos grandes) viram uma falha geral.
    /// </summary>
    private static void VerifyActiveMods(Run run)
    {
      run.Reporter.Stage(InstallStage.Verify, StageState.Running);
      var result = ActiveModsCheck.Run(run.Layout, run.Metadata);
      if (result.Expected == 0)
      {
        run.Reporter.Stage(InstallStage.Verify, StageState.Warning, "modlist.txt não encontrado");
        return;
      }

      var failed = run.Failures.Select(f => f.Index).ToHashSet();
      var loose = new List<string>();
      foreach (var name in result.Missing)
      {
        var dash = name.IndexOf('-');
        if (dash > 0 && int.TryParse(name[..dash], out var index))
        {
          if (!failed.Contains(index))
            run.Failures.Add(new ModFailure(index, name[(dash + 1)..].Trim(), "ativo no MO2, mas a pasta está vazia"));
        }
        else
        {
          loose.Add(name);
        }
      }
      // sem número e sem pasta: nome que o próprio modlist do GAMMA escreveu errado ("DarkasleQif's..."); o MO2 mostra
      // igual pra todo mundo, não tem o que reinstalar
      foreach (var name in loose)
        run.Warn($"O modlist do GAMMA liga \"{name}\", que não existe em pasta nenhuma (erro de digitação na lista do GAMMA; o MO2 mostra igual pra todo mundo).");

      var numbered = result.Missing.Count - loose.Count;
      var summary = $"{result.Found} de {result.Expected} mods ativos";
      run.Reporter.Log(numbered == 0 ? LogLevel.Success : LogLevel.Warning, $"Conferência: {summary} no MO2{(loose.Count > 0 ? $" ({loose.Count} com o nome errado na lista do GAMMA)" : string.Empty)}");
      run.Reporter.Stage(InstallStage.Verify, numbered > 0 ? StageState.Failed : loose.Count > 0 ? StageState.Warning : StageState.Done, summary);
    }

    // ===== Preparando =====

    private static void Prepare(Run run, ISystemInfo system)
    {
      var (layout, reporter) = (run.Layout, run.Reporter);
      reporter.Stage(InstallStage.Preparing, StageState.Running);

      // a mesma checagem que a tela mostra antes de começar: o que falhar para aqui, explicado
      var checks = Preflight.Run(layout, system);
      foreach (var check in checks.Where(c => c.Level == CheckLevel.Warning))
        run.Warn($"{check.Title}: {check.Message}");
      if (checks.FirstOrDefault(c => c.Level == CheckLevel.Failed) is { } failed)
        throw new InstallAbortedException($"{failed.Title}: {failed.Message}");
      run.Metadata = File.Exists(layout.MetadataFile)
        ? ModpackMetadata.Parse(File.ReadAllText(layout.MetadataFile))
        : ModpackMetadata.Gamma;
      run.MetaTemplate = File.Exists(layout.MetaIniTemplate) ? File.ReadAllText(layout.MetaIniTemplate) : MetaIni.DefaultTemplate;
      run.SeparatorTemplate = File.Exists(layout.SeparatorMetaIni) ? File.ReadAllText(layout.SeparatorMetaIni) : MetaIni.SeparatorTemplate;

      // sobra de uma instalação interrompida
      try
      {
        FileTree.Delete(layout.WorkDir);
      }
      catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
      {
        reporter.Log(LogLevel.Warning, $"Não deu pra limpar a pasta de trabalho: {ex.Message}");
      }
      Directory.CreateDirectory(layout.WorkDir);
      Directory.CreateDirectory(layout.Downloads);
      Directory.CreateDirectory(layout.Mods);

      reporter.Log(LogLevel.Info, $"GAMMA em {layout.Root}");
      reporter.Stage(InstallStage.Preparing, StageState.Done);
    }

    // ===== Repositórios =====

    private async Task SyncRepositoriesAsync(Run run, CancellationToken cancellationToken)
    {
      var reporter = run.Reporter;
      reporter.Stage(InstallStage.Repositories, StageState.Running);
      var warned = false;

      foreach (var repository in GitRepository.All)
      {
        var directory = run.Layout.Repository(repository.Name);
        var first = !Directory.Exists(Path.Combine(directory, ".git"));
        reporter.Log(LogLevel.Info, first ? $"Clonando {repository.Name} (só na primeira vez; pode demorar)" : $"Atualizando {repository.Name}");

        var progress = new Progress<string>(line =>
        {
          if (ProgressLines.TryParseGit(line, out var phase, out var fraction))
            reporter.Stage(InstallStage.Repositories, StageState.Running, $"{repository.Name} · {GitPhase(phase)} {fraction:P0}");
        });
        try
        {
          await git.SyncAsync(repository, directory, progress, cancellationToken).ConfigureAwait(false);
        }
        catch (GitException ex) when (!repository.Required || Directory.Exists(Path.Combine(run.Layout.Definition(run.Metadata.FolderName), "modpack_data")))
        {
          warned = true;
          run.Warn($"{repository.Name} não atualizou, seguindo com a cópia que já existe: {ex.Message}");
        }
        catch (GitException ex)
        {
          throw new InstallAbortedException($"Não deu pra baixar a definição do GAMMA ({repository.Name}): {ex.Message}");
        }
      }

      reporter.Stage(InstallStage.Repositories, warned ? StageState.Warning : StageState.Done);
    }

    private static string GitPhase(string phase) => phase switch
    {
      "Receiving objects" => "baixando",
      "Resolving deltas" => "montando",
      "Updating files" or "Checking out files" => "escrevendo arquivos",
      "Counting objects" or "Enumerating objects" or "Compressing objects" => "preparando",
      _ => phase.ToLowerInvariant(),
    };

    // ===== Definição =====

    private static void CopyDefinition(Run run)
    {
      var (layout, reporter) = (run.Layout, run.Reporter);
      reporter.Stage(InstallStage.Definition, StageState.Running);

      var repository = layout.Repository(GitRepository.Definition.Name);
      var source = Path.Combine(repository, run.Metadata.FolderName);
      if (Directory.Exists(source))
        FileTree.CopyContents(source, layout.Definition(run.Metadata.FolderName));

      var version = Path.Combine(repository, "G.A.M.M.A_definition_version.txt");
      if (File.Exists(version))
      {
        File.Copy(version, layout.VersionFile, overwrite: true);
        File.Copy(version, Path.Combine(layout.Installer, "G.A.M.M.A_definition_version.txt"), overwrite: true);
        run.State = run.State with { DefinitionVersion = File.ReadAllText(version).Trim() };
      }

      if (!Directory.Exists(Path.Combine(layout.Definition(run.Metadata.FolderName), "modpack_data")))
        throw new InstallAbortedException("A definição do modpack (G.A.M.M.A\\modpack_data) não existe. Confira a conexão com o GitHub e tente de novo.");

      reporter.Log(LogLevel.Info, $"Definição do GAMMA na versão {run.State.DefinitionVersion ?? "?"}");
      reporter.Stage(InstallStage.Definition, StageState.Done, run.State.DefinitionVersion);
    }

    // ===== Lista de mods =====

    private async Task<ModList> FetchModListAsync(Run run, CancellationToken cancellationToken)
    {
      var (layout, reporter) = (run.Layout, run.Reporter);
      reporter.Stage(InstallStage.ModList, StageState.Running);
      try
      {
        var text = await api.GetModListAsync(run.Options.ModListKey, cancellationToken).ConfigureAwait(false);
        var list = ModList.Parse(text);
        var temp = layout.ModListFile + ".tmp";
        await File.WriteAllTextAsync(temp, text, cancellationToken).ConfigureAwait(false);
        File.Move(temp, layout.ModListFile, overwrite: true);
        reporter.Stage(InstallStage.ModList, StageState.Done, $"{list.Mods.Count} addons");
        return list;
      }
      catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException && !cancellationToken.IsCancellationRequested)
      {
        if (!File.Exists(layout.ModListFile))
          throw new InstallAbortedException($"Não deu pra baixar a lista de mods de stalker-gamma.com: {ex.Message}");

        run.Warn($"A lista de mods não baixou ({ex.Message}); usando o mods.txt que já existe.");
        reporter.Stage(InstallStage.ModList, StageState.Warning, "lista local");
        return ModList.Parse(await File.ReadAllTextAsync(layout.ModListFile, cancellationToken).ConfigureAwait(false));
      }
    }

    private static ModList LoadLocalList(Run run)
    {
      if (!File.Exists(run.Layout.ModListFile))
        throw new InstallAbortedException("Ainda não tem mods.txt: rode a instalação completa primeiro.");
      return ModList.Parse(File.ReadAllText(run.Layout.ModListFile));
    }

    // ===== Addons =====

    private async Task InstallModsAsync(Run run, CancellationToken cancellationToken)
    {
      var (layout, options, reporter, list) = (run.Layout, run.Options, run.Reporter, run.List!);
      reporter.Stage(InstallStage.Mods, StageState.Running);

      if (options.OnlyMods is null)
      {
        foreach (var separator in list.Entries.Where(e => e.IsSeparator && e.Name.Trim().Length > 0))
          ModInstaller.CreateSeparator(layout, separator, run.SeparatorTemplate);
      }

      var currentFolders = list.Entries.Select(e => e.FolderName).ToHashSet(StringComparer.OrdinalIgnoreCase);
      var mods = list.Mods.Where(m => options.OnlyMods?.Contains(m.Index) ?? true).ToList();
      foreach (var orphan in mods.Where(m => m.ArchiveName is null || m.Url is null))
        run.Fail(orphan, "a linha do mods.txt não tem link ou nome de arquivo");
      mods = mods.Where(m => m.ArchiveName is not null && m.Url is not null).ToList();

      // quem divide cada arquivo: o progresso do download aparece em todos eles
      var sharers = mods.GroupBy(m => m.ArchiveName!, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(g => g.Key, g => g.Select(m => m.Index).ToList(), StringComparer.OrdinalIgnoreCase);
      var archives = new ConcurrentDictionary<string, Lazy<Task<ArchiveOutcome>>>(StringComparer.OrdinalIgnoreCase);

      using var modDbGate = new SemaphoreSlim(Math.Max(1, options.ModDbParallel));
      using var httpGate = new SemaphoreSlim(Math.Max(1, options.HttpParallel));
      using var hashGate = new SemaphoreSlim(2);
      using var extractGate = new SemaphoreSlim(Math.Max(1, options.ExtractParallel));
      var pacer = new ModDbPacer(resolver, options.ModDbSpacing, options.ModDbCooldown);
      var fetcher = new ArchiveFetcher(downloader, pacer, run.Hashes, layout.Downloads, options, modDbGate, httpGate, hashGate);
      var installer = new ModInstaller(extractor);

      var done = 0;
      void Tick() => reporter.Stage(InstallStage.Mods, StageState.Running, $"{Interlocked.Increment(ref done)} de {mods.Count}");

      Task<ArchiveOutcome> Fetch(ModEntry mod)
      {
        var name = mod.ArchiveName!;
        var spec = new ArchiveSpec(name, mod.Kind, mod.Url!, mod.Md5);
        return archives.GetOrAdd(name, _ => new Lazy<Task<ArchiveOutcome>>(() => fetcher.FetchAsync(
          spec,
          run.ETagOf(name),
          tag => run.RememberETag(name, tag),
          (status, transfer, detail) =>
          {
            foreach (var index in sharers[name])
            {
              reporter.Mod(new ModProgress(index, status, transfer?.Fraction, transfer?.Received ?? 0, transfer?.Total, transfer?.BytesPerSecond, detail));
            }
          },
          cancellationToken))).Value;
      }

      await Parallel.ForEachAsync(
        mods,
        // todo mundo na fila de uma vez (quem segura são as vagas de download e extração): GitHub no fim da lista não espera o ModDB
        new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, mods.Count), CancellationToken = cancellationToken },
        async (mod, token) =>
        {
          reporter.Mod(new ModProgress(mod.Index, ModStatus.Checking));
          ArchiveOutcome outcome;
          try
          {
            outcome = await Fetch(mod).ConfigureAwait(false);
          }
          catch (Exception ex) when (ex is not OperationCanceledException)
          {
            run.Fail(mod, $"download: {ex.Message}");
            Tick();
            return;
          }
          if (outcome.Warning is { } warning)
            run.Warn($"{mod.Title}: {warning}");

          var folder = Path.Combine(layout.Mods, mod.FolderName);
          var fingerprint = InstalledMod.FingerprintOf(outcome.Md5, mod.Instructions);
          var installed = run.InstalledIn(mod.FolderName);
          if (!options.ForceExtract && installed?.Fingerprint == fingerprint && FileTree.HasContent(folder))
          {
            Interlocked.Increment(ref run.UpToDate);
            reporter.Mod(new ModProgress(mod.Index, ModStatus.UpToDate));
            Tick();
            return;
          }

          // a lista nova mudou o número do mod: a pasta antiga, com o mesmo arquivo e as mesmas instruções, só troca de nome
          if (!options.ForceExtract && !Directory.Exists(folder) && run.TakeRenamed(mod.ArchiveName!, fingerprint, currentFolders) is { } previous)
          {
            try
            {
              Directory.Move(Path.Combine(layout.Mods, previous), folder);
              run.MarkInstalled(mod.FolderName, new InstalledMod(mod.ArchiveName!, fingerprint, DateTime.Now));
              Interlocked.Increment(ref run.UpToDate);
              reporter.Mod(new ModProgress(mod.Index, ModStatus.UpToDate, Detail: $"era {previous}"));
              reporter.Log(LogLevel.Info, $"#{mod.Index} {mod.Title}: pasta renomeada de \"{previous}\"");
              Tick();
              return;
            }
            catch (IOException ex)
            {
              run.Warn($"{mod.Title}: não deu pra renomear \"{previous}\" ({ex.Message}); extraindo de novo");
            }
          }

          reporter.Mod(new ModProgress(mod.Index, ModStatus.WaitingExtraction, Detail: "na fila de extração"));
          await extractGate.WaitAsync(token).ConfigureAwait(false);
          try
          {
            reporter.Mod(new ModProgress(mod.Index, ModStatus.Extracting, 0));
            await installer.InstallAsync(
              layout,
              mod,
              outcome.Path,
              run.MetaTemplate,
              new Progress<double>(f => reporter.Mod(new ModProgress(mod.Index, ModStatus.Extracting, f))),
              // pasta de instrução que não existe: o original também ignora calado; fica como informação, não aviso
              message => reporter.Log(LogLevel.Info, $"{mod.Title}: {message} (igual ao launcher original)"),
              token).ConfigureAwait(false);

            run.MarkInstalled(mod.FolderName, new InstalledMod(mod.ArchiveName!, fingerprint, DateTime.Now));
            Interlocked.Increment(ref run.Installed);
            reporter.Mod(new ModProgress(mod.Index, ModStatus.Installed, 1));
          }
          catch (Exception ex) when (ex is not OperationCanceledException)
          {
            run.Fail(mod, $"extração: {ex.Message}");
          }
          finally
          {
            extractGate.Release();
            Tick();
          }
        }).ConfigureAwait(false);

      if (pacer.Cooldowns > 0)
        run.Warn($"O ModDB pediu calma {pacer.Cooldowns} vez(es) e a fila esperou. Se acontecer sempre, deixe 2 downloads do ModDB nas Configurações.");

      if (options.OnlyMods is null)
        RemoveOrphans(run, currentFolders);

      // grava no meio também: se o resto der errado, o que já entrou não é reextraído na próxima
      TrySave(run);
      var failed = run.Failures.Count;
      reporter.Stage(
        InstallStage.Mods,
        failed > 0 ? StageState.Warning : StageState.Done,
        failed > 0 ? $"{failed} com falha" : $"{run.Installed} instalados, {run.UpToDate} em dia");
    }

    /// <summary>
    /// Depois de uma atualização da lista, o que sobrou da numeração antiga: pastas que o Проводник instalou e que não
    /// estão mais na lista (o mod saiu do GAMMA, ou mudou de número e não deu pra renomear) e separadores velhos (só
    /// com o meta.ini). Pasta que veio do launcher original e o Проводник não conhece não é tocada.
    /// </summary>
    private static void RemoveOrphans(Run run, HashSet<string> currentFolders)
    {
      var mods = run.Layout.Mods;
      foreach (var folder in run.OrphanFolders(currentFolders))
      {
        try
        {
          FileTree.Delete(Path.Combine(mods, folder));
          run.Reporter.Log(LogLevel.Info, $"Removida \"{folder}\": saiu da lista do GAMMA");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
          run.Warn($"Não deu pra remover \"{folder}\", que saiu da lista: {ex.Message}");
        }
      }

      if (!Directory.Exists(mods))
        return;
      foreach (var separator in Directory.EnumerateDirectories(mods, "*_separator"))
      {
        var name = Path.GetFileName(separator);
        var onlyMeta = Directory.EnumerateFileSystemEntries(separator).All(e => Path.GetFileName(e).Equals("meta.ini", StringComparison.OrdinalIgnoreCase));
        if (!currentFolders.Contains(name) && char.IsAsciiDigit(name[0]) && onlyMeta)
          FileTree.Delete(separator);
      }
    }

    // ===== Extras, perfil, patches, atalhos =====

    private static void InstallExtras(Run run, CancellationToken cancellationToken)
    {
      var (layout, reporter) = (run.Layout, run.Reporter);
      reporter.Stage(InstallStage.Extras, StageState.Running, "arquivos grandes");
      ExtrasInstaller.CopyLargeFiles(layout, cancellationToken);
      reporter.Stage(InstallStage.Extras, StageState.Running, "armas do Gunslinger");
      ExtrasInstaller.CopyGunslinger(layout, run.List!, cancellationToken);
      reporter.Stage(InstallStage.Extras, StageState.Running, "modpack_addons");
      ExtrasInstaller.CopyModpackAddons(layout, run.Metadata, cancellationToken);
      reporter.Stage(InstallStage.Extras, StageState.Done);
    }

    private static void SetUpProfile(Run run)
    {
      run.Reporter.Stage(InstallStage.Profile, StageState.Running);
      try
      {
        var profile = ProfileSetup.Apply(run.Layout, run.Metadata);
        run.Reporter.Log(LogLevel.Info, $"Perfil do MO2 em {profile}");
        run.Reporter.Stage(InstallStage.Profile, StageState.Done, run.Metadata.FolderName);
      }
      catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
      {
        run.Failures.Add(new ModFailure(0, "Perfil do MO2", ex.Message));
        run.Reporter.Stage(InstallStage.Profile, StageState.Failed, ex.Message);
      }
    }

    private static void PatchAnomaly(Run run)
    {
      run.Reporter.Stage(InstallStage.Patches, StageState.Running);
      try
      {
        var anomaly = AnomalyPatcher.FindAnomaly(run.Layout);
        AnomalyPatcher.Apply(run.Layout, run.Metadata, anomaly, run.Options.DeleteReshadeDlls);
        run.Reporter.Log(LogLevel.Info, $"Patches aplicados no Anomaly em {anomaly}");
        run.Reporter.Stage(InstallStage.Patches, StageState.Done, anomaly);
      }
      catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
      {
        run.Failures.Add(new ModFailure(0, "Patches do Anomaly", ex.Message));
        run.Reporter.Stage(InstallStage.Patches, StageState.Failed, ex.Message);
      }
    }

    private void CreateShortcuts(Run run)
    {
      if (!run.Options.CreateShortcuts)
      {
        run.Reporter.Stage(InstallStage.Shortcuts, StageState.Skipped);
        return;
      }

      run.Reporter.Stage(InstallStage.Shortcuts, StageState.Running);
      try
      {
        // a área de trabalho de verdade (o original usava $Home\Desktop e quebrava com o OneDrive)
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var icon = Path.Combine(run.Layout.Definition(run.Metadata.FolderName), "modpack_data", "modpack_icon.ico");
        shortcuts.Create(Path.Combine(desktop, "G.A.M.M.A..lnk"), run.Layout.ModOrganizerExe, icon, run.Layout.Root);
        if (LauncherPath is { } launcher && File.Exists(launcher))
          // nome latino: o WScript.Shell grava o .lnk em ANSI e o cirílico virava "?????????"
          shortcuts.Create(Path.Combine(desktop, "Provodnik (G.A.M.M.A.).lnk"), launcher, launcher, Path.GetDirectoryName(launcher));
        run.Reporter.Stage(InstallStage.Shortcuts, StageState.Done);
      }
      catch (Exception ex)
      {
        run.Warn($"Atalhos não criados: {ex.Message}");
        run.Reporter.Stage(InstallStage.Shortcuts, StageState.Warning, ex.Message);
      }
    }

    private static void TrySave(Run run)
    {
      try
      {
        lock (run.Gate)
          run.StateStore.Save(run.State);
        run.Hashes.Save();
      }
      catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
      {
        run.Reporter.Log(LogLevel.Warning, $"Não deu pra gravar o estado da instalação: {ex.Message}");
      }
    }

    /// <summary>Tudo de uma rodada: o que foi decidido, o que deu errado e o estado que vai pro disco.</summary>
    private sealed class Run(GammaLayout layout, InstallOptions options, IInstallReporter reporter)
    {
      public readonly Lock Gate = new();
      public int Installed;
      public int UpToDate;

      public GammaLayout Layout { get; } = layout;

      public InstallOptions Options { get; } = options;

      public IInstallReporter Reporter { get; } = reporter;

      public InstallStateStore StateStore { get; } = new(Path.Combine(layout.DataDir, "state.json"));

      public HashCache Hashes { get; } = HashCache.Load(Path.Combine(layout.DataDir, "hashes.json"));

      public InstallState State { get; set; } = new();

      public ModpackMetadata Metadata { get; set; } = ModpackMetadata.Gamma;

      public string MetaTemplate { get; set; } = MetaIni.DefaultTemplate;

      public string SeparatorTemplate { get; set; } = MetaIni.SeparatorTemplate;

      public ModList? List { get; set; }

      public ConcurrentBag<ModFailure> Failures { get; } = [];

      public ConcurrentQueue<string> Warnings { get; } = new();

      public void Fail(ModEntry mod, string reason)
      {
        Failures.Add(new ModFailure(mod.Index, mod.Title, reason));
        Reporter.Mod(new ModProgress(mod.Index, ModStatus.Failed, Detail: reason));
        Reporter.Log(LogLevel.Error, $"#{mod.Index} {mod.Title}: {reason}");
      }

      public void Warn(string message)
      {
        Warnings.Enqueue(message);
        Reporter.Log(LogLevel.Warning, message);
      }

      public InstalledMod? InstalledIn(string folder)
      {
        lock (Gate)
          return State.Mods.GetValueOrDefault(folder);
      }

      public void MarkInstalled(string folder, InstalledMod mod)
      {
        lock (Gate)
          State.Mods[folder] = mod;
      }

      /// <summary>
      /// Uma pasta instalada antes com o mesmo arquivo e instruções, que não está mais na lista (o número mudou). Sai do
      /// estado na hora: dois mods não pegam a mesma.
      /// </summary>
      public string? TakeRenamed(string archive, string fingerprint, HashSet<string> currentFolders)
      {
        lock (Gate)
        {
          var match = State.Mods
            .Where(m => !currentFolders.Contains(m.Key) && m.Value.Archive.Equals(archive, StringComparison.OrdinalIgnoreCase) && m.Value.Fingerprint == fingerprint)
            .Select(m => m.Key)
            .FirstOrDefault(folder => FileTree.HasContent(Path.Combine(Layout.Mods, folder)));
          if (match is not null)
            State.Mods.Remove(match);
          return match;
        }
      }

      /// <summary>As pastas no estado que não estão mais na lista (já tiradas do estado).</summary>
      public IReadOnlyList<string> OrphanFolders(HashSet<string> currentFolders)
      {
        lock (Gate)
        {
          var orphans = State.Mods.Keys.Where(k => !currentFolders.Contains(k)).ToList();
          foreach (var orphan in orphans)
            State.Mods.Remove(orphan);
          return orphans.Where(o => Directory.Exists(Path.Combine(Layout.Mods, o))).ToList();
        }
      }

      public string? ETagOf(string archive)
      {
        lock (Gate)
          return State.ETags.GetValueOrDefault(archive);
      }

      public void RememberETag(string archive, string tag)
      {
        lock (Gate)
          State.ETags[archive] = tag;
      }

      public InstallReport Report(TimeSpan elapsed, bool cancelled) =>
        new(Installed, UpToDate, Failures.OrderBy(f => f.Index).ToList(), Warnings.ToList(), elapsed, cancelled);
    }
  }

  /// <summary>Erro que impede a instalação de seguir (pasta errada, MO2 aberto, sem a definição).</summary>
  public sealed class InstallAbortedException(string message) : Exception(message);
}
