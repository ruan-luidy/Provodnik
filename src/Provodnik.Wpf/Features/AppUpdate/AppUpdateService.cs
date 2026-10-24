using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Provodnik.Core.Text;
using Provodnik.Wpf.Features.Install;
using Provodnik.Wpf.Shared.Controls;
using Provodnik.Wpf.Shared.Services;
using Velopack;
using Velopack.Sources;

namespace Provodnik.Wpf.Features.AppUpdate
{
  /// <summary>Em que pé está a atualização do próprio Проводник.</summary>
  public enum AppUpdateState
  {
    /// <summary>Não se atualiza aqui: exe solto na pasta do GAMMA, rodando do código ou na simulação.</summary>
    Unavailable,
    Idle,
    Checking,
    Downloading,
    UpToDate,
    /// <summary>A versão nova já baixou: entra ao fechar, ou agora com <see cref="AppUpdateService.ApplyAndRestart"/>.</summary>
    Ready,
    Failed,
  }

  /// <summary>
  /// A atualização do próprio Проводник pelo Velopack (a do GAMMA é o UpdateChecker), no esquema do Otmetka: procura
  /// no GitHub Releases, baixa sozinha e deixa pronta; o toast "reiniciar agora" avisa, e sem clique a versão entra
  /// quando o app fechar. Só vale pra quem instalou pelo Setup.exe: o exe solto na pasta do GAMMA segue sem
  /// atualização e sem reclamar.
  /// </summary>
  public sealed partial class AppUpdateService : ObservableObject
  {
    public const string RepositoryUrl = "https://github.com/ruan-luidy/Provodnik";

    private static readonly TimeSpan FirstCheck = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(2);

    // Canal fixo, igual ao do tools/build_tool.py
    private static readonly UpdateOptions Options = new() { ExplicitChannel = "win" };

    private readonly UpdateManager? _manager;
    private readonly SettingsStore _settings;
    private readonly InstallService _install;
    private readonly bool _demo;
    private readonly DispatcherTimer _timer;
    private UpdateInfo? _pending;
    private bool _applying;

    [ObservableProperty] private AppUpdateState _state;
    [ObservableProperty] private string _status = string.Empty;

    /// <summary>O estado em duas palavras, do lado da versão no Sobre.</summary>
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
      try
      {
        // O repositório é público: sem token. Versão de teste também enxerga os pré-releases
        _manager = new UpdateManager(new GithubSource(RepositoryUrl, null, AppInfo.IsTest), Options);
      }
      catch (Exception ex)
      {
        AppLog.Error(ex, "Atualização do Проводник");
      }

      _timer = new DispatcherTimer { Interval = FirstCheck };
      _timer.Tick += async (_, _) =>
      {
        _timer.Interval = Interval;
        await CheckAsync(announce: false);
      };
      ShowIdle();
    }

    public string Version => AppInfo.Version;

    /// <summary>Instalado pelo Setup.exe do Velopack (o exe solto na pasta do GAMMA não é).</summary>
    public bool IsInstalled => _manager?.IsInstalled == true;

    /// <summary>Pode procurar versão nova: instalado e fora da simulação.</summary>
    public bool CanUpdate => IsInstalled && !_demo;

    /// <summary>Liga a procura sozinha (a primeira uns segundos depois de abrir); a configuração desliga.</summary>
    public void Start()
    {
      Refresh();
      _settings.Changed += (_, _) => Refresh();
    }

    [RelayCommand(CanExecute = nameof(CanCheckNow))]
    public Task CheckNowAsync() => CheckAsync(announce: true);

    /// <summary>Fecha, aplica a versão baixada e abre de novo. No meio de uma instalação só avisa.</summary>
    [RelayCommand(CanExecute = nameof(IsReady))]
    public void ApplyAndRestart()
    {
      if (_manager is null || _pending is null)
        return;

      if (_install.IsRunning)
      {
        ToastService.Warning(L.T("appUpdate.busyInstalling"));
        return;
      }

      try
      {
        // fecha do jeito normal (configurações, instância única), e o Update.exe espera o processo sair
        _manager.WaitExitThenApplyUpdates(_pending, silent: false, restart: true);
        _applying = true;
        Application.Current.Shutdown();
      }
      catch (Exception ex)
      {
        AppLog.Error(ex, "Aplicando a atualização do Проводник");
        ToastService.Error(L.T("appUpdate.applyFailed"));
      }
    }

    /// <summary>Chamado ao fechar: a versão baixada entra quando o app terminar de sair.</summary>
    public void ApplyOnExit()
    {
      if (_applying || _manager is null || _pending is null)
        return;

      try
      {
        _manager.WaitExitThenApplyUpdates(_pending, silent: true, restart: false);
      }
      catch (Exception ex)
      {
        AppLog.Error(ex, "Atualização do Проводник ao fechar");
      }
    }

    private bool CanCheckNow() => !IsChecking;

    private void Refresh()
    {
      if (CanUpdate && _pending is null && _settings.Current.AutoUpdateApp)
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

      if (_pending is not null)
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
        var update = await _manager!.CheckForUpdatesAsync();
        if (update is null)
        {
          State = AppUpdateState.UpToDate;
          Status = L.T("appUpdate.upToDate");
          ShortStatus = L.T("appUpdate.short.upToDate");
          return;
        }

        var version = update.TargetFullRelease.Version.ToString();
        State = AppUpdateState.Downloading;
        Progress = 0;
        Status = L.T("appUpdate.downloading", version);
        ShortStatus = L.T("appUpdate.short.downloading");
        var dispatcher = Application.Current.Dispatcher;
        await _manager.DownloadUpdatesAsync(update, percent => dispatcher.InvokeAsync(() =>
        {
          Progress = percent;
          Status = L.T("appUpdate.downloadingPercent", version, percent);
          ShortStatus = L.T("appUpdate.short.downloadingPercent", percent);
        }));

        _pending = update;
        _timer.Stop();
        PendingVersion = version;
        State = AppUpdateState.Ready;
        Status = L.T("appUpdate.ready", version);
        ShortStatus = L.T("appUpdate.short.ready", version);
        IsReady = true;
        AnnounceReady();
      }
      catch (Exception ex)
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

    private void AnnounceReady() =>
      ToastService.Show(
        DialogKind.Info,
        L.T("appUpdate.toast.title"),
        L.T("appUpdate.toast.message", PendingVersion),
        null,
        L.T("appUpdate.toast.restart"),
        ApplyAndRestart,
        desktop: true);
  }
}
