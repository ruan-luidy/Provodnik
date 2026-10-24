using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Provodnik.Wpf.Features.Home;
using Provodnik.Wpf.Features.Install;
using Provodnik.Wpf.Features.Logs;
using Provodnik.Wpf.Features.Mods;
using Provodnik.Wpf.Features.Settings;
using Provodnik.Wpf.Shared;
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
      ElevationService elevation)
    {
      _settings = settings;
      _session = session;
      Install = installService;
      IsElevated = elevation.IsElevated;
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

    public string Title => _session.Layout is null ? "Проводник" : $"Проводник · {_session.Metadata.Name}";

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
    private async Task OpenSettingsAsync()
    {
      if (await DialogService.ShowAsync(new SettingsView { DataContext = new SettingsViewModel(_settings, _session) }))
        ToastService.Success("Configurações salvas");
    }
  }
}
