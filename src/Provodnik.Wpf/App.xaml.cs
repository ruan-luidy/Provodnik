using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Provodnik.Core.Archives;
using Provodnik.Core.Desktop;
using Provodnik.Core.Diagnostics;
using Provodnik.Core.Downloads;
using Provodnik.Core.Layout;
using Provodnik.Core.Remote;
using Provodnik.Core.Repos;
using Provodnik.Core.Text;
using Provodnik.Features.Install;
using Provodnik.Features.Update;
using Provodnik.Infrastructure.Desktop;
using Provodnik.Infrastructure.Http;
using Provodnik.Infrastructure.Tools;
using Provodnik.Wpf.Features.AppUpdate;
using Provodnik.Wpf.Features.Demo;
using Provodnik.Wpf.Features.Home;
using Provodnik.Wpf.Features.Install;
using Provodnik.Wpf.Features.Logs;
using Provodnik.Wpf.Features.Mods;
using Provodnik.Wpf.Shared.Services;
using Provodnik.Wpf.Shell;

namespace Provodnik.Wpf
{
  public partial class App : Application
  {
    // instância única: duas instalações ao mesmo tempo na mesma pasta seria desastre
    private const string RealInstance = "Provodnik-5d2a91c4";

    // a simulação tem a trava dela: roda junto com uma instalação de verdade sem acordar a outra
    private string InstanceName = RealInstance;

    private ServiceProvider? _services;
    private Mutex? _instance;
    private EventWaitHandle? _wake;
    private bool _showingError;
    private bool _elevating;

    protected override void OnStartup(StartupEventArgs e)
    {
      var demo = DemoWorld.Requested(e.Args) ? new DemoWorld() : null;
      if (demo is not null)
      {
        InstanceName = RealInstance + "-demo";
        // configurações, errors.log e cookies da simulação ficam longe dos de verdade
        Environment.SetEnvironmentVariable("PROVODNIK_DATA_DIR", demo.AppData);
        demo.Prepare(reset: e.Args.Contains("--demo-novo"));
      }

      _instance = new Mutex(true, InstanceName, out var first);
      if (!first)
      {
        if (EventWaitHandle.TryOpenExisting(InstanceName + "-wake", out var wake))
          wake.Set();
        Shutdown();
        return;
      }

      base.OnStartup(e);

      // erro que escapar de um handler de UI vira aviso em vez de derrubar o app
      DispatcherUnhandledException += OnDispatcherUnhandledException;

      var settings = AppSettings.Load();
      ApplyLanguage(settings);
      _services = ConfigureServices(settings, demo);
      ThemeService.Initialize(_services.GetRequiredService<SettingsStore>());

      var window = _services.GetRequiredService<MainWindow>();
      MainWindow = window;
      ListenForSecondInstance(window);
      window.Show();
      DemoConsole(_services.GetRequiredService<IToolConsole>());
      // versão nova do próprio Проводник (só na instalada pelo Setup.exe, fora da simulação)
      _services.GetRequiredService<AppUpdateService>().Start();
    }

    /// <summary>Só no Debug: --demo-console enche o console com linhas típicas (pra ver as explicações sem instalar).</summary>
    [Conditional("DEBUG")]
    private static void DemoConsole(IToolConsole console)
    {
      if (!Environment.GetCommandLineArgs().Contains("--demo-console"))
        return;
      console.Write("git", "$ git fetch --progress --depth 1 origin main");
      console.Write("git", "error: RPC failed; curl 56 Recv failure: Connection was reset");
      console.Write("git", "fatal: early EOF");
      console.Write("dl-cli", "pedindo o link de https://www.moddb.com/addons/start/222467");
      console.Write("dl-cli", "https://cdn.dbolical.com/cache/mods/1/addons/Groks_main_menu_theme.zip");
      console.Write("curl", "baixando https://cdn.dbolical.com/cache/mods/1/addons/Groks_main_menu_theme.zip");
      console.Write("curl", "curl: (22) The requested URL returned error: 403");
      console.Write("7z", "extraindo Soundscape_Overhaul_3.0.zip");
      console.Write("7z", "ERROR: Headers Error : D:\\GAMMA\\downloads\\Soundscape_Overhaul_3.0.zip");
    }

    private void ListenForSecondInstance(Window window)
    {
      _wake = new EventWaitHandle(false, EventResetMode.AutoReset, InstanceName + "-wake");
      var thread = new Thread(() =>
      {
        while (_wake.WaitOne())
        {
          Dispatcher.InvokeAsync(() =>
          {
            if (window.WindowState == WindowState.Minimized)
              window.WindowState = WindowState.Normal;
            window.Activate();
          });
        }
      })
      { IsBackground = true, Name = "Provodnik.Wake" };
      thread.Start();
    }

    /// <summary>Reabre como administrador: solta a instância única pra nova pegar e sai. Falso se o UAC foi recusado.</summary>
    /// <summary>
    /// O idioma escolhido (ou o do Windows) pra todo texto e pros números e datas. Antes de criar qualquer tela: trocar
    /// com o app aberto reabre (<see cref="Restart"/>).
    /// </summary>
    private static void ApplyLanguage(AppSettings settings)
    {
      L.Use(settings.Language ?? L.SystemLanguage);
      CultureInfo.DefaultThreadCurrentCulture = L.Culture;
      CultureInfo.DefaultThreadCurrentUICulture = L.Culture;
      CultureInfo.CurrentCulture = L.Culture;
      CultureInfo.CurrentUICulture = L.Culture;
      FrameworkElement.LanguageProperty.OverrideMetadata(
        typeof(FrameworkElement), new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(L.Culture.IetfLanguageTag)));
    }

    /// <summary>
    /// Fecha e abre de novo, com os mesmos argumentos (a simulação continua simulação, sem recomeçar do zero) mais os
    /// <paramref name="extra"/>: pra trocar de idioma. Solta a instância única antes, senão a nova só acordaria esta e
    /// sairia.
    /// </summary>
    public void Restart(params string[] extra)
    {
      _instance?.ReleaseMutex();
      _instance?.Dispose();
      _instance = null;
      var args = Environment.GetCommandLineArgs().Skip(1)
        .Where(a => a != "--demo-novo" && a != Shell.MainWindow.ReopenSettings && !a.StartsWith(Shell.MainWindow.BoundsArg, StringComparison.Ordinal))
        .Concat(extra);
      Process.Start(new ProcessStartInfo(Environment.ProcessPath!, args) { UseShellExecute = false })?.Dispose();
      // a versão baixada fica pra quando fechar de vez: o Update.exe trocaria os arquivos com a nova já rodando
      _elevating = true;
      Shutdown();
    }

    public bool RestartElevated()
    {
      _instance?.ReleaseMutex();
      _instance?.Dispose();
      _instance = null;
      try
      {
        Process.Start(new ProcessStartInfo(Environment.ProcessPath!) { Verb = "runas", UseShellExecute = true })?.Dispose();
      }
      catch (System.ComponentModel.Win32Exception)
      {
        _instance = new Mutex(true, InstanceName, out _);
        return false;
      }
      _elevating = true;
      Shutdown();
      return true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
      // versão nova baixada entra quando o processo terminar de sair; reabrindo como administrador não, que o
      // Update.exe trocaria os arquivos com a outra instância já rodando
      if (!_elevating)
        _services?.GetService<AppUpdateService>()?.ApplyOnExit();
      _services?.Dispose();
      _instance?.Dispose();
      base.OnExit(e);
    }

    /// <summary>Serviços e shell são Singleton (uma janela só). As telas também: cada aba guarda o que estava aberto.</summary>
    private static ServiceProvider ConfigureServices(AppSettings settings, DemoWorld? demo)
    {
      var services = new ServiceCollection();
      var agent = $"Provodnik/{AppInfo.Version} (+https://github.com/ruan-luidy/Provodnik)";

      // consultas pequenas (lista, versão) e downloads grandes em clientes separados: o de download não descomprime
      // nem tem tempo-limite total (um arquivo de 4 GB leva o tempo que levar)
      var api = new HttpClient(new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.All, ConnectTimeout = TimeSpan.FromSeconds(30) }) { Timeout = TimeSpan.FromSeconds(60) };
      api.DefaultRequestHeaders.UserAgent.ParseAdd(agent);
      var download = new HttpClient(new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.None, ConnectTimeout = TimeSpan.FromSeconds(30) }) { Timeout = Timeout.InfiniteTimeSpan };
      download.DefaultRequestHeaders.UserAgent.ParseAdd(agent);

      services.AddSingleton(settings);
      services.AddSingleton(new AppOptions(demo is not null, demo?.Root));
      services.AddSingleton(new SettingsStore(settings));
      services.AddSingleton<Navigator>();
      services.AddSingleton<LogBook>();
      services.AddSingleton<GammaSession>();
      services.AddSingleton<ToolConsole>();
      services.AddSingleton<IToolConsole>(sp => sp.GetRequiredService<ToolConsole>());
      services.AddSingleton<ISystemInfo, SystemInfo>();
      services.AddSingleton<ElevationService>();

      // as ferramentas moram na pasta do GAMMA, que pode mudar nas configurações: o caminho é lido na hora de usar
      static GammaLayout Layout(IServiceProvider provider) =>
        provider.GetRequiredService<GammaSession>().Layout ?? throw new InvalidOperationException(L.T("shell.gammaNotChosen"));

      services.AddSingleton<IGammaApi>(new GammaApi(api));
      services.AddSingleton<IGitClient>(sp => new GitClient(() => Layout(sp).Git, sp.GetRequiredService<IToolConsole>()));
      services.AddSingleton(sp => new CurlDownloader(() =>
      {
        var layout = Layout(sp);
        return new CurlPaths(layout.Curl, layout.CurlConfig, layout.CurlHeader);
      }, sp.GetRequiredService<IToolConsole>()));
      services.AddSingleton(new HttpDownloader(download));
      services.AddSingleton<IFileDownloader, FileDownloader>();
      // o link do ModDB pelo navegador (WebView2); sem ele, o stalker-gamma-dl-cli do GAMMA
      services.AddSingleton(sp => new ModDbResolver(() => Layout(sp).ModDbCli, sp.GetRequiredService<IToolConsole>()));
      services.AddSingleton<IModDbResolver>(sp => new ModDbBrowser(sp.GetRequiredService<ModDbResolver>(), sp.GetRequiredService<IToolConsole>()));
      services.AddSingleton<IArchiveExtractor>(sp => new SevenZipExtractor(() => Layout(sp).SevenZip, sp.GetRequiredService<IToolConsole>()));
      services.AddSingleton<IShortcutWriter, ShortcutWriter>();
      if (demo is not null)
      {
        // a simulação troca as ferramentas de verdade pelas de mentira (o último registro é o que vale)
        services.AddSingleton(demo);
        services.AddSingleton<IGammaApi>(new DemoTools.Api(demo));
        services.AddSingleton<IGitClient>(sp => new DemoTools.Git(demo, sp.GetRequiredService<IToolConsole>()));
        services.AddSingleton<IModDbResolver>(sp => new DemoTools.Resolver(sp.GetRequiredService<IToolConsole>()));
        services.AddSingleton<IFileDownloader>(sp => new DemoTools.Downloader(sp.GetRequiredService<IToolConsole>()));
        services.AddSingleton<IArchiveExtractor>(sp => new DemoTools.Extractor(demo, sp.GetRequiredService<IToolConsole>()));
        services.AddSingleton<IShortcutWriter, DemoTools.Shortcuts>();
        services.AddSingleton<ISystemInfo>(new DemoTools.System(new SystemInfo()));
      }
      services.AddSingleton<InstallPipeline>();
      services.AddSingleton<UpdateChecker>();
      services.AddSingleton<InstallService>();
      services.AddSingleton<AppUpdateService>();

      services.AddSingleton<HomeViewModel>();
      services.AddSingleton<InstallViewModel>();
      services.AddSingleton<ModsViewModel>();
      services.AddSingleton<LogsViewModel>();
      services.AddSingleton<MainViewModel>();
      services.AddSingleton<MainWindow>();

      return services.BuildServiceProvider();
    }

    /// <summary>
    /// Erro que escapou de um handler de UI: vai inteiro pro errors.log e vira um diálogo com o resumo e "ver
    /// detalhes". Erro enquanto o diálogo de erro está aberto só vai pro log (não empilha caixas).
    /// </summary>
    private async void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
      e.Handled = true;
      AppLog.Error(e.Exception, "UI");

      if (_showingError)
        return;

      _showingError = true;
      try
      {
        await DialogService.ErrorAsync(L.T("shell.error.title"), e.Exception.GetBaseException().Message, e.Exception.ToString());
      }
      finally
      {
        _showingError = false;
      }
    }
  }
}
