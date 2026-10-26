using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Provodnik.Core.Text;
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
    private readonly SettingsStore _settings;

    [ObservableProperty] private ObservableCollection<ModItemRow> _items = [];
    [ObservableProperty] private ModItemRow? _selected;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtitle))]
    private int _installed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtitle))]
    private int _missing;
    [ObservableProperty] private bool _onlyMissing;

    public ModsViewModel(GammaSession session, InstallService install, Navigator navigator, ElevationService elevation, SettingsStore settings)
    {
      _settings = settings;
      _session = session;
      _navigator = navigator;
      _elevation = elevation;
      Install = install;
      session.Reloaded += (_, _) => Stale = true;
      install.Finished += (_, _) => _ = RunAsync(LoadAsync);
    }

    public InstallService Install { get; }

    public string Subtitle => L.T("mods.subtitle", Installed, Missing);

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
    private async Task OpenModOrganizerAsync()
    {
      if (_session.Layout is not { } layout)
        return;
      try
      {
        GameLauncher.OpenModOrganizer(layout, _settings.Current.Profile ?? _session.Metadata.FolderName, _elevation.Unelevated);
      }
      catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception)
      {
        await DialogService.ErrorAsync(L.T("home.play.failed"), ex.Message);
      }
    }

    /// <summary>Apaga de downloads\ as versões velhas que nada mais usa (<see cref="DownloadsCleaner"/>), depois de mostrar quais.</summary>
    [RelayCommand]
    private async Task FreeSpaceAsync()
    {
      if (_session.Layout is not { } layout || _session.ModList is not { } list)
        return;
      if (Install.IsRunning)
      {
        ToastService.Warning(L.T("mods.freeSpace.busy"));
        return;
      }

      var old = await Task.Run(() => DownloadsCleaner.Find(layout, list));
      if (old.Count == 0)
      {
        await DialogService.InfoAsync(L.T("mods.freeSpace.title"), L.T("mods.freeSpace.nothing"));
        return;
      }
      var details = string.Join("\n", old.Select(o => $"{o.Name}  ({ByteSize.Format(o.Size)})"));
      if (!await DialogService.ConfirmAsync(L.T("mods.freeSpace.title"), L.T("mods.freeSpace.confirm", old.Count, ByteSize.Format(old.Sum(o => o.Size))), L.T("mods.freeSpace.delete"), details: details))
        return;

      var result = await Task.Run(() => DownloadsCleaner.Delete(old));
      if (result.Failed.Count == 0)
        ToastService.Success(L.T("mods.freeSpace.done", result.Deleted, ByteSize.Format(result.Freed)));
      else
        await DialogService.WarningAsync(L.T("mods.freeSpace.title"), L.T("mods.freeSpace.partial", result.Deleted, ByteSize.Format(result.Freed), result.Failed.Count), string.Join("\n", result.Failed));
      await RunAsync(LoadAsync);
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
      if (!await DialogService.ConfirmAsync(L.T("mods.fill.title"), L.T("mods.fill.question", missing.Count), L.T("mods.fill.confirm"), kind: DialogKind.Info))
        return;
      _navigator.Open(Tab.Install);
      await Install.ReinstallAsync(missing);
    }
  }
}
