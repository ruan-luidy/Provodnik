using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Provodnik.Wpf.Features.AppUpdate;
using Provodnik.Wpf.Shared.Services;

namespace Provodnik.Wpf.Features.About
{
  /// <summary>O Sobre: versão, a atualização do app, o que mudou nesta versão e os links.</summary>
  public sealed partial class AboutViewModel(AppUpdateService update, GammaSession session) : ObservableObject
  {
    public AppUpdateService Update { get; } = update;

    public string Version => AppInfo.Version;

    public string BuildText => AppInfo.BuildDate is { } date ? $"build de {date:dd/MM/yyyy}" : "build local";

    /// <summary>O exe solto na pasta do GAMMA (ou rodando do código): não se atualiza sozinho.</summary>
    public bool IsPortable => !Update.IsInstalled;

    /// <summary>Os itens da versão atual no CHANGELOG embutido (a seção "## 0.3.0").</summary>
    public IReadOnlyList<string> Changes { get; } = ChangesOf(AppInfo.Changelog, AppInfo.Version);

    [RelayCommand]
    private void OpenRepository() => FolderOpener.Browse(AppUpdateService.RepositoryUrl);

    [RelayCommand]
    private void OpenReleases() => FolderOpener.Browse($"{AppUpdateService.RepositoryUrl}/releases");

    [RelayCommand]
    private void OpenDataFolder() => FolderOpener.Open(session.Layout?.DataDir);

    private static IReadOnlyList<string> ChangesOf(string changelog, string version)
    {
      var lines = changelog.Replace("\r\n", "\n").Split('\n');
      // a seção desta versão; sem ela (build local à frente do CHANGELOG), a primeira que tiver
      var start = Array.FindIndex(lines, l => l.Trim() == $"## {version.Split('-')[0]}");
      if (start < 0)
        start = Array.FindIndex(lines, l => l.StartsWith("## ", StringComparison.Ordinal));
      if (start < 0)
        return [];

      return lines
        .Skip(start + 1)
        .TakeWhile(l => !l.StartsWith("## ", StringComparison.Ordinal))
        .Where(l => l.StartsWith("- ", StringComparison.Ordinal))
        .Select(l => l[2..].Replace("`", string.Empty).Trim())
        .ToList();
    }
  }
}
