using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Provodnik.Wpf.Features.Install
{
  /// <summary>A lista do console; desce sozinha pro fim, a não ser que a pessoa tenha subido pra ler.</summary>
  public partial class ConsolePanel : UserControl
  {
    public static readonly DependencyProperty LinesProperty = DependencyProperty.Register(
      nameof(Lines), typeof(IEnumerable), typeof(ConsolePanel), new PropertyMetadata(null, (d, e) => ((ConsolePanel)d).List.ItemsSource = (IEnumerable?)e.NewValue));

    private ScrollViewer? _viewer;

    public ConsolePanel()
    {
      InitializeComponent();
      ((INotifyCollectionChanged)List.Items).CollectionChanged += OnLinesChanged;
      // voltou pra tela (ou o painel foi ligado): começa no fim, que é o que importa
      IsVisibleChanged += (_, _) =>
      {
        if (IsVisible && List.Items.Count > 0)
          Dispatcher.InvokeAsync(() => List.ScrollIntoView(List.Items[^1]), System.Windows.Threading.DispatcherPriority.Loaded);
      };
    }

    public IEnumerable? Lines
    {
      get => (IEnumerable?)GetValue(LinesProperty);
      set => SetValue(LinesProperty, value);
    }

    private void OnLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
      if (e.Action != NotifyCollectionChangedAction.Add || List.Items.Count == 0)
        return;
      _viewer ??= FindScroll(List);
      if (_viewer is null || _viewer.VerticalOffset >= _viewer.ScrollableHeight - 2)
        List.ScrollIntoView(List.Items[^1]);
    }

    private static ScrollViewer? FindScroll(DependencyObject node)
    {
      for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
      {
        var child = VisualTreeHelper.GetChild(node, i);
        if (child is ScrollViewer viewer)
          return viewer;
        if (FindScroll(child) is { } inner)
          return inner;
      }
      return null;
    }
  }
}
