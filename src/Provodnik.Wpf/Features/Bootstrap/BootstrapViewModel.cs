using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Provodnik.Core.Bootstrap;
using Provodnik.Core.Install;
using Provodnik.Core.Text;
using Provodnik.Features.Bootstrap;
using Provodnik.Wpf.Features.Install;
using Provodnik.Wpf.Shared;
using Provodnik.Wpf.Shared.Services;

namespace Provodnik.Wpf.Features.Bootstrap
{
  /// <summary>Uma etapa da instalação do zero, na linha do tempo do Início.</summary>
  public sealed partial class BootstrapStepRow(BootstrapStep step, string title) : ObservableObject
  {
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunning), nameof(IsDone))]
    private StageState _state;

    [ObservableProperty] private string? _detail;

    /// <summary>Quanto da etapa já foi (nulo: sem medida, a barra some).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFraction))]
    private double? _fraction;

    public BootstrapStep Step { get; } = step;

    public string Title { get; } = title;

    public bool IsLast { get; init; }

    public bool IsRunning => State == StageState.Running;

    public bool IsDone => State is StageState.Done or StageState.Warning or StageState.Failed or StageState.Skipped;

    public bool HasFraction => Fraction is not null && IsRunning;
  }

  /// <summary>Um aviso da checagem da pasta (o que impede, o que só avisa, o que é bom saber).</summary>
  public sealed record BootstrapIssueRow(BootstrapIssueLevel Level, string Text)
  {
    public RowTone Tone => Level switch
    {
      BootstrapIssueLevel.Blocker => RowTone.Error,
      BootstrapIssueLevel.Warning => RowTone.Warning,
      _ => RowTone.Success,
    };
  }

  /// <summary>
  /// O Início sem GAMMA: a instalação do zero. A pessoa escolhe só onde (o disco ou uma pasta; o GAMMA e o Anomaly
  /// ficam lado a lado dentro dela), vê na hora o que a wiki do GAMMA não deixa (disco diferente, pasta do usuário,
  /// pendrive, pouco espaço) e clica uma vez. No fim a pasta vira a do GAMMA do Проводник e a instalação de sempre
  /// começa sozinha na tela de Instalação.
  /// </summary>
  public sealed partial class BootstrapViewModel : ObservableObject
  {
    private readonly BootstrapPipeline _pipeline;
    private readonly BootstrapServices _services;
    private readonly GammaSession _session;
    private readonly SettingsStore _settings;
    private readonly Navigator _navigator;
    private readonly InstallService _install;
    private readonly ToolConsole _console;
    private readonly AppOptions _options;
    private CancellationTokenSource? _cancel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Target), nameof(GammaFolder), nameof(AnomalyFolder))]
    private string _parentFolder;

    /// <summary>Um Anomaly 1.5.3 que já existe em outro lugar (nulo: o Anomaly vai do lado do GAMMA).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Target), nameof(AnomalyFolder), nameof(HasAnomalyOverride))]
    private string? _anomalyOverride;

    [ObservableProperty] private bool _defenderExclusion = true;
    [ObservableProperty] private bool _dependencies = true;
    [ObservableProperty] private string _freeSpaceText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart), nameof(IsIdle))]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private bool _isRunning;

    [ObservableProperty] private bool _isCancelling;

    /// <summary>Já rodou uma vez nesta abertura: a linha do tempo fica na tela (com o erro, se parou nele).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private bool _started;

    [ObservableProperty] private string? _headline;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private bool _hasBlocker;

    public BootstrapViewModel(
      BootstrapPipeline pipeline,
      BootstrapServices services,
      GammaSession session,
      SettingsStore settings,
      Navigator navigator,
      InstallService install,
      ToolConsole console,
      AppOptions options)
    {
      _options = options;
      _pipeline = pipeline;
      _services = services;
      _session = session;
      _settings = settings;
      _navigator = navigator;
      _install = install;
      _console = console;
      _parentFolder = settings.Current.BootstrapFolder ?? SuggestParent();
      Steps =
      [
        new(BootstrapStep.Prepare, L.T("bootstrap.step.prepare")),
        new(BootstrapStep.System, L.T("bootstrap.step.system")),
        new(BootstrapStep.Gamma, L.T("bootstrap.step.gamma")),
        new(BootstrapStep.Anomaly, L.T("bootstrap.step.anomaly")),
        new(BootstrapStep.ModOrganizer, L.T("bootstrap.step.mo2")) { IsLast = true },
      ];
      Check();
    }

    public IReadOnlyList<BootstrapStepRow> Steps { get; }

    public ObservableCollection<BootstrapIssueRow> Issues { get; } = [];

    public BootstrapTarget Target
    {
      get
      {
        var under = BootstrapTarget.Under(ParentFolder is { Length: > 0 } parent && Path.IsPathFullyQualified(parent) ? parent : SuggestParent());
        return AnomalyOverride is { } anomaly ? under with { Anomaly = anomaly } : under;
      }
    }

    public string GammaFolder => Target.Gamma;

    public string AnomalyFolder => Target.Anomaly;

    public bool HasAnomalyOverride => AnomalyOverride is not null;

    public bool CanStart => !IsRunning && !HasBlocker;

    /// <summary>A escolha da pasta aparece antes de começar e depois de parar; rodando, só a linha do tempo.</summary>
    public bool IsIdle => !IsRunning;

    partial void OnParentFolderChanged(string value) => Check();

    partial void OnAnomalyOverrideChanged(string? value) => Check();

    /// <summary>A checagem da wiki pra pasta escolhida, refeita a cada mudança.</summary>
    private void Check()
    {
      var target = Target;
      var facts = Facts(target.Gamma);
      Issues.Clear();
      foreach (var issue in target.Check(facts))
        Issues.Add(new BootstrapIssueRow(issue.Level, issue.Text));
      HasBlocker = Issues.Any(i => i.Level == BootstrapIssueLevel.Blocker);
      FreeSpaceText = facts.Drive is DriveType.NoRootDirectory or DriveType.Unknown
        ? string.Empty
        : L.T("bootstrap.free", ByteSize.Format(facts.FreeBytes), Path.GetPathRoot(target.Gamma));
    }

    private static BootstrapFacts Facts(string path)
    {
      string[] forbidden =
      [
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        Environment.GetEnvironmentVariable("OneDrive") ?? string.Empty,
        Environment.GetEnvironmentVariable("OneDriveConsumer") ?? string.Empty,
        Environment.GetEnvironmentVariable("OneDriveCommercial") ?? string.Empty,
      ];
      try
      {
        var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path))!);
        return drive.IsReady
          ? new BootstrapFacts(drive.DriveType, drive.AvailableFreeSpace, forbidden)
          : new BootstrapFacts(DriveType.NoRootDirectory, 0, forbidden);
      }
      catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
      {
        return new BootstrapFacts(DriveType.NoRootDirectory, 0, forbidden);
      }
    }

    /// <summary>A raiz do disco interno com mais espaço livre: fica D:\GAMMA e D:\Anomaly, o caminho que a wiki usa.</summary>
    private static string SuggestParent()
    {
      try
      {
        return DriveInfo.GetDrives()
          .Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
          .MaxBy(d => d.AvailableFreeSpace)?.RootDirectory.FullName ?? @"C:\";
      }
      catch (IOException)
      {
        return @"C:\";
      }
    }

    [RelayCommand]
    private void ChooseParent()
    {
      var dialog = new OpenFolderDialog { Title = L.T("bootstrap.chooseParent.title"), InitialDirectory = Directory.Exists(ParentFolder) ? ParentFolder : null };
      if (dialog.ShowDialog() == true)
        ParentFolder = dialog.FolderName;
    }

    [RelayCommand]
    private async Task ChooseAnomalyAsync()
    {
      var dialog = new OpenFolderDialog { Title = L.T("bootstrap.chooseAnomaly.title") };
      if (dialog.ShowDialog() != true)
        return;
      if (!(Target with { Anomaly = dialog.FolderName }).AnomalyReady)
      {
        await DialogService.WarningAsync(L.T("bootstrap.chooseAnomaly.wrongTitle"), L.T("bootstrap.chooseAnomaly.wrong", dialog.FolderName));
        return;
      }
      AnomalyOverride = dialog.FolderName;
    }

    [RelayCommand]
    private void ClearAnomaly() => AnomalyOverride = null;

    /// <summary>O GAMMA já existe em outra pasta: só apontar pra ela, como antes.</summary>
    [RelayCommand]
    private async Task ChooseExistingAsync()
    {
      var dialog = new OpenFolderDialog { Title = L.T("home.chooseFolder.title") };
      if (dialog.ShowDialog() != true)
        return;
      if (_session.SetRoot(dialog.FolderName) is { } error)
        await DialogService.WarningAsync(L.T("home.chooseFolder.wrong"), error);
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
      Check();
      if (HasBlocker)
        return;

      var target = Target;
      _settings.Update(s => s with { BootstrapFolder = ParentFolder });
      foreach (var step in Steps)
      {
        step.State = StageState.Pending;
        step.Detail = null;
        step.Fraction = null;
      }
      Started = true;
      IsRunning = true;
      Headline = L.T("bootstrap.headline.running");
      _services.Target = target;
      _cancel = new CancellationTokenSource();
      KeepAwake(true);
      _console.RecordTo(Path.Combine(AppPaths.Root, $"bootstrap-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log"));
      _console.Write("provodnik", L.T("bootstrap.console.started", target.Gamma, target.Anomaly));

      BootstrapResult result;
      try
      {
        var options = new BootstrapOptions(DefenderExclusion, Dependencies, Environment.ProcessPath);
        var progress = new Progress<BootstrapProgress>(Apply);
        var token = _cancel.Token;
        result = await Task.Run(() => _pipeline.RunAsync(target, options, progress, token));
      }
      catch (OperationCanceledException)
      {
        Headline = L.T("bootstrap.headline.cancelled");
        return;
      }
      finally
      {
        _console.RecordTo(null);
        KeepAwake(false);
        _cancel.Dispose();
        _cancel = null;
        IsRunning = false;
        IsCancelling = false;
        Check();
      }

      if (!result.Succeeded)
      {
        Headline = L.T("bootstrap.headline.failed");
        return;
      }

      // a pasta nova vira a do Проводник e a instalação de sempre começa
      if (_session.SetRoot(target.Gamma) is { } error)
      {
        Headline = error;
        return;
      }
      Headline = L.T("bootstrap.headline.done");

      // o exe baixado reabre pela cópia que ficou no GAMMA (onde o Проводник mora, como o launcher original), que
      // apaga o baixado e segue com a instalação. No --do-zero não: a cópia pegaria a trava do Проводник de sempre
      if (Environment.ProcessPath is { } running && !_options.FromScratch)
      {
        var copy = Path.Combine(target.Gamma, Path.GetFileName(running));
        if (File.Exists(copy) && !string.Equals(Path.GetFullPath(copy), Path.GetFullPath(running), StringComparison.OrdinalIgnoreCase))
        {
          ((App)System.Windows.Application.Current).ReopenAs(copy, AppOptions.ContinueFlag, running);
          return;
        }
      }

      ToastService.Success(L.T("bootstrap.toast.done"), L.T("bootstrap.toast.title"));
      _navigator.Open(Tab.Install);
      _ = _install.InstallAsync(forceExtract: false);
    }

    [RelayCommand]
    private void Cancel()
    {
      if (_cancel is null)
        return;
      IsCancelling = true;
      Headline = L.T("bootstrap.headline.cancelling");
      _cancel.Cancel();
    }

    private void Apply(BootstrapProgress progress)
    {
      var row = Steps.First(s => s.Step == progress.Step);
      row.State = progress.State;
      if (progress.Detail is not null || progress.State != StageState.Running)
        row.Detail = progress.Detail;
      row.Fraction = progress.State == StageState.Running ? progress.Fraction : null;
    }

    // ===== Sem suspensão no meio de um download de 20 GB =====

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
