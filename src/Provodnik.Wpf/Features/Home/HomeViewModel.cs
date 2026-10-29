using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Provodnik.Core.Text;
using Provodnik.Core.Desktop;
using Provodnik.Features.Launch;
using Provodnik.Features.ModOrganizer;
using Provodnik.Features.Mods;
using Provodnik.Features.Update;
using Provodnik.Features.Verify;
using Provodnik.Wpf.Features.Bootstrap;
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
    private readonly ISystemInfo _system;

    [ObservableProperty] private string _statusTitle = L.T("home.status.checking");
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InstallSuggested))]
    private RowTone _tone;
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
    [ObservableProperty] private string _lastInstallText = L.T("home.stat.last.never");
    [ObservableProperty] private string _lastInstallCaption = L.T("home.stat.last.caption.never");
    [ObservableProperty] private IReadOnlyList<string> _executables = [];
    [ObservableProperty] private string? _selectedExecutable;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProfileChoice))]
    private IReadOnlyList<string> _profiles = [];
    [ObservableProperty] private string? _selectedProfile;

    public HomeViewModel(GammaSession session, SettingsStore settings, UpdateChecker updates, Navigator navigator, InstallService install, ElevationService elevation, ISystemInfo system, BootstrapViewModel bootstrap)
    {
      Bootstrap = bootstrap;
      _system = system;
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

    /// <summary>Sem GAMMA: a instalação do zero.</summary>
    public BootstrapViewModel Bootstrap { get; }

    /// <summary>A barrinha dos cartões de proporção (verde quando completa).</summary>
    public double ModsFraction => ModsTotal > 0 ? (double)ModsInstalled / ModsTotal : 0;

    public bool ModsComplete => ModsTotal > 0 && ModsInstalled >= ModsTotal;

    public double ActiveFraction => ActiveExpected > 0 ? (double)ActiveFound / ActiveExpected : 0;

    public bool ActiveComplete => ActiveExpected > 0 && ActiveFound >= ActiveExpected;

    public bool HasGamma => _session.Layout is not null;

    public string GammaRoot => _session.Layout?.Root ?? "—";

    public string AnomalyPath => _session.Mo2.GamePath ?? L.T("home.folders.anomalyUnknown");

    public string ProfileName => _session.Mo2.SelectedProfile ?? _session.Metadata.FolderName;

    public bool IsPortable => _session.IsPortable;

    /// <summary>Tem o que instalar (atualização, instalação pela metade, mod faltando): o Instalar do topo fica azul.</summary>
    public bool InstallSuggested => Tone == RowTone.Warning;

    /// <summary>A escolha de perfil só aparece com mais de um (quase todo mundo só tem o do GAMMA).</summary>
    public bool HasProfileChoice => Profiles.Count > 1;

    partial void OnSelectedExecutableChanged(string? value)
    {
      if (value is not null && value != _settings.Current.Executable)
        _settings.Update(s => s with { Executable = value });
    }

    partial void OnSelectedProfileChanged(string? value)
    {
      if (value is not null && value != _settings.Current.Profile)
        _settings.Update(s => s with { Profile = value });
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
        OnlineVersionText = status.OnlineVersion ?? L.T("home.noAnswer");
        ModListText = status.ModListChanged switch
        {
          true => L.T("home.modList.changed"),
          false => L.T("home.modList.upToDate"),
          null => L.T("home.noAnswer"),
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
      VersionText = _session.InstalledVersion ?? L.T("home.version.notInstalled");
      Executables = _session.Mo2.GameExecutables.Select(e => e.Title).ToList();
      SelectedExecutable = Executables.Contains(_settings.Current.Executable ?? string.Empty)
        ? _settings.Current.Executable
        : Executables.FirstOrDefault(e => e.Contains("DX11-AVX", StringComparison.OrdinalIgnoreCase)) ?? Executables.FirstOrDefault();
      // o salvo, senão o do GAMMA (o que a instalação monta), senão o último que o MO2 usou
      Profiles = _session.Layout is { } layout ? GameLauncher.Profiles(layout, _session.Metadata.FolderName) : [];
      SelectedProfile = Profiles.FirstOrDefault(p => p == _settings.Current.Profile)
        ?? Profiles.FirstOrDefault(p => p == _session.Metadata.FolderName)
        ?? Profiles.FirstOrDefault(p => p == _session.Mo2.SelectedProfile)
        ?? Profiles.FirstOrDefault();
      LastInstallText = _session.State.LastCompleted is { } done ? L.T("home.stat.last.date", done) : L.T("home.stat.last.never");
      // a legenda diz o que a data quer dizer; sem nenhuma completa, se já tentou ou nem começou
      LastInstallCaption = L.T(_session.State.LastCompleted is not null ? "home.stat.last.caption"
        : _session.State.LastStarted is not null ? "home.stat.last.caption.failed" : "home.stat.last.caption.never");
      if (!HasGamma)
      {
        StatusTitle = L.T("home.status.notFound.title");
        StatusText = L.T("home.status.notFound.text");
        Tone = RowTone.Error;
      }
    }

    private void Summarize(UpdateStatus status)
    {
      if (_session.State.IsIncomplete)
      {
        (StatusTitle, StatusText, Tone) = (L.T("home.status.incomplete.title"), L.T("home.status.incomplete.text"), RowTone.Warning);
      }
      else if (_session.InstalledVersion is null)
      {
        (StatusTitle, StatusText, Tone) = (L.T("home.status.notInstalled.title"), L.T("home.status.notInstalled.text"), RowTone.Warning);
      }
      else if (status.HasUpdate)
      {
        (StatusTitle, StatusText, Tone) = (L.T("home.status.update.title"), status.DefinitionOutdated ? L.T("home.status.update.definition", status.OnlineVersion) : L.T("home.status.update.modList"), RowTone.Warning);
      }
      else if (ActiveFound < ActiveExpected || ModsInstalled < ModsTotal)
      {
        (StatusTitle, StatusText, Tone) = (L.T("home.status.missing.title"), L.T("home.status.missing.text", ActiveFound, ActiveExpected), RowTone.Warning);
      }
      else
      {
        (StatusTitle, StatusText, Tone) = (L.T("home.status.ok.title"), L.T("home.status.ok.text", ActiveExpected), RowTone.Success);
      }

      if (status.Error is { } error)
        StatusText += L.T("home.status.noAnswerFrom", error);
    }

    [RelayCommand]
    private async Task PlayAsync()
    {
      if (_session.Layout is not { } layout)
        return;
      if (Install.IsRunning)
      {
        ToastService.Warning(L.T("home.play.wait"));
        return;
      }
      try
      {
        GameLauncher.Play(layout, SelectedExecutable, SelectedProfile, _elevation.Unelevated);
        ToastService.Info(SelectedExecutable is null ? L.T("home.play.openingMo2") : L.T("home.play.opening", SelectedExecutable), L.T("home.play.goodHunting"));
      }
      catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception)
      {
        await DialogService.ErrorAsync(L.T("home.play.failed"), ex.Message);
      }
    }

    [RelayCommand]
    private async Task OpenModOrganizerAsync()
    {
      if (_session.Layout is not { } layout)
        return;
      try
      {
        GameLauncher.OpenModOrganizer(layout, SelectedProfile, _elevation.Unelevated);
      }
      catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception)
      {
        await DialogService.ErrorAsync(L.T("home.play.failed"), ex.Message);
      }
    }

    /// <summary>
    /// Volta o modlist.txt do perfil do GAMMA pra cópia de antes da última instalação. O de agora vira cópia também,
    /// então voltar de novo desfaz. Com o MO2 aberto não: ele grava a lista dele por cima quando fecha.
    /// </summary>
    [RelayCommand]
    private async Task RestoreProfileAsync()
    {
      if (_session.Layout is not { } layout)
        return;
      if (Install.IsRunning)
      {
        ToastService.Warning(L.T("home.restoreProfile.busy"));
        return;
      }
      if (ProfileSetup.LatestBackup(layout) is not { } latest)
      {
        await DialogService.InfoAsync(L.T("home.restoreProfile.title"), L.T("home.restoreProfile.none"));
        return;
      }
      if (_system.IsModOrganizerRunning)
      {
        await DialogService.WarningAsync(L.T("home.restoreProfile.title"), L.T("home.restoreProfile.closeMo2"));
        return;
      }
      if (!await DialogService.ConfirmAsync(L.T("home.restoreProfile.title"), L.T("home.restoreProfile.confirm", _session.Metadata.FolderName, latest.LastWriteTime), L.T("home.restoreProfile.restore")))
        return;

      try
      {
        ProfileSetup.RestoreLatest(layout, _session.Metadata);
        ToastService.Success(L.T("home.restoreProfile.done", latest.LastWriteTime));
      }
      catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
      {
        await DialogService.ErrorAsync(L.T("home.restoreProfile.title"), ex.Message);
      }
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
