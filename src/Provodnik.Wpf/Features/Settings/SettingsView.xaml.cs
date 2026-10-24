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

    private void Save_Click(object sender, RoutedEventArgs e)
    {
      if (((SettingsViewModel)DataContext).Save())
        Closed?.Invoke(this, true);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Closed?.Invoke(this, false);
  }
}
