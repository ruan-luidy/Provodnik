using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Provodnik.Core.Text;
using Provodnik.Wpf.Features.About;
using Provodnik.Wpf.Features.AppUpdate;
using Provodnik.Wpf.Features.Home;
using Provodnik.Wpf.Features.Install;
using Provodnik.Wpf.Features.Logs;
using Provodnik.Wpf.Features.Mods;
using Provodnik.Wpf.Features.Settings;
using Provodnik.Wpf.Shared;
using Provodnik.Wpf.Shared.Controls;
using Provodnik.Wpf.Shared.Services;

namespace Provodnik.Wpf.Shell
{
  /// <summary>
  /// O shell no molde do Windfinder: o painel lateral com as telas (cada uma é um <see cref="PageViewModel"/> que
  /// vive o app inteiro) e a instalação rodando, que aparece na barra de título e na barra de tarefas de qualquer tela.
  /// </summary>
  public sealed partial class MainViewModel : ObservableObject
  {
    private readonly Dictionary<Tab, PageViewModel> _pages;
    private readonly SettingsStore _settings;
    private readonly GammaSession _session;
    private readonly AppUpdateService _update;

    [ObservableProperty] private Tab _tab;
    [ObservableProperty] private PageViewModel _current;

    /// <summary>O painel lateral recolhido: só os ícones.</summary>
    [ObservableProperty] private bool _paneCompact;

    public MainViewModel(
      HomeViewModel home,
      InstallViewModel install,
      ModsViewModel mods,
      LogsViewModel logs,
      InstallService installService,
      GammaSession session,
      SettingsStore settings,
      Navigator navigator,
      ElevationService elevation,
      AppOptions options,
      AppUpdateService update)
    {
      _settings = settings;
      _session = session;
      _update = update;
      Install = installService;
      IsElevated = elevation.IsElevated;
      IsDemo = options.Demo;
      _pages = new()
      {
        [Tab.Home] = home,
        [Tab.Install] = install,
        [Tab.Mods] = mods,
        [Tab.Logs] = logs,
      };
      // abre na aba em que fechou; com instalação pela metade, no Início (que avisa)
      _tab = Enum.TryParse<Tab>(settings.Current.Tab, out var tab) ? tab : Tab.Home;
      _paneCompact = settings.Current.PaneCompact;
      _current = _pages[_tab];
      navigator.PageRequested += page => Tab = page;
    }

    public InstallService Install { get; }

    /// <summary>Rodando como administrador: a barra de título avisa (o MO2 e o jogo abrem sem).</summary>
    public bool IsElevated { get; }

    /// <summary>A simulação (--demo): a barra de título avisa que nada ali é de verdade.</summary>
    public bool IsDemo { get; }

    public string Title => _session.Layout is null ? L.T("shell.appName") : $"{L.T("shell.appName")} · {_session.Metadata.Name}";

    public async Task StartAsync() => await Current.ShowAsync();

    partial void OnTabChanged(Tab value)
    {
      _settings.Update(s => s with { Tab = value.ToString() });
      Current = _pages[value];
      _ = Current.ShowAsync();
    }

    [RelayCommand]
    private void TogglePane() => PaneCompact = !PaneCompact;

    partial void OnPaneCompactChanged(bool value) => _settings.Update(s => s with { PaneCompact = value });

    [RelayCommand]
    private void GoInstall() => Tab = Tab.Install;

    [RelayCommand]
    private Task OpenAboutAsync() => DialogService.ShowAsync(new AboutView { DataContext = new AboutViewModel(_update, _session) });

    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
      var settings = new SettingsViewModel(_settings, _session);
      if (!await DialogService.ShowAsync(new SettingsView { DataContext = settings }))
        return;
      if (!settings.LanguageChanged)
      {
        ToastService.Success(L.T("settings.saved"));
        return;
      }
      // o idioma vale da partida em diante: reabre, a não ser no meio de uma instalação
      if (Install.IsRunning)
      {
        ToastService.Info(L.T("settings.language.later"));
        return;
      }
      if (await DialogService.ConfirmAsync(L.T("settings.language.title"), L.T("settings.language.restart"), L.T("settings.language.reopen"), L.T("settings.language.notNow"), DialogKind.Info))
        ((App)Application.Current).Restart();
    }
  }
}
