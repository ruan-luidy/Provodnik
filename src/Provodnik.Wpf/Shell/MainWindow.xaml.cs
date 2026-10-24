using System.ComponentModel;
using HandyControl.Data;
using HandyControl.Tools;
using Provodnik.Wpf.Features.Install;
using Provodnik.Wpf.Shared.Controls;
using Provodnik.Wpf.Shared.Services;

namespace Provodnik.Wpf.Shell
{
  public partial class MainWindow : HandyControl.Controls.Window
  {
    private readonly InstallService _install;
    private readonly SettingsStore _settings;
    private bool _closing;

    public MainWindow(MainViewModel viewModel, InstallService install, SettingsStore settings)
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
      Loaded += async (_, _) => await viewModel.StartAsync();
      install.Finished += (_, report) =>
      {
        // terminou com a janela atrás de outra (o jogo, o navegador): o aviso vai pra área de trabalho
        var desktop = !IsActive;
        if (report.Cancelled)
          return;
        if (report.Succeeded)
          ToastService.Show(DialogKind.Success, "GAMMA pronto", $"{report.Installed} instalados, {report.UpToDate} já em dia. É só jogar.", TimeSpan.FromSeconds(8), desktop: desktop);
        else
          ToastService.Show(DialogKind.Warning, "Terminou com falhas", $"{report.Failures.Count} item(ns) não entraram. Veja na tela de instalação e use \"Tentar de novo\".", TimeSpan.FromSeconds(10), desktop: desktop);
      };
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
      if (!await DialogService.ConfirmAsync("Parar a instalação?", "O que já foi baixado e extraído fica. Da próxima vez ela continua de onde parou.", "Parar e sair", "Continuar instalando"))
        return;

      _closing = true;
      _install.Finished += (_, _) => Close();
      _install.Cancel();
    }
  }
}
