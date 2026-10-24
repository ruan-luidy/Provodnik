using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Provodnik.Core.Diagnostics;
using Provodnik.Core.Downloads;
using Provodnik.Wpf.Shared.Services;

namespace Provodnik.Wpf.Features.Install
{
  /// <summary>
  /// O link do ModDB pelo navegador de verdade (WebView2, o Edge do Windows). O ModDB passou a pôr o Cloudflare
  /// na página "addons/start/N" e só navegador passa: o stalker-gamma-dl-cli que vem no GAMMA responde "Failed to
  /// generate download link" pra todo mundo, e o curl-impersonate leva o "Just a moment...".
  ///
  /// O caminho, medido: abre a página de start; assim que o HTML chega (antes de carregar anúncio, ~0,3 s) tira dele o
  /// "/downloads/mirror/..." e vai direto pra lá; o ModDB redireciona pro CDN (fmtN.dl.dbolical.com), o navegador
  /// começa a baixar e aqui o download é cancelado e o link vai pro curl (que continua de onde parou e confere o md5).
  /// Os cookies ficam em %LocalAppData%\Provodnik\webview: o Cloudflare passa uma vez e vale pros próximos.
  /// Desafio que pede clique ("confirme que é humano"): a janela aparece por cima pra pessoa clicar e some depois.
  /// Sem WebView2 instalado, usa o resolvedor de reserva (o dl-cli).
  /// </summary>
  public sealed partial class ModDbBrowser(IModDbResolver fallback, IToolConsole console) : IModDbResolver, IDisposable
  {
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan Limit = TimeSpan.FromMinutes(4);

    private readonly SemaphoreSlim _gate = new(1);
    private Window? _window;
    private WebView2? _web;
    private CoreWebView2? _core;
    private TaskCompletionSource<string>? _pending;
    private bool? _available;

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
      console.Write("moddb", $"pedindo o link de {addonUrl}");
      _core!.Navigate(addonUrl);

      using var cancel = cancellationToken.Register(() => pending.TrySetCanceled(cancellationToken));
      var clock = DateTime.UtcNow;
      while (!pending.Task.IsCompleted)
      {
        await Task.WhenAny(pending.Task, Task.Delay(500, CancellationToken.None));
        var waited = DateTime.UtcNow - clock;
        if (pending.Task.IsCompleted)
          break;
        // passou do normal: provavelmente o desafio do Cloudflare com clique; a pessoa vê e resolve
        if (waited > Patience && !IsVisibleToUser)
          Reveal();
        if (waited > Limit)
          pending.TrySetException(new DownloadException("O ModDB não respondeu a tempo (o desafio do Cloudflare não foi resolvido)."));
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
        Title = "ModDB · confirme que você é humano",
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
    }

    /// <summary>O HTML da página de start chegou: pula a contagem e os anúncios e vai direto pro espelho.</summary>
    private async void OnResponse(object? sender, CoreWebView2WebResourceResponseReceivedEventArgs e)
    {
      if (_pending is not { } pending || !e.Request.Uri.Contains("/addons/start/", StringComparison.OrdinalIgnoreCase))
        return;
      try
      {
        if (e.Response.StatusCode == 429)
        {
          pending.TrySetException(new DownloadException("O ModDB bloqueou temporariamente (429 too many requests)."));
          return;
        }
        if (e.Response.StatusCode != 200)
          return; // 403 do Cloudflare: o desafio roda e a página volta sozinha

        using var stream = await e.Response.GetContentAsync();
        if (stream is null)
          return;
        var html = await new StreamReader(stream).ReadToEndAsync();
        if (html.Contains("temporarily blocked", StringComparison.OrdinalIgnoreCase))
        {
          pending.TrySetException(new DownloadException("O ModDB bloqueou temporariamente este IP (temporarily blocked, 429)."));
          return;
        }
        if (MirrorLink().Match(html) is { Success: true } mirror)
          _core?.Navigate("https://www.moddb.com" + mirror.Value.Replace("&amp;", "&"));
      }
      catch (Exception ex) when (ex is IOException or InvalidOperationException or System.Runtime.InteropServices.COMException)
      {
        // sem o corpo: a própria página redireciona em alguns segundos
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
      console.Write("moddb", "o Cloudflare do ModDB quer confirmar que você é humano: clique na caixa da janela que abriu");
      ToastService.Warning("O ModDB quer confirmar que você é humano. Clique na caixa da janela que abriu; depois disso segue sozinho.", "ModDB");
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

    [GeneratedRegex(@"/downloads/mirror/[^""'<> ]+")]
    private static partial Regex MirrorLink();
  }
}
