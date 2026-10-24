using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Provodnik.Features.Launch;
using Provodnik.Wpf.Shared;
using Provodnik.Wpf.Shared.Services;

namespace Provodnik.Wpf.Features.Install
{
  /// <summary>
  /// A tela de instalação: as etapas à esquerda, à direita o total, o que está trabalhando agora e as falhas com o
  /// "tentar de novo". Tudo vem do <see cref="InstallService"/>; aqui ficam os comandos e as opções da rodada.
  /// </summary>
  public sealed partial class InstallViewModel : PageViewModel
  {
    private readonly GammaSession _session;

    /// <summary>O "Force zip extraction" do original: reextrai todo mod mesmo sem mudança (conserta instalação quebrada).</summary>
    [ObservableProperty] private bool _forceExtract;

    public InstallViewModel(InstallService service, GammaSession session)
    {
      Service = service;
      _session = session;
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
    }

    public InstallService Service { get; }

    public bool HasFailures => Service.Failures.Count > 0 || Service.LastReport?.Failures.Count > 0;

    private bool CanStart => !Service.IsRunning && _session.Layout is not null;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
      if (GameLauncher.IsModOrganizerRunning())
      {
        await DialogService.WarningAsync("Feche o Mod Organizer", "O MO2 está aberto e segura os arquivos dos mods. Feche ele (e o jogo) e comece de novo.");
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
    private void OpenLog()
    {
      if (Service.LastLogFile is { } file && File.Exists(file))
        Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
      else if (_session.Layout is { } layout && Directory.Exists(layout.LogsDir))
        Process.Start(new ProcessStartInfo(layout.LogsDir) { UseShellExecute = true });
    }
  }
}
