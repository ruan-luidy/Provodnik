using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Provodnik.Wpf.Shared.Controls
{
  /// <summary>Estado vazio de painel: ícone + recado + ação opcional. O visual está no XAML.</summary>
  public partial class EmptyState : UserControl
  {
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
      nameof(Icon), typeof(Geometry), typeof(EmptyState));

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
      nameof(Text), typeof(string), typeof(EmptyState));

    public static readonly DependencyProperty ActionTextProperty = DependencyProperty.Register(
      nameof(ActionText), typeof(string), typeof(EmptyState));

    public static readonly DependencyProperty ActionCommandProperty = DependencyProperty.Register(
      nameof(ActionCommand), typeof(ICommand), typeof(EmptyState));

    public EmptyState()
    {
      InitializeComponent();
    }

    public Geometry? Icon
    {
      get => (Geometry?)GetValue(IconProperty);
      set => SetValue(IconProperty, value);
    }

    public string? Text
    {
      get => (string?)GetValue(TextProperty);
      set => SetValue(TextProperty, value);
    }

    /// <summary>Rótulo do botão de ação; sem ele o botão não aparece.</summary>
    public string? ActionText
    {
      get => (string?)GetValue(ActionTextProperty);
      set => SetValue(ActionTextProperty, value);
    }

    public ICommand? ActionCommand
    {
      get => (ICommand?)GetValue(ActionCommandProperty);
      set => SetValue(ActionCommandProperty, value);
    }
  }
}
