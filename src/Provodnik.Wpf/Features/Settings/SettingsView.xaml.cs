using System.Windows;
using System.Windows.Controls;
using Provodnik.Wpf.Shared.Controls;

namespace Provodnik.Wpf.Features.Settings
{
  public partial class SettingsView : UserControl, IDialogContent
  {
    public SettingsView()
    {
      InitializeComponent();
    }

    public event EventHandler<bool>? Closed;

    // as opções já valem; o Pronto (e o X, e o Esc) só confere a pasta do GAMMA e fecha
    private void Done_Click(object sender, RoutedEventArgs e)
    {
      if (((SettingsViewModel)DataContext).Done())
        Closed?.Invoke(this, true);
    }
  }
}
