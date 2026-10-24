using System.Windows;
using System.Windows.Controls;

namespace Provodnik.Wpf.Shared.Controls
{
  /// <summary>O cabeçalho de toda página: título, uma linha do que ela faz e as ações à direita. O visual está no XAML.</summary>
  public partial class PageHeader : UserControl
  {
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
      nameof(Title), typeof(string), typeof(PageHeader));

    public static readonly DependencyProperty SubtitleProperty = DependencyProperty.Register(
      nameof(Subtitle), typeof(string), typeof(PageHeader));

    public static readonly DependencyProperty ActionsProperty = DependencyProperty.Register(
      nameof(Actions), typeof(object), typeof(PageHeader));

    public PageHeader()
    {
      InitializeComponent();
    }

    public string? Title
    {
      get => (string?)GetValue(TitleProperty);
      set => SetValue(TitleProperty, value);
    }

    public string? Subtitle
    {
      get => (string?)GetValue(SubtitleProperty);
      set => SetValue(SubtitleProperty, value);
    }

    /// <summary>Os botões da página, à direita do título.</summary>
    public object? Actions
    {
      get => GetValue(ActionsProperty);
      set => SetValue(ActionsProperty, value);
    }
  }
}
