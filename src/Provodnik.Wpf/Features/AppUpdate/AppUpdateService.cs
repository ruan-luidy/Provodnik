using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Provodnik.Core.Text;
using Provodnik.Wpf.Features.Install;
using Provodnik.Wpf.Shared.Controls;
using Provodnik.Wpf.Shared.Services;

namespace Provodnik.Wpf.Features.AppUpdate
{
  /// <summary>Em que pé está a atualização do próprio Проводник.</summary>
  public enum AppUpdateState
  {
    /// <summary>Não se atualiza: rodando do código ou na simulação.</summary>
    Unavailable,
    Idle,
    Checking,
    Downloading,
    UpToDate,
    /// <summary>O exe novo já baixou: entra ao fechar, ou agora com <see cref="AppUpdateService.ApplyAndRestart"/>.</summary>
    Ready,
    Failed,
  }

  /// <summary>
  /// O Проводник é um exe só, que mora na pasta do GAMMA, e se atualiza sozinho: pergunta o último release pro GitHub,
  /// baixa o Provodnik.exe novo do lado do atual (Provodnik.exe.new, conferido pelo tamanho e pelo SHA-256 que o GitHub
  /// informa) e troca quando a pessoa reinicia ou o app fecha. O Windows deixa renomear um exe rodando: o atual vira
  /// Provodnik.exe.old, o novo toma o nome, e o .old some na próxima abertura.
  /// </summary>
  public sealed partial class AppUpdateService : ObservableObject
  {
    public const string RepositoryUrl = "https://github.com/ruan-luidy/Provodnik";
    private const string AssetName = "Provodnik.exe";

    private static readonly TimeSpan FirstCheck = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(2);

    private readonly SettingsStore _settings;
    private readonly InstallService _install;
    private readonly bool _demo;
    private readonly DispatcherTimer _timer;
    private readonly HttpClient _http;
    private readonly string? _exe;
    private bool _applied;

    [ObservableProperty] private AppUpdateState _state;
    [ObservableProperty] private string _status = string.Empty;

    /// <summary>O estado em duas palavras.</summary>
    [ObservableProperty] private string _shortStatus = string.Empty;

    /// <summary>De 0 a 100 enquanto baixa.</summary>
    [ObservableProperty] private int _progress;

    /// <summary>A versão que já baixou e espera pra entrar.</summary>
    [ObservableProperty] private string? _pendingVersion;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckNowCommand))]
    private bool _isChecking;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyAndRestartCommand))]
    private bool _isReady;

    public AppUpdateService(SettingsStore settings, AppOptions options, InstallService install)
    {
      _settings = settings;
      _install = install;
      _demo = options.Demo;
      // só o exe único (publicado com tudo dentro) se troca; rodando do código o assembly tem caminho e não mexe
      _exe = string.IsNullOrEmpty(typeof(AppUpdateService).Assembly.Location) ? Environment.ProcessPath : null;
      _http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
      // a API do GitHub recusa pedido sem User-Agent
      _http.DefaultRequestHeaders.UserAgent.ParseAdd($"Provodnik/{AppInfo.Version}");

      _timer = new DispatcherTimer { Interval = FirstCheck };
      _timer.Tick += async (_, _) =>
      {
        _timer.Interval = Interval;
        await CheckAsync(announce: false);
      };
      ShowIdle();
    }

    public string Version => AppInfo.Version;

    /// <summary>Pode se atualizar: o exe único, fora da simulação.</summary>
    public bool CanUpdate => _exe is not null && !_demo;

    private string NewFile => _exe + ".new";

    private string OldFile => _exe + ".old";

    /// <summary>Apaga o exe velho da última troca e liga a procura sozinha (a configuração desliga).</summary>
    public void Start()
    {
      if (_exe is not null)
        TryDelete(OldFile);
      Refresh();
      _settings.Changed += (_, _) => Refresh();
    }

    [RelayCommand(CanExecute = nameof(CanCheckNow))]
    public Task CheckNowAsync() => CheckAsync(announce: true);

    /// <summary>Troca o exe e abre o novo. No meio de uma instalação só avisa: a troca fica pra quando fechar.</summary>
    [RelayCommand(CanExecute = nameof(IsReady))]
    public void ApplyAndRestart()
    {
      if (!IsReady)
        return;
      if (_install.IsRunning)
      {
        ToastService.Warning(L.T("appUpdate.busyInstalling"));
        return;
      }
      if (!Swap())
      {
        ToastService.Error(L.T("appUpdate.applyFailed"));
        return;
      }
      ((App)Application.Current).Restart();
    }

    /// <summary>Chamado ao fechar: a versão baixada toma o lugar do exe, e a próxima abertura já é ela.</summary>
    public void ApplyOnExit()
    {
      if (IsReady && !_applied)
        Swap();
    }

    private bool CanCheckNow() => !IsChecking;

    // o atual vira .old (o Windows deixa renomear o exe rodando) e o novo assume o nome; deu errado no meio, desfaz
    private bool Swap()
    {
      if (_exe is null || !File.Exists(NewFile))
        return false;
      try
      {
        TryDelete(OldFile);
        File.Move(_exe, OldFile);
        try
        {
          File.Move(NewFile, _exe);
        }
        catch
        {
          File.Move(OldFile, _exe);
          throw;
        }
        _applied = true;
        return true;
      }
      catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
      {
        AppLog.Error(ex, "Trocando o exe do Проводник");
        return false;
      }
    }

    private void Refresh()
    {
      if (CanUpdate && !IsReady && _settings.Current.AutoUpdateApp)
      {
        if (!_timer.IsEnabled)
          _timer.Start();
      }
      else
        _timer.Stop();
    }

    private void ShowIdle()
    {
      if (CanUpdate)
      {
        State = AppUpdateState.Idle;
        Status = L.T("appUpdate.idle");
        ShortStatus = string.Empty;
        return;
      }

      State = AppUpdateState.Unavailable;
      Status = L.T(_demo ? "appUpdate.demo" : "appUpdate.notInstalled");
      ShortStatus = L.T(_demo ? "appUpdate.short.demo" : "appUpdate.short.notInstalled");
    }

    private async Task CheckAsync(bool announce)
    {
      if (IsChecking)
        return;
      if (!CanUpdate)
      {
        ShowIdle();
        return;
      }
      if (IsReady)
      {
        if (announce)
          AnnounceReady();
        return;
      }

      IsChecking = true;
      State = AppUpdateState.Checking;
      Status = L.T("appUpdate.checking");
      ShortStatus = L.T("appUpdate.short.checking");
      try
      {
        var release = await LatestAsync();
        var asset = release?.Assets?.FirstOrDefault(a => string.Equals(a.Name, AssetName, StringComparison.OrdinalIgnoreCase));
        var version = release?.TagName?.TrimStart('v', 'V');
        if (version is null || asset?.DownloadUrl is null || !IsNewer(version, Version))
        {
          State = AppUpdateState.UpToDate;
          Status = L.T("appUpdate.upToDate");
          ShortStatus = L.T("appUpdate.short.upToDate");
          return;
        }

        State = AppUpdateState.Downloading;
        Progress = 0;
        Status = L.T("appUpdate.downloading", version);
        ShortStatus = L.T("appUpdate.short.downloading");
        if (!await DownloadAsync(asset, version))
        {
          State = AppUpdateState.Failed;
          Status = L.T("appUpdate.badDownload");
          ShortStatus = L.T("appUpdate.short.failed");
          return;
        }

        _timer.Stop();
        PendingVersion = version;
        State = AppUpdateState.Ready;
        Status = L.T("appUpdate.ready", version);
        ShortStatus = L.T("appUpdate.short.ready", version);
        IsReady = true;
        AnnounceReady();
      }
      catch (UnauthorizedAccessException ex)
      {
        // a pasta não deixa gravar o exe novo (GAMMA em Program Files sem administrador)
        AppLog.Error(ex, "Baixando a versão nova do Проводник");
        State = AppUpdateState.Failed;
        Status = L.T("appUpdate.noWrite");
        ShortStatus = L.T("appUpdate.short.failed");
      }
      catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or System.Text.Json.JsonException)
      {
        // sem internet, GitHub fora ou limite da API: só tenta de novo na próxima volta, nada de caixa de erro
        AppLog.Error(ex, "Procurando versão nova do Проводник");
        State = AppUpdateState.Failed;
        Status = L.T("appUpdate.failed");
        ShortStatus = L.T("appUpdate.short.failed");
      }
      finally
      {
        IsChecking = false;
      }
    }

    // versão de teste ("0.4.0-teste.2") também enxerga os pré-releases; a normal, só o último publicado
    private async Task<Release?> LatestAsync()
    {
      var api = $"https://api.github.com/repos/{RepositoryUrl["https://github.com/".Length..]}/releases";
      if (!AppInfo.IsTest)
        return await _http.GetFromJsonAsync<Release>($"{api}/latest");
      var all = await _http.GetFromJsonAsync<List<Release>>($"{api}?per_page=10");
      return all?.FirstOrDefault(r => !r.Draft);
    }

    /// <summary>Baixa pro .new, com o progresso, e confere tamanho e SHA-256 (quando o GitHub informa) antes de aceitar.</summary>
    private async Task<bool> DownloadAsync(Asset asset, string version)
    {
      var dispatcher = Application.Current.Dispatcher;
      using var response = await _http.GetAsync(asset.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
      response.EnsureSuccessStatusCode();
      var total = response.Content.Headers.ContentLength ?? asset.Size;
      await using (var source = await response.Content.ReadAsStreamAsync())
      await using (var target = File.Create(NewFile))
      {
        var buffer = new byte[81920];
        long received = 0;
        var last = -1;
        int read;
        while ((read = await source.ReadAsync(buffer)) > 0)
        {
          await target.WriteAsync(buffer.AsMemory(0, read));
          received += read;
          var percent = total > 0 ? (int)(received * 100 / total) : 0;
          if (percent != last)
          {
            last = percent;
            _ = dispatcher.InvokeAsync(() =>
            {
              Progress = percent;
              Status = L.T("appUpdate.downloadingPercent", version, percent);
              ShortStatus = L.T("appUpdate.short.downloadingPercent", percent);
            });
          }
        }
      }

      var ok = new FileInfo(NewFile).Length == asset.Size;
      if (ok && asset.Digest?.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) == true)
      {
        await using var file = File.OpenRead(NewFile);
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(file));
        ok = string.Equals(hash, asset.Digest["sha256:".Length..], StringComparison.OrdinalIgnoreCase);
      }
      if (!ok)
        TryDelete(NewFile);
      return ok;
    }

    // "0.4.0" contra "0.3.0"; o pedaço de teste ("-teste.2") não conta, e versão que não lê não é mais nova
    private static bool IsNewer(string latest, string current) =>
      System.Version.TryParse(latest.Split('-')[0], out var a)
      && System.Version.TryParse(current.Split('-')[0], out var b)
      && a > b;

    private static void TryDelete(string path)
    {
      try
      {
        if (File.Exists(path))
          File.Delete(path);
      }
      catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
      {
        // o processo velho ainda saindo: fica pra próxima abertura
      }
    }

    private void AnnounceReady() =>
      ToastService.Show(
        DialogKind.Info,
        L.T("appUpdate.toast.title"),
        L.T("appUpdate.toast.message", PendingVersion),
        null,
        L.T("appUpdate.toast.restart"),
        ApplyAndRestart,
        desktop: true);

    private sealed record Release(
      [property: JsonPropertyName("tag_name")] string? TagName,
      [property: JsonPropertyName("draft")] bool Draft,
      [property: JsonPropertyName("assets")] List<Asset>? Assets);

    private sealed record Asset(
      [property: JsonPropertyName("name")] string? Name,
      [property: JsonPropertyName("size")] long Size,
      [property: JsonPropertyName("digest")] string? Digest,
      [property: JsonPropertyName("browser_download_url")] string? DownloadUrl);
  }
}
