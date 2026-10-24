using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Provodnik.Core.Text;
using Microsoft.Win32;
using Provodnik.Wpf.Shared.Services;

namespace Provodnik.Wpf.Features.Settings
{
  /// <summary>
  /// Configurações: a pasta do GAMMA (quando o exe não mora nela), quantos downloads e extrações ao mesmo tempo e as
  /// opções que o launcher original tinha como caixinhas (md5, GitHub, Reshade), mais os atalhos.
  /// </summary>
  public sealed partial class SettingsViewModel : ObservableObject
  {
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
      _mica = current.Mica;
      _autoUpdateApp = current.AutoUpdateApp;
      _language = Languages.First(l => l.Code == L.Language);
    }

    /// <summary>Os idiomas, cada um escrito nele mesmo (quem não lê o atual acha o seu).</summary>
    public IReadOnlyList<LanguageOption> Languages { get; } =
    [
      new(L.Portuguese, "Português"),
      new(L.English, "English"),
      new(L.Russian, "Русский"),
    ];

    /// <summary>Salvou com outro idioma: o app precisa reabrir pra trocar.</summary>
    public bool LanguageChanged { get; private set; }

    /// <summary>O exe mora dentro do GAMMA: a pasta é essa e pronto.</summary>
    public bool IsPortable => _session.IsPortable;

    [RelayCommand]
    private void Browse()
    {
      var dialog = new OpenFolderDialog { Title = L.T("settings.folder.browse.title") };
      if (!string.IsNullOrEmpty(GammaRoot))
        dialog.InitialDirectory = GammaRoot;
      if (dialog.ShowDialog() == true)
        GammaRoot = dialog.FolderName;
    }

    /// <summary>Grava. Falso (com o motivo em <see cref="Error"/>) quando a pasta não é do GAMMA.</summary>
    public bool Save()
    {
      if (!IsPortable && !string.IsNullOrWhiteSpace(GammaRoot) && !string.Equals(GammaRoot, _session.Layout?.Root, StringComparison.OrdinalIgnoreCase))
      {
        Error = _session.SetRoot(GammaRoot);
        if (Error is not null)
          return false;
      }

      _store.Update(s => s with
      {
        ModDbParallel = Math.Clamp(ModDbParallel, 1, 8),
        HttpParallel = Math.Clamp(HttpParallel, 1, 16),
        ExtractParallel = Math.Clamp(ExtractParallel, 1, 8),
        CheckMd5 = CheckMd5,
        UpdateGitHubAddons = UpdateGitHubAddons,
        DeleteReshadeDlls = DeleteReshadeDlls,
        CreateShortcuts = CreateShortcuts,
        Mica = Mica,
        AutoUpdateApp = AutoUpdateApp,
        Language = Language.Code,
      });
      LanguageChanged = Language.Code != L.Language;
      return true;
    }
  }

  /// <summary>Um idioma da lista: o código ("pt") e o nome escrito nele mesmo.</summary>
  public sealed record LanguageOption(string Code, string Name)
  {
    public override string ToString() => Name;
  }
}
