using System.Windows;
using Provodnik.Wpf.Shared.Services;

namespace Provodnik.Wpf.Features.Install
{
  public partial class ConsoleWindow : HandyControl.Controls.Window
  {
    private readonly ToolConsole _console;

    public ConsoleWindow(ToolConsole console)
    {
      InitializeComponent();
      _console = console;
      Panel.Lines = console.Lines;
    }

    private void Pin_Changed(object sender, RoutedEventArgs e) => Topmost = Pin.IsChecked == true;

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
      Clipboard.SetText(string.Join(Environment.NewLine, _console.Lines.Select(l => $"{l.TimeText} [{l.Tool}] {l.Text}")));
      ToastService.Success($"{_console.Lines.Count} linhas copiadas");
    }
  }
}
