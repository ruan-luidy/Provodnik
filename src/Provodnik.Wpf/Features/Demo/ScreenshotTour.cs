using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Provodnik.Wpf.Features.Install;
using Provodnik.Wpf.Shared.Services;
using Provodnik.Wpf.Shell;

namespace Provodnik.Wpf.Features.Demo
{
  /// <summary>
  /// As capturas do README, tiradas pelo próprio app (Provodnik.exe --demo --shots pasta): sem o selo DEMO, cada tela
  /// renderizada em 2x (nítida em tela de alta densidade e no GitHub) sobre o fundo escuro do Mica. Começa uma
  /// instalação da simulação, fotografa no meio, espera acabar (tentando de novo as falhas) e passa por Início, Mods,
  /// Registro e Sobre; depois fecha.
  /// </summary>
  public static class ScreenshotTour
  {
    public const string Flag = "--shots";

    private const double Scale = 2;

    // o canto da janela do Windows 11 e a folga em volta pra caber a sombra
    private const double Radius = 8;
    private const double Padding = 32;

    // o cinza do Mica escuro atrás da janela (a captura não enxerga o papel de parede)
    private static readonly Color Backdrop = Color.FromRgb(0x20, 0x20, 0x22);

    /// <summary>A pasta das capturas, se pediram (só vale junto com a simulação).</summary>
    public static string? Folder(IReadOnlyList<string> args)
    {
      var at = args.ToList().IndexOf(Flag);
      return at >= 0 && at + 1 < args.Count ? args[at + 1] : null;
    }

    public static async Task RunAsync(MainWindow window, MainViewModel shell, InstallService install, string folder)
    {
      Directory.CreateDirectory(folder);
      await Task.Delay(3000);

      shell.Tab = Tab.Install;
      await Task.Delay(1500);
      if (shell.Current is InstallViewModel page && page.StartCommand.CanExecute(null))
        page.StartCommand.Execute(null);
      await Task.Delay(15000);
      Save(window, Path.Combine(folder, "install.png"));
      // espera a instalação acabar (o estado de quem instalou só é gravado no fim)
      var limit = DateTime.Now.AddMinutes(20);
      while (install.IsRunning && DateTime.Now < limit)
        await Task.Delay(1000);
      // as falhas de propósito da simulação voltam no "tentar de novo"; o Início sai com a instalação completa
      if (install.Failures.Count > 0)
        await install.RetryFailedAsync();
      // o aviso de fim some sozinho em uns 10 s
      await Task.Delay(11000);

      foreach (var (tab, name) in new[] { (Tab.Home, "home.png"), (Tab.Mods, "mods.png"), (Tab.Logs, "log.png") })
      {
        shell.Tab = tab;
        await Task.Delay(3000);
        Save(window, Path.Combine(folder, name));
      }

      shell.OpenAboutCommand.Execute(null);
      await Task.Delay(1500);
      Save(window, Path.Combine(folder, "about.png"));
      Application.Current.Shutdown();
    }

    private static void Save(Window window, string path)
    {
      var width = window.ActualWidth;
      var height = window.ActualHeight;
      var content = new RenderTargetBitmap((int)(width * Scale), (int)(height * Scale), 96 * Scale, 96 * Scale, PixelFormats.Pbgra32);
      content.Render(window);

      // a janela como o Windows 11 mostra: cantos redondos, contorno fino e sombra, sobre fundo transparente
      // (fica bem no tema claro e no escuro do GitHub)
      var corners = new CornerRadius(Radius);
      var window11 = new Border
      {
        Width = width,
        Height = height,
        CornerRadius = corners,
        Background = new SolidColorBrush(Backdrop),
        Effect = new DropShadowEffect { BlurRadius = 36, ShadowDepth = 8, Direction = 270, Opacity = 0.45, Color = Colors.Black },
        Child = new Grid
        {
          Children =
          {
            new Image { Source = content, Width = width, Height = height, Clip = new RectangleGeometry(new Rect(0, 0, width, height), Radius, Radius) },
            new Border { CornerRadius = corners, BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)) },
          },
        },
      };
      var page = new Grid { Margin = new Thickness(Padding), Children = { window11 } };
      var size = new Size(width + 2 * Padding, height + 2 * Padding);
      page.Measure(size);
      page.Arrange(new Rect(size));
      page.UpdateLayout();

      var image = new RenderTargetBitmap((int)(size.Width * Scale), (int)(size.Height * Scale), 96 * Scale, 96 * Scale, PixelFormats.Pbgra32);
      image.Render(page);

      var encoder = new PngBitmapEncoder();
      encoder.Frames.Add(BitmapFrame.Create(image));
      using var file = File.Create(path);
      encoder.Save(file);
    }
  }
}
