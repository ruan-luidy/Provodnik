using System.Collections.Specialized;
using System.Windows.Controls;

namespace Provodnik.Wpf.Features.Logs
{
  public partial class LogsView : UserControl
  {
    public LogsView()
    {
      InitializeComponent();
      // linha nova: desce pro fim, a não ser que a pessoa tenha subido pra ler
      ((INotifyCollectionChanged)List.Items).CollectionChanged += (_, e) =>
      {
        if (e.Action != NotifyCollectionChangedAction.Add || List.Items.Count == 0)
          return;
        var viewer = FindScroll(List);
        if (viewer is null || viewer.VerticalOffset >= viewer.ScrollableHeight - 2)
          List.ScrollIntoView(List.Items[^1]);
      };
    }

    private static ScrollViewer? FindScroll(System.Windows.DependencyObject node)
    {
      for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(node); i++)
      {
        var child = System.Windows.Media.VisualTreeHelper.GetChild(node, i);
        if (child is ScrollViewer viewer)
          return viewer;
        if (FindScroll(child) is { } inner)
          return inner;
      }
      return null;
    }
  }
}
