using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Provodnik.Features.Launch;
using Provodnik.Features.Mods;
using Provodnik.Features.Update;
using Provodnik.Features.Verify;
using Provodnik.Wpf.Features.Install;
using Provodnik.Wpf.Shared;
using Provodnik.Wpf.Shared.Services;

namespace Provodnik.Wpf.Features.Home
{
  /// <summary>
  /// Início: o que o topo do launcher original mostrava (definição e lista em dia ou não), mais o que faltava saber
  /// (quantos addons estão de fato instalados, quantos arquivos faltam, se a última instalação parou no meio) e o
  /// Jogar direto no executável escolhido.
  /// </summary>
  public sealed partial class HomeViewModel : PageViewModel
  {
    private readonly GammaSession _session;
    private readonly SettingsStore _settings;
    private readonly UpdateChecker _updates;
    private readonly Navigator _navigator;
    private readonly ElevationService _elevation;

    [ObservableProperty] private string _statusTitle = "Conferindo...";
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private RowTone _tone;
    [ObservableProperty] private bool _isChecking;
    [ObservableProperty] private string _versionText = "—";
    [ObservableProperty] private string _onlineVersionText = "—";
    [ObservableProperty] private string _modListText = "—";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModsFraction), nameof(ModsComplete))]
    private int _modsTotal;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModsFraction), nameof(ModsComplete))]
    private int _modsInstalled;
    [ObservableProperty] private int _archivesPresent;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveFraction), nameof(ActiveComplete))]
    private int _activeFound;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveFraction), nameof(ActiveComplete))]
    private int _activeExpected;
    [ObservableProperty] private int _archivesMissing;
    [ObservableProperty] private string _lastInstallText = "ainda não";
    [ObservableProperty] private IReadOnlyList<string> _executables = [];
    [ObservableProperty] private string? _selectedExecutable;

    public HomeViewModel(GammaSession session, SettingsStore settings, UpdateChecker updates, Navigator navigator, InstallService install, ElevationService elevation)
    {
      _session = session;
      _settings = settings;
      _updates = updates;
      _navigator = navigator;
      _elevation = elevation;
      Install = install;
      session.Reloaded += (_, _) =>
      {
        Stale = true;
        OnPropertyChanged(string.Empty);
        RefreshLocal();
      };
      install.Finished += (_, _) => _ = RunAsync(LoadAsync);
      RefreshLocal();
    }

    public InstallService Install { get; }

    /// <summary>A barrinha dos cartões de proporção (verde quando completa).</summary>
    public double ModsFraction => ModsTotal > 0 ? (double)ModsInstalled / ModsTotal : 0;

    public bool ModsComplete => ModsTotal > 0 && ModsInstalled >= ModsTotal;

    public double ActiveFraction => ActiveExpected > 0 ? (double)ActiveFound / ActiveExpected : 0;

    public bool ActiveComplete => ActiveExpected > 0 && ActiveFound >= ActiveExpected;

    public bool HasGamma => _session.Layout is not null;

    public string GammaRoot => _session.Layout?.Root ?? "—";

    public string AnomalyPath => _session.Mo2.GamePath ?? "o MO2 ainda não sabe (abra ele uma vez)";

    public string ProfileName => _session.Mo2.SelectedProfile ?? _session.Metadata.FolderName;

    public bool IsPortable => _session.IsPortable;

    partial void OnSelectedExecutableChanged(string? value)
    {
      if (value is not null && value != _settings.Current.Executable)
        _settings.Update(s => s with { Executable = value });
    }

    protected override async Task LoadAsync()
    {
      RefreshLocal();
      if (_session.Layout is not { } layout)
        return;

      // os números do disco por trás (varrer 473 pastas leva um instante)
      var list = _session.ModList;
      var state = _session.State;
      var metadata = _session.Metadata;
      if (list is not null)
      {
        var items = await Task.Run(() => ModInventory.Scan(layout, list, state, ActiveModsCheck.Disabled(layout, metadata)));
        // o vazio de mod que o GAMMA deixa desligado não conta como faltando
        ModsTotal = items.Count(i => i.FolderHasContent || i.NeedsInstall);
        ModsInstalled = items.Count(i => i.FolderHasContent);
        ArchivesPresent = items.Where(i => i.ArchivePresent).Select(i => i.Entry.ArchiveName).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        ArchivesMissing = items.Where(i => !i.ArchivePresent).Select(i => i.Entry.ArchiveName).Distinct(StringComparer.OrdinalIgnoreCase).Count();
      }

      // a "regra dos 5xx mods": quantos o modlist do perfil liga e quantos têm pasta de verdade
      var active = await Task.Run(() => ActiveModsCheck.Run(layout, metadata));
      ActiveExpected = active.Expected;
      ActiveFound = active.Found;

      IsChecking = true;
      try
      {
        var status = await _updates.CheckAsync(layout, _settings.Current.ModListKey, CancellationToken.None);
        OnlineVersionText = status.OnlineVersion ?? "sem resposta";
        ModListText = status.ModListChanged switch
        {
          true => "mudou no site",
          false => "em dia",
          null => "sem resposta",
        };
        Summarize(status);
      }
      finally
      {
        IsChecking = false;
      }
    }

    /// <summary>O que dá pra saber sem internet: versão instalada, executáveis e a última instalação.</summary>
    private void RefreshLocal()
    {
      VersionText = _session.InstalledVersion ?? "não instalada";
      Executables = _session.Mo2.GameExecutables.Select(e => e.Title).ToList();
      SelectedExecutable = Executables.Contains(_settings.Current.Executable ?? string.Empty)
        ? _settings.Current.Executable
        : Executables.FirstOrDefault(e => e.Contains("DX11-AVX", StringComparison.OrdinalIgnoreCase)) ?? Executables.FirstOrDefault();
      LastInstallText = _session.State.LastCompleted is { } done ? done.ToString("dd/MM/yyyy HH:mm") : "ainda não";
      if (!HasGamma)
      {
        StatusTitle = "GAMMA não encontrado";
        StatusText = "Escolha a pasta do GAMMA (a que tem o ModOrganizer.exe) ou coloque o Проводник dentro dela.";
        Tone = RowTone.Error;
      }
    }

    private void Summarize(UpdateStatus status)
    {
      if (_session.State.IsIncomplete)
      {
        (StatusTitle, StatusText, Tone) = ("Instalação incompleta", "A última instalação não chegou até o fim. Rode de novo: ela continua de onde parou.", RowTone.Warning);
      }
      else if (_session.InstalledVersion is null)
      {
        (StatusTitle, StatusText, Tone) = ("GAMMA não instalado", "Clique em Instalar pra baixar e montar o modpack.", RowTone.Warning);
      }
      else if (status.HasUpdate)
      {
        (StatusTitle, StatusText, Tone) = ("Atualização disponível", status.DefinitionOutdated ? $"A definição foi pra {status.OnlineVersion}." : "A lista de mods mudou no site.", RowTone.Warning);
      }
      else if (ActiveFound < ActiveExpected || ModsInstalled < ModsTotal)
      {
        (StatusTitle, StatusText, Tone) = ("Faltam addons", $"O MO2 tem {ActiveFound} de {ActiveExpected} mods ativos com pasta. Instale de novo pra completar (ele continua de onde parou).", RowTone.Warning);
      }
      else
      {
        (StatusTitle, StatusText, Tone) = ("Tudo em dia", $"{ActiveExpected} mods ativos no MO2, como a lista pede. É só jogar.", RowTone.Success);
      }

      if (status.Error is { } error)
        StatusText += $" (sem resposta de: {error})";
    }

    [RelayCommand]
    private async Task PlayAsync()
    {
      if (_session.Layout is not { } layout)
        return;
      if (Install.IsRunning)
      {
        ToastService.Warning("Espere a instalação terminar.");
        return;
      }
      try
      {
        GameLauncher.Play(layout, SelectedExecutable, _elevation.Unelevated);
        ToastService.Info(SelectedExecutable is null ? "Abrindo o Mod Organizer" : $"Abrindo {SelectedExecutable} pelo MO2", "Boa caçada, stalker");
      }
      catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception)
      {
        await DialogService.ErrorAsync("Não abriu", ex.Message);
      }
    }

    [RelayCommand]
    private void OpenModOrganizer()
    {
      if (_session.Layout is { } layout)
        GameLauncher.OpenModOrganizer(layout, _elevation.Unelevated);
    }

    [RelayCommand]
    private void GoInstall()
    {
      _navigator.Open(Tab.Install);
      if (!Install.IsRunning)
        _ = Install.InstallAsync(forceExtract: false);
    }

    [RelayCommand]
    private Task CheckAsync() => RunAsync(LoadAsync);

    [RelayCommand]
    private async Task ChooseFolderAsync()
    {
      var dialog = new OpenFolderDialog { Title = "A pasta do GAMMA (a que tem o ModOrganizer.exe)" };
      if (dialog.ShowDialog() != true)
        return;
      if (_session.SetRoot(dialog.FolderName) is { } error)
        await DialogService.WarningAsync("Pasta errada", error);
    }

    [RelayCommand]
    private void OpenFolder(string? which)
    {
      if (_session.Layout is not { } layout)
        return;
      FolderOpener.Open(which switch
      {
        "anomaly" => _session.Mo2.GamePath,
        "downloads" => layout.Downloads,
        "mods" => layout.Mods,
        "profile" => Path.Combine(layout.Profiles, ProfileName),
        _ => layout.Root,
      });
    }
  }
}
