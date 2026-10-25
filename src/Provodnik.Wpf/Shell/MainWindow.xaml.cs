using System.ComponentModel;
using HandyControl.Data;
using HandyControl.Tools;
using Provodnik.Core.Text;
using Provodnik.Wpf.Features.Install;
using Provodnik.Wpf.Shared.Controls;
using Provodnik.Wpf.Shared.Services;

namespace Provodnik.Wpf.Shell
{
  public partial class MainWindow : HandyControl.Controls.Window
  {
    /// <summary>Reaberto pra trocar de idioma: abre as Configurações de novo, onde a pessoa estava.</summary>
    public const string ReopenSettings = "--settings";

    /// <summary>"--bounds=x,y,largura,altura,maximizada": a janela volta no mesmo lugar depois de reabrir.</summary>
    public const string BoundsArg = "--bounds=";

    private readonly InstallService _install;
    private readonly SettingsStore _settings;
    private bool _closing;

    public MainWindow(MainViewModel viewModel, InstallService install, SettingsStore settings, AppOptions options)
    {
      InitializeComponent();
      DataContext = viewModel;
      _install = install;
      _settings = settings;
      settings.Changed += (_, _) => ApplyBackdrop();
      ApplyBackdrop();
      // diálogo aberto: a barra de título escurece igual ao resto (a máscara do HandyControl não chega nela)
      DialogService.OpenChanged += (_, _) =>
      {
        if (DialogService.IsOpen)
          SetResourceReference(NonClientAreaBackgroundProperty, "DarkOpacityBrush");
        else
          NonClientAreaBackground = System.Windows.Media.Brushes.Transparent;
      };
      ApplyStartupBounds();
      Loaded += async (_, _) =>
      {
        if (Environment.GetCommandLineArgs().Contains(ReopenSettings))
          _ = Dispatcher.InvokeAsync(() => viewModel.OpenSettingsCommand.Execute(null), System.Windows.Threading.DispatcherPriority.Background);
        await viewModel.StartAsync();
        if (options.ShotsFolder is { } shots)
          await Features.Demo.ScreenshotTour.RunAsync(this, viewModel, install, shots);
      };
      install.Finished += (_, report) =>
      {
        // terminou com a janela atrás de outra (o jogo, o navegador): o aviso vai pra área de trabalho
        var desktop = !IsActive;
        if (report.Cancelled)
          return;
        if (report.Succeeded)
          ToastService.Show(DialogKind.Success, L.T("shell.done.title"), L.T("shell.done.message", report.Installed, report.UpToDate), TimeSpan.FromSeconds(8), desktop: desktop);
        else
          ToastService.Show(DialogKind.Warning, L.T("shell.failed.title"), L.T("shell.failed.message", report.Failures.Count), TimeSpan.FromSeconds(10), desktop: desktop);
      };
    }

    /// <summary>O argumento com o lugar e o tamanho da janela agora, pra reabrir igual.</summary>
    public string BoundsArgument()
    {
      var r = WindowState == System.Windows.WindowState.Normal ? new System.Windows.Rect(Left, Top, Width, Height) : RestoreBounds;
      var culture = System.Globalization.CultureInfo.InvariantCulture;
      return string.Create(culture, $"{BoundsArg}{r.X:0},{r.Y:0},{r.Width:0},{r.Height:0},{(WindowState == System.Windows.WindowState.Maximized ? 1 : 0)}");
    }

    private void ApplyStartupBounds()
    {
      var arg = Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith(BoundsArg, StringComparison.Ordinal));
      var parts = arg?[BoundsArg.Length..].Split(',');
      if (parts is not { Length: 5 } || !parts.Take(4).All(p => double.TryParse(p, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _)))
        return;
      var v = parts.Take(4).Select(p => double.Parse(p, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
      WindowStartupLocation = System.Windows.WindowStartupLocation.Manual;
      (Left, Top, Width, Height) = (v[0], v[1], v[2], v[3]);
      if (parts[4] == "1")
        WindowState = System.Windows.WindowState.Maximized;
    }

    /// <summary>
    /// Fundo Mica do Windows 11 pelo próprio HandyControl, como na Связь (SystemBackdropType: DWMWA_SYSTEMBACKDROP_TYPE
    /// + modo escuro do DWM + fundo transparente). Sem suporte (Windows 10) ou desligado nas configurações, volta pro
    /// cinza sólido.
    /// </summary>
    private void ApplyBackdrop()
    {
      if (_settings.Current.Mica && BackdropType.Mica.IsSupported())
      {
        if (SystemBackdropType != BackdropType.Mica)
          SystemBackdropType = BackdropType.Mica;
        return;
      }
      if (SystemBackdropType == BackdropType.Mica)
        SystemBackdropType = BackdropType.Disable;
      SetResourceReference(BackgroundProperty, "RegionBrush");
    }

    /// <summary>Fechar no meio de uma instalação pergunta; confirmando, cancela e fecha quando ela parar.</summary>
    protected override async void OnClosing(CancelEventArgs e)
    {
      if (_closing || !_install.IsRunning)
      {
        base.OnClosing(e);
        return;
      }

      e.Cancel = true;
      if (!await DialogService.ConfirmAsync(L.T("shell.close.title"), L.T("shell.close.message"), L.T("shell.close.stop"), L.T("shell.close.continue")))
        return;

      _closing = true;
      _install.Finished += (_, _) => Close();
      _install.Cancel();
    }
  }
}
