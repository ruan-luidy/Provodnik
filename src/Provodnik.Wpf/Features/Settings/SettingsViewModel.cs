using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Provodnik.Core.Text;
using Provodnik.Wpf.Shared.Services;

namespace Provodnik.Wpf.Features.Settings
{
  /// <summary>
  /// Configurações: a pasta do GAMMA (quando o exe não mora nela), quantos downloads e extrações ao mesmo tempo, as
  /// opções que o launcher original tinha como caixinhas (md5, GitHub, Reshade), os atalhos, o idioma e a aparência.
  /// Cada opção vale na hora em que muda, como na Связь; só a pasta do GAMMA, que precisa ser conferida, vale no
  /// Pronto. O idioma novo pede pra reabrir o app (<see cref="LanguageSwitchRequested"/>).
  /// </summary>
  public sealed partial class SettingsViewModel : ObservableObject
  {
    // o que grava assim que muda
    private static readonly HashSet<string> Live =
    [
      nameof(ModDbParallel), nameof(HttpParallel), nameof(ExtractParallel), nameof(CheckMd5), nameof(UpdateGitHubAddons),
      nameof(DeleteReshadeDlls), nameof(CreateShortcuts), nameof(BackupSaves), nameof(Mica), nameof(AutoUpdateApp),
    ];

    private readonly SettingsStore _store;
    private readonly GammaSession _session;

    [ObservableProperty] private string _gammaRoot;
    [ObservableProperty] private int _modDbParallel;
    [ObservableProperty] private int _httpParallel;
    [ObservableProperty] private int _extractParallel;
    [ObservableProperty] private bool _checkMd5;
    [ObservableProperty] private bool _updateGitHubAddons;
    [ObservableProperty] private bool _deleteReshadeDlls;
    [ObservableProperty] private bool _createShortcuts;
    [ObservableProperty] private bool _backupSaves;
    [ObservableProperty] private bool _mica;
    [ObservableProperty] private bool _autoUpdateApp;
    [ObservableProperty] private LanguageOption _language;
    [ObservableProperty] private string? _error;

    public SettingsViewModel(SettingsStore store, GammaSession session)
    {
      _store = store;
      _session = session;
      var current = store.Current;
      _gammaRoot = session.Layout?.Root ?? string.Empty;
      _modDbParallel = current.ModDbParallel;
      _httpParallel = current.HttpParallel;
      _extractParallel = current.ExtractParallel;
      _checkMd5 = current.CheckMd5;
      _updateGitHubAddons = current.UpdateGitHubAddons;
      _deleteReshadeDlls = current.DeleteReshadeDlls;
      _createShortcuts = current.CreateShortcuts;
      _backupSaves = current.BackupSaves;
      _mica = current.Mica;
      _autoUpdateApp = current.AutoUpdateApp;
      Languages =
      [
        new(null, L.T("settings.language.auto")),
        new(L.Portuguese, "Português"),
        new(L.English, "English"),
        new(L.Russian, "Русский"),
      ];
      _language = Languages.FirstOrDefault(l => l.Code == current.Language) ?? Languages[0];
    }

    /// <summary>
    /// O automático (o idioma do Windows, o padrão) e os idiomas, cada um escrito nele mesmo: quem não lê o atual
    /// acha o seu.
    /// </summary>
    public IReadOnlyList<LanguageOption> Languages { get; }

    /// <summary>Escolheu um idioma diferente do que está na tela: o app reabre pra trocar.</summary>
    public event EventHandler? LanguageSwitchRequested;

    /// <summary>O exe mora dentro do GAMMA: a pasta é essa e pronto.</summary>
    public bool IsPortable => _session.IsPortable;

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
      base.OnPropertyChanged(e);
      if (e.PropertyName is { } name && Live.Contains(name))
        Persist();
    }

    partial void OnLanguageChanged(LanguageOption value)
    {
      _store.Update(s => s with { Language = value.Code });
      if ((value.Code ?? L.SystemLanguage) != L.Language)
        LanguageSwitchRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Browse()
    {
      var dialog = new OpenFolderDialog { Title = L.T("settings.folder.browse.title") };
      if (!string.IsNullOrEmpty(GammaRoot))
        dialog.InitialDirectory = GammaRoot;
      if (dialog.ShowDialog() == true)
        GammaRoot = dialog.FolderName;
    }

    /// <summary>O Pronto: a pasta do GAMMA, se mudou. Falso (com o motivo em <see cref="Error"/>) quando a pasta não é do GAMMA.</summary>
    public bool Done()
    {
      if (!IsPortable && !string.IsNullOrWhiteSpace(GammaRoot) && !string.Equals(GammaRoot, _session.Layout?.Root, StringComparison.OrdinalIgnoreCase))
      {
        Error = _session.SetRoot(GammaRoot);
        if (Error is not null)
          return false;
      }
      return true;
    }

    private void Persist() =>
      _store.Update(s => s with
      {
        ModDbParallel = Math.Clamp(ModDbParallel, 1, 8),
        HttpParallel = Math.Clamp(HttpParallel, 1, 16),
        ExtractParallel = Math.Clamp(ExtractParallel, 1, 8),
        CheckMd5 = CheckMd5,
        UpdateGitHubAddons = UpdateGitHubAddons,
        DeleteReshadeDlls = DeleteReshadeDlls,
        CreateShortcuts = CreateShortcuts,
        BackupSaves = BackupSaves,
        Mica = Mica,
        AutoUpdateApp = AutoUpdateApp,
      });
  }

  /// <summary>Um idioma da lista: o código ("pt"; nulo é o do Windows) e o nome escrito nele mesmo.</summary>
  public sealed record LanguageOption(string? Code, string Name)
  {
    public override string ToString() => Name;
  }
}
