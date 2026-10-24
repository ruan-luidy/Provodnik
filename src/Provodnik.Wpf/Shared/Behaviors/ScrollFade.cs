using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Provodnik.Wpf.Shared.Behaviors
{
  /// <summary>
  /// Lista com mais linhas do que cabe no painel: os 20 px da ponta que continua (embaixo, e em cima depois de rolar -
  /// menos na grade, que rola por linha e nunca corta a de cima)
  /// somem num degradê, em vez de a linha aparecer cortada ao meio (parecia erro de layout). Na ponta, ou com tudo
  /// cabendo, aquele lado volta ao normal. Vale pra
  /// DataGrid, ListBox ou ScrollViewer - o degradê vai só no conteúdo, o cabeçalho da tabela fica inteiro.
  ///   &lt;DataGrid behaviors:ScrollFade.IsEnabled="True" /&gt;
  /// </summary>
  public static class ScrollFade
  {
    private const double FadeHeight = 20;

    public static readonly DependencyProperty IsEnabledProperty =
      DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(ScrollFade), new PropertyMetadata(false, OnIsEnabledChanged));

    /// <summary>Já ligado nesse controle (Loaded roda de novo a cada troca de aba).</summary>
    private static readonly DependencyProperty HookedProperty =
      DependencyProperty.RegisterAttached("Hooked", typeof(bool), typeof(ScrollFade), new PropertyMetadata(false));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
      if (d is not FrameworkElement element || e.NewValue is not true)
        return;

      element.Loaded += (_, _) => Hook(element);
      // Lista que nasce escondida (o detalhe do Finder antes de escolher a peça) ainda não montou o template no Loaded:
      // tenta de novo quando ela aparece
      element.IsVisibleChanged += (_, _) =>
      {
        if (element.IsVisible)
          element.Dispatcher.InvokeAsync(() => Hook(element), System.Windows.Threading.DispatcherPriority.Loaded);
      };
      if (element.IsLoaded)
        Hook(element);
    }

    private static void Hook(FrameworkElement element)
    {
      if ((bool)element.GetValue(HookedProperty) || (element as ScrollViewer ?? Find<ScrollViewer>(element)) is not { } viewer)
        return;

      element.SetValue(HookedProperty, true);

      // A grade rola de linha em linha: a primeira linha sempre aparece inteira, só a de baixo corta. Degradê em cima
      // só nas listas que rolam por pixel
      var top = element is not DataGrid;
      viewer.ScrollChanged += (_, _) => Update(viewer, top);
      viewer.SizeChanged += (_, _) => Update(viewer, top);
      Update(viewer, top);
    }

    private static void Update(ScrollViewer viewer, bool top)
    {
      var target = viewer.Template?.FindName("PART_ScrollContentPresenter", viewer) as FrameworkElement ?? viewer;
      var height = target.ActualHeight;
      var below = viewer.ScrollableHeight > 0.5 && viewer.VerticalOffset < viewer.ScrollableHeight - 0.5;
      var above = top && viewer.VerticalOffset > 0.5;
      if (!(below || above) || height < FadeHeight * 3)
      {
        target.OpacityMask = null;
        return;
      }

      // Em pixels da parte visível (Absolute): o relativo mede o conteúdo inteiro, que é a lista toda rolada, e o
      // degradê ia parar fora da tela. Some nos 20 px da ponta que tem mais linha, opaco no resto
      var edge = FadeHeight / height;
      var fade = new LinearGradientBrush
      {
        MappingMode = BrushMappingMode.Absolute,
        StartPoint = new Point(0, 0),
        EndPoint = new Point(0, height),
      };
      fade.GradientStops.Add(new GradientStop(above ? Colors.Transparent : Colors.Black, 0));
      fade.GradientStops.Add(new GradientStop(Colors.Black, above ? edge : 0));
      fade.GradientStops.Add(new GradientStop(Colors.Black, below ? 1 - edge : 1));
      fade.GradientStops.Add(new GradientStop(below ? Colors.Transparent : Colors.Black, 1));
      fade.Freeze();
      target.OpacityMask = fade;
    }

    private static T? Find<T>(DependencyObject root)
      where T : DependencyObject
    {
      for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
      {
        var child = VisualTreeHelper.GetChild(root, i);
        if (child is T match)
          return match;
        if (Find<T>(child) is { } found)
          return found;
      }

      return null;
    }
  }
}
