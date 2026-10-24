using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Provodnik.Features.Launch;
using Provodnik.Features.Mods;
using Provodnik.Features.Verify;
using Provodnik.Wpf.Features.Install;
using Provodnik.Wpf.Shared;
using Provodnik.Wpf.Shared.Controls;
using Provodnik.Wpf.Shared.Services;

namespace Provodnik.Wpf.Features.Mods
{
  /// <summary>
  /// Os 473 addons do mods.txt com o que tem de cada um no disco. É aqui que se vê o que o original escondia: pasta
  /// de mod vazia (só com o meta.ini), arquivo que nunca baixou. Dá pra reinstalar um mod só ou todos os vazios.
  /// </summary>
  public sealed partial class ModsViewModel : PageViewModel
  {
    private readonly GammaSession _session;
    private readonly Navigator _navigator;
    private readonly ElevationService _elevation;

    [ObservableProperty] private ObservableCollection<ModItemRow> _items = [];
    [ObservableProperty] private ModItemRow? _selected;
    [ObservableProperty] private int _installed;
    [ObservableProperty] private int _missing;
    [ObservableProperty] private bool _onlyMissing;

    public ModsViewModel(GammaSession session, InstallService install, Navigator navigator, ElevationService elevation)
    {
      _session = session;
      _navigator = navigator;
      _elevation = elevation;
      Install = install;
      session.Reloaded += (_, _) => Stale = true;
      install.Finished += (_, _) => _ = RunAsync(LoadAsync);
    }

    public InstallService Install { get; }

    private IReadOnlyList<ModItemRow> _all = [];

    partial void OnOnlyMissingChanged(bool value) => ApplyFilter();

    protected override async Task LoadAsync()
    {
      if (_session.Layout is not { } layout || _session.ModList is not { } list)
      {
        _all = [];
        ApplyFilter();
        return;
      }

      var state = _session.State;
      var metadata = _session.Metadata;
      var items = await Task.Run(() => ModInventory.Scan(layout, list, state, ActiveModsCheck.Disabled(layout, metadata)));
      _all = items.Select(i => new ModItemRow(i, Path.Combine(layout.Mods, i.Entry.FolderName))).ToList();
      Installed = _all.Count(r => r.Item.FolderHasContent);
      Missing = _all.Count(r => r.IsMissing);
      ApplyFilter();
    }

    private void ApplyFilter() =>
      Items = new ObservableCollection<ModItemRow>(OnlyMissing ? _all.Where(r => r.IsMissing) : _all);

    /// <summary>Ligar, desligar, ordenar e buscar é com o MO2: esta tela só diz se o addon foi baixado e extraído.</summary>
    [RelayCommand]
    private void OpenModOrganizer()
    {
      if (_session.Layout is { } layout)
        GameLauncher.OpenModOrganizer(layout, _elevation.Unelevated);
    }

    [RelayCommand]
    private void OpenPage(ModItemRow? row) => FolderOpener.Browse(row?.WebPage);

    [RelayCommand]
    private void OpenFolder(ModItemRow? row) => FolderOpener.Open(row?.FolderPath);

    [RelayCommand]
    private async Task ReinstallAsync(ModItemRow? row)
    {
      if (row is null || Install.IsRunning)
        return;
      _navigator.Open(Tab.Install);
      await Install.ReinstallAsync([row.Index]);
    }

    [RelayCommand]
    private async Task ReinstallMissingAsync()
    {
      var missing = _all.Where(r => r.IsMissing).Select(r => r.Index).ToList();
      if (missing.Count == 0 || Install.IsRunning)
        return;
      if (!await DialogService.ConfirmAsync("Completar os addons", $"Baixar (se faltar) e extrair os {missing.Count} addons com a pasta vazia?", "Completar", kind: DialogKind.Info))
        return;
      _navigator.Open(Tab.Install);
      await Install.ReinstallAsync(missing);
    }
  }
}
