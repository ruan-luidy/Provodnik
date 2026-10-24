using System.Windows;
using System.Windows.Controls;
using Provodnik.Wpf.Shared.Controls;

namespace Provodnik.Wpf.Features.About
{
  public partial class AboutView : UserControl, IDialogContent
  {
    public AboutView()
    {
      InitializeComponent();
    }

    public event EventHandler<bool>? Closed;

    private void Close_Click(object sender, RoutedEventArgs e) => Closed?.Invoke(this, true);
  }
}
