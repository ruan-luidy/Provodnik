using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Provodnik.Core.Install;

namespace Provodnik.Wpf.Shared.Services
{
  /// <summary>
  /// Preferências de quem usa (%LocalAppData%\Provodnik\settings.json):
  ///   - GammaRoot: a pasta do GAMMA quando o exe não mora dentro dela (nulo: procura a partir do exe);
  ///   - Executable: o executável do MO2 que o Jogar abre ("Anomaly (DX11-AVX)");
  ///   - AutoUpdateApp: procura e baixa sozinho a versão nova do próprio Проводник (só na versão do Setup.exe);
  ///   - o resto vira <see cref="InstallOptions"/>, com os padrões do launcher original.
  /// </summary>
  public sealed record AppSettings(
    string? GammaRoot = null,
    string? Executable = null,
    int ModDbParallel = 3,
    int HttpParallel = 4,
    int ExtractParallel = 2,
    bool CheckMd5 = true,
    bool UpdateGitHubAddons = true,
    bool DeleteReshadeDlls = true,
    bool CreateShortcuts = true,
    string ModListKey = "",
    string? Tab = null,
    bool PaneCompact = false,
    bool ShowConsole = true,
    bool Mica = true,
    bool DarkTheme = true,
    bool AutoUpdateApp = true)
  {
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static AppSettings Load() => Load(AppPaths.Settings);

    public static AppSettings Load(string path)
    {
      try
      {
        return File.Exists(path)
          ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), Options) ?? new AppSettings()
          : new AppSettings();
      }
      catch (Exception ex) when (ex is JsonException or IOException or NotSupportedException)
      {
        Debug.WriteLine($"[Settings] ignorando {path}: {ex.Message}");
        return new AppSettings();
      }
    }

    public void Save() => Save(AppPaths.Settings);

    public void Save(string path)
    {
      var temp = path + ".tmp";
      File.WriteAllText(temp, JsonSerializer.Serialize(this, Options));
      File.Move(temp, path, overwrite: true);
    }

    /// <summary>As opções de uma instalação a partir das preferências.</summary>
    public InstallOptions ToInstallOptions() => new()
    {
      ModListKey = ModListKey,
      CheckMd5 = CheckMd5,
      UpdateGitHubAddons = UpdateGitHubAddons,
      DeleteReshadeDlls = DeleteReshadeDlls,
      CreateShortcuts = CreateShortcuts,
      ModDbParallel = Math.Clamp(ModDbParallel, 1, 8),
      HttpParallel = Math.Clamp(HttpParallel, 1, 16),
      ExtractParallel = Math.Clamp(ExtractParallel, 1, 8),
    };
  }
}
