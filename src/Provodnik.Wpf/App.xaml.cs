using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Provodnik.Core.Archives;
using Provodnik.Core.Desktop;
using Provodnik.Core.Downloads;
using Provodnik.Core.Layout;
using Provodnik.Core.Remote;
using Provodnik.Core.Repos;
using Provodnik.Features.Install;
using Provodnik.Features.Update;
using Provodnik.Infrastructure.Desktop;
using Provodnik.Infrastructure.Http;
using Provodnik.Infrastructure.Tools;
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
    private const string InstanceName = "Provodnik-5d2a91c4";

    private ServiceProvider? _services;
    private Mutex? _instance;
    private EventWaitHandle? _wake;
    private bool _showingError;

    protected override void OnStartup(StartupEventArgs e)
    {
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
      _services = ConfigureServices(settings);
      ThemeService.Initialize(_services.GetRequiredService<SettingsStore>());

      var window = _services.GetRequiredService<MainWindow>();
      MainWindow = window;
      ListenForSecondInstance(window);
      window.Show();
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

    protected override void OnExit(ExitEventArgs e)
    {
      _services?.Dispose();
      _instance?.Dispose();
      base.OnExit(e);
    }

    /// <summary>Serviços e shell são Singleton (uma janela só). As telas também: cada aba guarda o que estava aberto.</summary>
    private static ServiceProvider ConfigureServices(AppSettings settings)
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
      services.AddSingleton(new SettingsStore(settings));
      services.AddSingleton<Navigator>();
      services.AddSingleton<LogBook>();
      services.AddSingleton<GammaSession>();

      // as ferramentas moram na pasta do GAMMA, que pode mudar nas configurações: o caminho é lido na hora de usar
      static GammaLayout Layout(IServiceProvider provider) =>
        provider.GetRequiredService<GammaSession>().Layout ?? throw new InvalidOperationException("A pasta do GAMMA não foi escolhida.");

      services.AddSingleton<IGammaApi>(new GammaApi(api));
      services.AddSingleton<IGitClient>(sp => new GitClient(() => Layout(sp).Git));
      services.AddSingleton(sp => new CurlDownloader(() =>
      {
        var layout = Layout(sp);
        return new CurlPaths(layout.Curl, layout.CurlConfig, layout.CurlHeader);
      }));
      services.AddSingleton(new HttpDownloader(download));
      services.AddSingleton<IFileDownloader, FileDownloader>();
      services.AddSingleton<IModDbResolver>(sp => new ModDbResolver(() => Layout(sp).ModDbCli));
      services.AddSingleton<IArchiveExtractor>(sp => new SevenZipExtractor(() => Layout(sp).SevenZip));
      services.AddSingleton<IShortcutWriter, ShortcutWriter>();
      services.AddSingleton<InstallPipeline>();
      services.AddSingleton<UpdateChecker>();
      services.AddSingleton<InstallService>();

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
        await DialogService.ErrorAsync("Algo deu errado", e.Exception.GetBaseException().Message, e.Exception.ToString());
      }
      finally
      {
        _showingError = false;
      }
    }
  }
}
