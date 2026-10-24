using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Provodnik.Core.Text;
using Provodnik.Wpf.Features.AppUpdate;
using Provodnik.Wpf.Shared.Services;

namespace Provodnik.Wpf.Features.About
{
  /// <summary>
  /// O Sobre: a versão, a procura de versão nova e os links. Instalado pelo Setup.exe, a procura é a do Velopack (baixa
  /// e reinicia); a cópia solta na pasta do GAMMA pergunta o último release pro GitHub e oferece o download.
  /// </summary>
  public sealed partial class AboutViewModel(AppUpdateService update, GammaSession session) : ObservableObject
  {
    private static readonly HttpClient Http = CreateHttp();

    [ObservableProperty] private string _portableStatus = L.T("about.update.idle");
    [ObservableProperty] private string? _latestUrl;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckCommand))]
    private bool _isChecking;

    public AppUpdateService Update { get; } = update;

    public string Version => AppInfo.Version;

    public string VersionText => L.T("about.version", AppInfo.Version);

    public string BuildText => AppInfo.BuildDate is { } date ? L.T("about.build", date.ToString("d", L.Culture)) : L.T("about.buildLocal");

    /// <summary>O exe solto na pasta do GAMMA (ou rodando do código): não se atualiza sozinho.</summary>
    public bool IsPortable => !Update.IsInstalled;

    /// <summary>Saiu versão nova e esta cópia não se atualiza: o botão de baixar aparece.</summary>
    public bool HasDownload => LatestUrl is not null;

    partial void OnLatestUrlChanged(string? value) => OnPropertyChanged(nameof(HasDownload));

    [RelayCommand(CanExecute = nameof(CanCheck))]
    private async Task CheckAsync()
    {
      if (!IsPortable)
      {
        await Update.CheckNowAsync();
        return;
      }

      IsChecking = true;
      PortableStatus = L.T("about.update.checking");
      try
      {
        var release = await Http.GetFromJsonAsync<Release>("releases/latest");
        var latest = release?.TagName?.TrimStart('v', 'V');
        if (latest is null)
        {
          PortableStatus = L.T("about.update.noAnswer");
        }
        else if (IsNewer(latest, Version))
        {
          PortableStatus = L.T("about.update.newer", latest);
          LatestUrl = release!.HtmlUrl;
        }
        else
        {
          PortableStatus = L.T("about.update.latest");
          LatestUrl = null;
        }
      }
      catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
      {
        PortableStatus = L.T("about.update.failed");
      }
      finally
      {
        IsChecking = false;
      }
    }

    private bool CanCheck() => !IsChecking;

    [RelayCommand]
    private void Download() => FolderOpener.Browse(LatestUrl);

    [RelayCommand]
    private void OpenRepository() => FolderOpener.Browse(AppUpdateService.RepositoryUrl);

    [RelayCommand]
    private void OpenReleases() => FolderOpener.Browse($"{AppUpdateService.RepositoryUrl}/releases");

    [RelayCommand]
    private void OpenDataFolder() => FolderOpener.Open(session.Layout?.DataDir);

    // "0.4.0" contra "0.3.0"; a parte de teste ("-teste.2") não conta, e versão que não lê não é mais nova
    private static bool IsNewer(string latest, string current) =>
      System.Version.TryParse(latest.Split('-')[0], out var a)
      && System.Version.TryParse(current.Split('-')[0], out var b)
      && a > b;

    private static HttpClient CreateHttp()
    {
      var http = new HttpClient
      {
        BaseAddress = new Uri("https://api.github.com/repos/ruan-luidy/Provodnik/"),
        Timeout = TimeSpan.FromSeconds(15),
      };
      // a API do GitHub recusa pedido sem User-Agent
      http.DefaultRequestHeaders.UserAgent.ParseAdd($"Provodnik/{AppInfo.Version}");
      http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
      return http;
    }

    private sealed record Release(
      [property: JsonPropertyName("tag_name")] string? TagName,
      [property: JsonPropertyName("html_url")] string? HtmlUrl);
  }
}
