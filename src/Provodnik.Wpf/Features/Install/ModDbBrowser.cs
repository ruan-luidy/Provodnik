using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Provodnik.Core.Diagnostics;
using Provodnik.Core.Downloads;
using Provodnik.Core.Text;
using Provodnik.Wpf.Shared.Services;

namespace Provodnik.Wpf.Features.Install
{
  /// <summary>
  /// O link do ModDB pelo navegador de verdade (WebView2, o Edge do Windows). O ModDB passou a pôr o Cloudflare
  /// na página "addons/start/N" e só navegador passa: o stalker-gamma-dl-cli que vem no GAMMA responde "Failed to
  /// generate download link" pra todo mundo, e o curl-impersonate leva o "Just a moment...".
  ///
  /// O caminho: abre a página de start e deixa ela carregar inteira, com a contagem e os anúncios, como num navegador
  /// comum (o ModDB pede que o site não seja contornado); quando a contagem acaba, o próprio ModDB redireciona pro CDN
  /// (fmtN.dl.dbolical.com), o navegador começa a baixar e aqui o download é cancelado e o link vai pro curl (que
  /// continua de onde parou e confere o md5). Da página só se lê o 429 e o "temporarily blocked", pra esfriar a fila.
  /// A página aparece no cartão "Baixando do ModDB" da tela de Instalação quando a pessoa abre ele (<see cref="Attach"/>);
  /// fechado, ou sem cartão na tela, ela fica na janela própria, fora da vista (no cartão fechado o navegador acharia que
  /// a página está escondida). Os cookies ficam em %LocalAppData%\Provodnik\webview: o Cloudflare passa uma vez e vale
  /// pros próximos. Desafio que pede clique ("confirme que é humano"): o cartão abre a página (<see cref="Attention"/>)
  /// ou, sem cartão, a janela aparece por cima pra pessoa clicar e some depois.
  /// Sem WebView2 instalado, usa o resolvedor de reserva (o dl-cli).
  ///
  /// Serve também pra instalação do zero: a página de start de um download do ModDB (o Anomaly), que segue o mesmo
  /// caminho, e a página de um arquivo do Mediafire (o RC3), que também tem Cloudflare: quando ela termina de
  /// carregar, o botão de download é clicado aqui e o download que o navegador começa vira o link.
  /// </summary>
  public sealed class ModDbBrowser(IModDbResolver fallback, IToolConsole console) : IModDbResolver, IDisposable
  {
    // a página inteira leva uns 50 s (medido: os anúncios terminam de carregar em ~45 s e só aí a contagem de 5 s
    // começa); o desafio do Cloudflare é visto pelo quadro dele, e sem nada disso a janela aparece depois de 2 min
    private static readonly TimeSpan ChallengeGrace = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan Patience = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan Limit = TimeSpan.FromMinutes(4);

    private readonly SemaphoreSlim _gate = new(1);
    private Window? _window;
    private WebView2? _web;
    private CoreWebView2? _core;
    private TaskCompletionSource<string>? _pending;
    private ContentControl? _host;
    private bool _revealed;
    private bool _challenged;
    private bool? _available;

    /// <summary>O desafio do Cloudflare pede um clique: o cartão na tela mostra a página e devolve true; sem ninguém, a janela aparece.</summary>
    public Func<bool>? Attention { get; set; }

    /// <summary>Mostra a página do ModDB dentro de <paramref name="host"/> (no thread da interface).</summary>
    public void Attach(ContentControl host)
    {
      _host = host;
      Place();
    }

    /// <summary>O card saiu da tela: a página volta pra janela própria.</summary>
    public void Detach(ContentControl host)
    {
      if (_host != host)
        return;
      _host = null;
      Place();
    }

    private void Place()
    {
      if (_web is null)
        return;
      if (_host is { } host)
      {
        if (_window?.Content == _web)
          _window.Content = null;
        host.Content = _web;
      }
      else
      {
        if (_web.Parent is ContentControl owner && owner != _window)
          owner.Content = null;
        if (_window is not null)
          _window.Content = _web;
      }
    }

    /// <summary>Tem o WebView2 Runtime no Windows (o Windows 11 já vem com ele).</summary>
    public static string? RuntimeVersion
    {
      get
      {
        try
        {
          return CoreWebView2Environment.GetAvailableBrowserVersionString();
        }
        catch (Exception ex) when (ex is WebView2RuntimeNotFoundException or FileNotFoundException)
        {
          return null;
        }
      }
    }

    public async Task<string> ResolveAsync(string addonUrl, CancellationToken cancellationToken)
    {
      _available ??= RuntimeVersion is not null;
      if (_available == false)
        return await fallback.ResolveAsync(addonUrl, cancellationToken).ConfigureAwait(false);

      await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
      try
      {
        var dispatcher = Application.Current.Dispatcher;
        var link = await await dispatcher.InvokeAsync(() => StartAsync(addonUrl, cancellationToken));
        return link;
      }
      finally
      {
        _gate.Release();
      }
    }

    private async Task<string> StartAsync(string addonUrl, CancellationToken cancellationToken)
    {
      await EnsureAsync();
      var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
      _pending = pending;
      _revealed = false;
      _challenged = false;
      console.Write("moddb", L.T("install.moddb.requesting", addonUrl));
      _core!.Navigate(addonUrl);

      using var cancel = cancellationToken.Register(() => pending.TrySetCanceled(cancellationToken));
      var clock = DateTime.UtcNow;
      while (!pending.Task.IsCompleted)
      {
        await Task.WhenAny(pending.Task, Task.Delay(500, CancellationToken.None));
        var waited = DateTime.UtcNow - clock;
        if (pending.Task.IsCompleted)
          break;
        // o desafio do Cloudflare que não passou sozinho (pede clique), ou passou muito do normal: a pessoa vê e resolve
        if (!_revealed && (_challenged && waited > ChallengeGrace || waited > Patience))
        {
          _revealed = true;
          Reveal();
        }
        if (waited > Limit)
          pending.TrySetException(new DownloadException(L.T("install.moddb.timeout")));
      }

      Conceal();
      _pending = null;
      return await pending.Task;
    }

    private async Task EnsureAsync()
    {
      if (_core is not null)
        return;

      _web = new WebView2();
      _window = new Window
      {
        Title = L.T("install.moddb.windowTitle"),
        Width = 900,
        Height = 650,
        ShowInTaskbar = false,
        ShowActivated = false,
        WindowStartupLocation = WindowStartupLocation.Manual,
        Left = -32000,
        Top = -32000,
        Content = _web,
      };
      // fechar no X só esconde: o navegador continua servindo os próximos links
      _window.Closing += (_, e) =>
      {
        if (Application.Current.MainWindow is not { IsVisible: true })
          return;
        e.Cancel = true;
        Conceal();
      };
      _window.Show();

      var environment = await CoreWebView2Environment.CreateAsync(null, Path.Combine(AppPaths.Root, "webview"));
      await _web.EnsureCoreWebView2Async(environment);
      _core = _web.CoreWebView2;
      _core.Settings.AreDefaultContextMenusEnabled = false;
      _core.Settings.IsStatusBarEnabled = false;
      _core.WebResourceResponseReceived += OnResponse;
      _core.DownloadStarting += OnDownload;
      _core.NavigationCompleted += OnLoaded;
      // propaganda que abre janela nova (o Mediafire abre uma no clique) não abre
      _core.NewWindowRequested += (_, e) => e.Handled = true;
      _core.FrameNavigationStarting += (_, e) =>
      {
        if (e.Uri.Contains("challenges.cloudflare.com", StringComparison.OrdinalIgnoreCase))
          _challenged = true;
      };
      _core.DocumentTitleChanged += (_, _) =>
      {
        if (_core.DocumentTitle.StartsWith("Just a moment", StringComparison.OrdinalIgnoreCase))
          _challenged = true;
      };
      Place();
    }

    private static bool IsModDbStart(string uri) =>
      uri.Contains("/addons/start/", StringComparison.OrdinalIgnoreCase) || uri.Contains("/downloads/start/", StringComparison.OrdinalIgnoreCase);

    /// <summary>A página do arquivo do Mediafire carregou (o Cloudflare já passou): clica no botão de download.</summary>
    private async void OnLoaded(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
      if (_pending is null || !e.IsSuccess || _core is null || !_core.Source.Contains("mediafire.com/file/", StringComparison.OrdinalIgnoreCase))
        return;
      try
      {
        await _core.ExecuteScriptAsync(
          "(() => { const b = document.getElementById('downloadButton') || document.querySelector('a[aria-label=\"Download file\"]'); if (b) b.click(); })()");
      }
      catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
      {
        // sem o botão: a janela aparece depois da paciência e a pessoa clica
      }
    }

    /// <summary>A resposta da página de start: só o bloqueio interessa, o resto a página faz sozinha.</summary>
    private async void OnResponse(object? sender, CoreWebView2WebResourceResponseReceivedEventArgs e)
    {
      if (_pending is not { } pending || !IsModDbStart(e.Request.Uri))
        return;
      try
      {
        if (e.Response.StatusCode == 429)
        {
          pending.TrySetException(new DownloadException(L.T("install.moddb.tooManyRequests")));
          return;
        }
        if (e.Response.StatusCode != 200)
          return; // 403 do Cloudflare: o desafio roda e a página volta sozinha

        using var stream = await e.Response.GetContentAsync();
        if (stream is null)
          return;
        var html = await new StreamReader(stream).ReadToEndAsync();
        if (html.Contains("temporarily blocked", StringComparison.OrdinalIgnoreCase))
          pending.TrySetException(new DownloadException(L.T("install.moddb.blockedIp")));
      }
      catch (Exception ex) when (ex is IOException or InvalidOperationException or System.Runtime.InteropServices.COMException)
      {
        // sem o corpo: a página segue e o tempo limite pega um bloqueio que não deu pra ler
      }
    }

    /// <summary>O ModDB entregou o arquivo: o link do CDN é o que interessa, o download fica com o curl.</summary>
    private void OnDownload(object? sender, CoreWebView2DownloadStartingEventArgs e)
    {
      e.Cancel = true;
      var link = e.DownloadOperation.Uri;
      console.Write("moddb", $"link: {link}");
      _pending?.TrySetResult(link);
    }

    private bool IsVisibleToUser => _window is { Left: > -10000 };

    private void Reveal()
    {
      if (_window is null)
        return;
      console.Write("moddb", L.T("install.moddb.challenge.console"));
      ToastService.Warning(L.T("install.moddb.challenge.toast"), "ModDB");
      if (Attention?.Invoke() == true)
        return;
      var area = SystemParameters.WorkArea;
      _window.Left = area.Left + (area.Width - _window.Width) / 2;
      _window.Top = area.Top + (area.Height - _window.Height) / 2;
      _window.ShowInTaskbar = true;
      _window.Topmost = true;
      _window.Activate();
    }

    private void Conceal()
    {
      if (_window is null || !IsVisibleToUser)
        return;
      _window.Topmost = false;
      _window.ShowInTaskbar = false;
      _window.Left = -32000;
      _window.Top = -32000;
    }

    public void Dispose()
    {
      _web?.Dispose();
      _gate.Dispose();
    }
  }
}
