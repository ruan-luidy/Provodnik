using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Provodnik.Core.Install;
using Provodnik.Core.Text;
using Provodnik.Features.Install;
using Provodnik.Wpf.Shared.Services;

namespace Provodnik.Wpf.Features.Install
{
  /// <summary>
  /// A instalação rodando, vista pela tela. O pipeline trabalha fora da thread da tela e manda centenas de avisos
  /// por segundo; aqui eles entram numa fila e a cada 150 ms vira um lote só na tela (a última posição de cada mod,
  /// as etapas e o registro). O Início, a tela de instalação, a de Mods e a barra de tarefas olham tudo daqui.
  /// Enquanto roda, o Windows não entra em suspensão.
  /// </summary>
  public sealed partial class InstallService : ObservableObject
  {
    private readonly InstallPipeline _pipeline;
    private readonly GammaSession _session;
    private readonly LogBook _log;
    private readonly SettingsStore _settings;
    private readonly DispatcherTimer _timer;
    private readonly ConcurrentDictionary<int, ModProgress> _pendingMods = new();
    private readonly ConcurrentQueue<(InstallStage Stage, StageState State, string? Detail)> _pendingStages = new();
    private readonly Dictionary<int, ModRow> _rows = [];
    private CancellationTokenSource? _cancel;

    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private bool _isCancelling;
    [ObservableProperty] private InstallReport? _lastReport;
    [ObservableProperty] private string? _lastLogFile;
    [ObservableProperty] private int _total;
    [ObservableProperty] private int _done;
    [ObservableProperty] private int _downloading;
    [ObservableProperty] private int _extracting;
    [ObservableProperty] private string _speedText = "—";
    [ObservableProperty] private string _headline = "Pronto pra instalar";

    /// <summary>De 0 a 1, pra barra grande e pra barra de tarefas.</summary>
    [ObservableProperty] private double _overall;

    public InstallService(InstallPipeline pipeline, GammaSession session, LogBook log, SettingsStore settings)
    {
      _pipeline = pipeline;
      _session = session;
      _log = log;
      _settings = settings;
      _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(150), DispatcherPriority.Background, (_, _) => Flush(), Application.Current.Dispatcher);
      _timer.Stop();
      // parado, o total é o tamanho da lista que está no disco
      Total = session.ModList?.Mods.Count ?? 0;
      session.Reloaded += (_, _) =>
      {
        if (!IsRunning)
          Total = session.ModList?.Mods.Count ?? 0;
      };
    }

    public IReadOnlyList<StageRow> Stages { get; } = StageRow.CreateAll();

    /// <summary>Os mods trabalhando agora (baixando, conferindo, extraindo).</summary>
    public ObservableCollection<ModRow> Active { get; } = [];

    public ObservableCollection<ModRow> Failures { get; } = [];

    /// <summary>A rodada terminou (com sucesso, falha ou cancelada).</summary>
    public event EventHandler<InstallReport>? Finished;

    /// <summary>O estado de um mod nesta rodada (a tela de Mods mostra junto).</summary>
    public ModRow? RowFor(int index) => _rows.GetValueOrDefault(index);

    public Task<InstallReport?> InstallAsync(bool forceExtract) =>
      RunAsync(Options() with { ForceExtract = forceExtract });

    /// <summary>Só os que falharam na última rodada, sem atualizar repositórios nem lista de novo.</summary>
    public Task<InstallReport?> RetryFailedAsync()
    {
      var failed = LastReport?.Failures.Where(f => f.Index > 0).Select(f => f.Index).ToHashSet();
      return failed is { Count: > 0 }
        ? RunAsync(Options() with { OnlyMods = failed, SkipPreparation = true })
        : InstallAsync(forceExtract: false);
    }

    /// <summary>Reextrai (e baixa, se precisar) só estes mods.</summary>
    public Task<InstallReport?> ReinstallAsync(IEnumerable<int> indices) =>
      RunAsync(Options() with { OnlyMods = indices.ToHashSet(), SkipPreparation = true, ForceExtract = true });

    public void Cancel()
    {
      if (!IsRunning || _cancel is null)
        return;
      IsCancelling = true;
      Headline = "Cancelando...";
      _cancel.Cancel();
    }

    private InstallOptions Options() => _settings.Current.ToInstallOptions();

    private async Task<InstallReport?> RunAsync(InstallOptions options)
    {
      if (IsRunning || _session.Layout is not { } layout)
        return null;

      Reset(options);
      IsRunning = true;
      _cancel = new CancellationTokenSource();
      _pipeline.LauncherPath = Environment.ProcessPath;
      KeepAwake(true);
      _timer.Start();

      InstallReport report;
      try
      {
        using var reporter = new LogFileReporter(new QueueReporter(this), layout.LogsDir);
        LastLogFile = reporter.Path;
        var token = _cancel.Token;
        report = await Task.Run(() => _pipeline.RunAsync(layout, options, reporter, token)).ConfigureAwait(true);
      }
      finally
      {
        Flush();
        _timer.Stop();
        KeepAwake(false);
        _cancel.Dispose();
        _cancel = null;
        // relê antes de marcar parado: o total desta rodada (uma parte só, no tentar de novo) fica na tela
        _session.Reload();
        IsRunning = false;
        IsCancelling = false;
      }

      LastReport = report;
      // falhas que não vieram de um mod rodando (a conferência final, a instalação que nem começou) também vão pra lista
      foreach (var failure in report.Failures.Where(f => Failures.All(r => r.Index != f.Index || f.Index == 0)))
      {
        var entry = _session.ModList?.Entries.ElementAtOrDefault(failure.Index - 1);
        var row = new ModRow(failure.Index, entry?.Index == failure.Index ? entry : null, failure.Name);
        row.Apply(new ModProgress(failure.Index, ModStatus.Failed, Detail: failure.Reason));
        Failures.Add(row);
      }
      Overall = report.Cancelled ? Overall : 1;
      Headline = report switch
      {
        { Cancelled: true } => "Cancelada",
        { Succeeded: true } => $"Pronto: {report.Installed} instalados, {report.UpToDate} em dia",
        _ => $"Terminou com {report.Failures.Count} falha(s)",
      };
      Finished?.Invoke(this, report);
      return report;
    }

    private void Reset(InstallOptions options)
    {
      foreach (var stage in Stages)
      {
        stage.State = StageState.Pending;
        stage.Detail = null;
      }
      _rows.Clear();
      _pendingMods.Clear();
      _pendingStages.Clear();
      Active.Clear();
      Failures.Clear();
      LastReport = null;
      Done = 0;
      Downloading = 0;
      Extracting = 0;
      Overall = 0;
      SpeedText = "—";
      Total = options.OnlyMods?.Count ?? _session.ModList?.Mods.Count ?? 0;
      Headline = "Começando...";
    }

    /// <summary>O lote da vez: etapas, a última posição de cada mod que mudou e os totais.</summary>
    private void Flush()
    {
      while (_pendingStages.TryDequeue(out var update))
      {
        var row = Stages.First(s => s.Stage == update.Stage);
        row.State = update.State;
        row.Detail = update.Detail;
        if (update.State == StageState.Running)
          Headline = update.Detail is { } detail ? $"{row.Title} · {detail}" : row.Title;

        // a lista nova chegou: os nomes dos mods (e o total) vêm dela
        if (update.Stage == InstallStage.ModList && update.State is StageState.Done or StageState.Warning)
        {
          _session.Reload();
          if (Total == 0)
            Total = _session.ModList?.Mods.Count ?? 0;
        }
      }

      foreach (var index in _pendingMods.Keys.ToList())
      {
        if (!_pendingMods.TryRemove(index, out var progress))
          continue;

        if (!_rows.TryGetValue(index, out var row))
        {
          var entry = _session.ModList?.Entries.ElementAtOrDefault(index - 1);
          row = new ModRow(index, entry?.Index == index ? entry : null);
          _rows[index] = row;
        }

        var wasActive = row.IsActive;
        row.Apply(progress);
        if (row.IsActive && !wasActive)
          Active.Add(row);
        else if (!row.IsActive && wasActive)
          Active.Remove(row);
        if (row.Status == ModStatus.Failed && !Failures.Contains(row))
          Failures.Add(row);
      }

      Done = _rows.Values.Count(r => r.IsFinished);
      Downloading = _rows.Values.Count(r => r.Status is ModStatus.Downloading or ModStatus.Resolving);
      Extracting = _rows.Values.Count(r => r.Status == ModStatus.Extracting);
      var speed = _rows.Values.Sum(r => r.BytesPerSecond);
      SpeedText = speed > 0 ? ByteSize.FormatSpeed(speed) : "—";
      if (Total > 0)
        Overall = Math.Clamp((double)Done / Total, 0, 1);
    }

    /// <summary>Repassa o pipeline pras filas (qualquer thread) e o registro pro LogBook.</summary>
    private sealed class QueueReporter(InstallService owner) : IInstallReporter
    {
      public void Stage(InstallStage stage, StageState state, string? detail = null) =>
        owner._pendingStages.Enqueue((stage, state, detail));

      public void Mod(ModProgress progress)
      {
        // estado final nunca é atropelado por um progresso que chegou atrasado no mesmo lote
        owner._pendingMods.AddOrUpdate(
          progress.Index,
          progress,
          (_, current) => current.Status is ModStatus.Installed or ModStatus.UpToDate or ModStatus.Failed && progress.Status is not (ModStatus.Installed or ModStatus.UpToDate or ModStatus.Failed)
            ? current
            : progress);
      }

      public void Log(LogLevel level, string message) => owner._log.Write(level, message);
    }

    // ===== Sem suspensão no meio de uma instalação de horas =====

    [Flags]
    private enum ExecutionState : uint
    {
      Continuous = 0x80000000,
      SystemRequired = 0x00000001,
    }

    [DllImport("kernel32.dll")]
    private static extern ExecutionState SetThreadExecutionState(ExecutionState state);

    private static void KeepAwake(bool on) =>
      SetThreadExecutionState(on ? ExecutionState.Continuous | ExecutionState.SystemRequired : ExecutionState.Continuous);
  }
}
