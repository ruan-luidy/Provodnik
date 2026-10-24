using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Provodnik.Core.Desktop;
using Provodnik.Core.Text;
using Provodnik.Features.Verify;
using Provodnik.Wpf.Shared;
using Provodnik.Wpf.Shared.Services;

namespace Provodnik.Wpf.Features.Install
{
  /// <summary>
  /// A tela de instalação: as etapas à esquerda; à direita o total, as falhas (com o que fazer), o que está trabalhando
  /// agora (ou, parado, a checagem antes de instalar) e o console das ferramentas. Tudo da rodada vem do
  /// <see cref="InstallService"/>; aqui ficam os comandos, a checagem e os consertos.
  /// </summary>
  public sealed partial class InstallViewModel : PageViewModel
  {
    private readonly GammaSession _session;
    private readonly SettingsStore _settings;
    private readonly ISystemInfo _system;
    private readonly ElevationService _elevation;
    private ConsoleWindow? _consoleWindow;

    /// <summary>O "Force zip extraction" do original: reextrai todo mod mesmo sem mudança (conserta instalação quebrada).</summary>
    [ObservableProperty] private bool _forceExtract;

    [ObservableProperty] private IReadOnlyList<PreflightRow> _checks = [];

    [ObservableProperty] private bool _showConsole;

    public InstallViewModel(InstallService service, GammaSession session, SettingsStore settings, ToolConsole console, ISystemInfo system, ElevationService elevation)
    {
      Service = service;
      Console = console;
      _session = session;
      _settings = settings;
      _system = system;
      _elevation = elevation;
      _showConsole = settings.Current.ShowConsole;
      service.PropertyChanged += (_, e) =>
      {
        if (e.PropertyName is nameof(InstallService.IsRunning) or nameof(InstallService.LastReport))
        {
          StartCommand.NotifyCanExecuteChanged();
          RetryCommand.NotifyCanExecuteChanged();
          OnPropertyChanged(nameof(HasFailures));
        }
      };
      service.Failures.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasFailures));
      service.Finished += (_, _) => _ = RunAsync(LoadAsync);
      session.Reloaded += (_, _) => Stale = true;
    }

    public InstallService Service { get; }

    public ToolConsole Console { get; }

    public bool HasFailures => Service.Failures.Count > 0 || Service.LastReport?.Failures.Count > 0;

    /// <summary>A checagem barrou: o Instalar avisa antes de tentar.</summary>
    public bool HasBlockingCheck => Checks.Any(c => c.Check.Level == CheckLevel.Failed);

    partial void OnChecksChanged(IReadOnlyList<PreflightRow> value) => OnPropertyChanged(nameof(HasBlockingCheck));

    partial void OnShowConsoleChanged(bool value) => _settings.Update(s => s with { ShowConsole = value });

    /// <summary>A checagem roda toda vez que a tela aparece parada (o MO2 pode ter sido aberto ou fechado no meio).</summary>
    protected override async Task LoadAsync()
    {
      Stale = true;
      if (_session.Layout is not { } layout)
      {
        Checks = [];
        return;
      }
      var checks = await Task.Run(() => Preflight.Run(layout, _system));
      Checks = checks.Select(c => new PreflightRow(c)).ToList();
    }

    private bool CanStart => !Service.IsRunning && _session.Layout is not null;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
      await LoadAsync();
      if (Checks.FirstOrDefault(c => c.Check.Level == CheckLevel.Failed) is { } blocked)
      {
        await DialogService.WarningAsync(blocked.Title, blocked.Message + (blocked.HasFix ? "\n\n" + L.T("install.blocked.useFix", blocked.FixText) : string.Empty));
        return;
      }
      await Service.InstallAsync(ForceExtract);
      ForceExtract = false;
    }

    private bool CanRetry => !Service.IsRunning && Service.LastReport is { Failures.Count: > 0 };

    [RelayCommand(CanExecute = nameof(CanRetry))]
    private Task RetryAsync() => Service.RetryFailedAsync();

    [RelayCommand]
    private void Cancel() => Service.Cancel();

    [RelayCommand]
    private Task CheckAgainAsync() => RunAsync(LoadAsync);

    [RelayCommand]
    private async Task FixAsync(PreflightRow? row)
    {
      switch (row?.Fix)
      {
        case CheckFix.RunAsAdmin:
          if (!_elevation.RestartAsAdmin())
            ToastService.Warning(L.T("install.fix.adminRefused"));
          break;
        case CheckFix.EnableLongPaths:
          if (await _elevation.EnableLongPathsAsync())
            ToastService.Success(L.T("install.fix.longPathsOn"));
          else
            ToastService.Warning(L.T("install.fix.longPathsRefused"));
          await RunAsync(LoadAsync);
          break;
      }
    }

    /// <summary>O console numa janela à parte, como o cmd do launcher original.</summary>
    [RelayCommand]
    private void PopOutConsole()
    {
      if (_consoleWindow is { IsLoaded: true })
      {
        _consoleWindow.Activate();
        return;
      }
      _consoleWindow = new ConsoleWindow(Console) { Owner = Application.Current.MainWindow };
      _consoleWindow.Closed += (_, _) => _consoleWindow = null;
      _consoleWindow.Show();
    }

    [RelayCommand]
    private void ClearConsole() => Console.Clear();

    [RelayCommand]
    private void OpenLog()
    {
      if (Service.LastLogFile is { } file && File.Exists(file))
        Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
      else if (_session.Layout is { } layout && Directory.Exists(layout.LogsDir))
        Process.Start(new ProcessStartInfo(layout.LogsDir) { UseShellExecute = true });
    }
  }
}
