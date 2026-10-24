using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Provodnik.Wpf.Shared.Controls
{
  /// <summary>Erro de painel: recado + "Tentar de novo". Fica escondido enquanto não há texto. O visual está no XAML.</summary>
  public partial class ErrorState : UserControl
  {
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
      nameof(Text), typeof(string), typeof(ErrorState), new PropertyMetadata(null, OnTextChanged));

    public static readonly DependencyProperty DetailProperty = DependencyProperty.Register(
      nameof(Detail), typeof(string), typeof(ErrorState), new PropertyMetadata(null, OnTextChanged));

    public static readonly DependencyProperty RetryCommandProperty = DependencyProperty.Register(
      nameof(RetryCommand), typeof(ICommand), typeof(ErrorState), new PropertyMetadata(null, OnRetryCommandChanged));

    private static readonly DependencyPropertyKey ToolTipTextKey = DependencyProperty.RegisterReadOnly(
      nameof(ToolTipText), typeof(string), typeof(ErrorState), new PropertyMetadata(null));

    public static readonly DependencyProperty ToolTipTextProperty = ToolTipTextKey.DependencyProperty;

    public ErrorState()
    {
      InitializeComponent();
      RetryButton.Visibility = Visibility.Collapsed;
    }

    /// <summary>O recado curto que aparece no painel.</summary>
    public string? Text
    {
      get => (string?)GetValue(TextProperty);
      set => SetValue(TextProperty, value);
    }

    /// <summary>O erro inteiro, pro tooltip. Sem ele o tooltip repete o Text.</summary>
    public string? Detail
    {
      get => (string?)GetValue(DetailProperty);
      set => SetValue(DetailProperty, value);
    }

    /// <summary>O que o "Tentar de novo" faz; sem comando o botão não aparece.</summary>
    public ICommand? RetryCommand
    {
      get => (ICommand?)GetValue(RetryCommandProperty);
      set => SetValue(RetryCommandProperty, value);
    }

    public string? ToolTipText => (string?)GetValue(ToolTipTextProperty);

    private static void OnTextChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
      var state = (ErrorState)target;
      state.Visibility = string.IsNullOrEmpty(state.Text) ? Visibility.Collapsed : Visibility.Visible;
      state.SetValue(ToolTipTextKey, string.IsNullOrEmpty(state.Detail) ? state.Text : state.Detail);
    }

    private static void OnRetryCommandChanged(DependencyObject target, DependencyPropertyChangedEventArgs e) =>
      ((ErrorState)target).RetryButton.Visibility = e.NewValue is null ? Visibility.Collapsed : Visibility.Visible;
  }
}
