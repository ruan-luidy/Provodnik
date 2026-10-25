using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Provodnik.Core.Text;
using Provodnik.Wpf.Features.AppUpdate;
using Provodnik.Wpf.Shared.Services;

namespace Provodnik.Wpf.Features.About
{
  /// <summary>O Sobre: a versão, a procura de versão nova (o <see cref="AppUpdateService"/>, que baixa e troca o exe) e os links.</summary>
  public sealed partial class AboutViewModel(AppUpdateService update, GammaSession session) : ObservableObject
  {
    public AppUpdateService Update { get; } = update;

    public string VersionText => L.T("about.version", AppInfo.Version);

    public string BuildText => AppInfo.BuildDate is { } date ? L.T("about.build", date.ToString("d", L.Culture)) : L.T("about.buildLocal");

    [RelayCommand]
    private void OpenRepository() => FolderOpener.Browse(AppUpdateService.RepositoryUrl);

    [RelayCommand]
    private void OpenReleases() => FolderOpener.Browse($"{AppUpdateService.RepositoryUrl}/releases");

    [RelayCommand]
    private void OpenDataFolder() => FolderOpener.Open(session.Layout?.DataDir);
  }
}
