using System.Windows;

namespace Provodnik.Wpf.Shared.Controls
{
  /// <summary>
  /// O contador de um item do painel lateral (desatualizados nas Versões, batch rodando nos Batches). Zero não aparece;
  /// <see cref="IsAlertProperty"/> pinta de vermelho em vez do azul. Com o painel recolhido vira uma bolinha no ícone.
  /// </summary>
  public static class NavBadge
  {
    public static readonly DependencyProperty CountProperty = DependencyProperty.RegisterAttached(
      "Count", typeof(int), typeof(NavBadge), new PropertyMetadata(0));

    public static readonly DependencyProperty IsAlertProperty = DependencyProperty.RegisterAttached(
      "IsAlert", typeof(bool), typeof(NavBadge), new PropertyMetadata(false));

    public static int GetCount(DependencyObject element) => (int)element.GetValue(CountProperty);

    public static void SetCount(DependencyObject element, int value) => element.SetValue(CountProperty, value);

    public static bool GetIsAlert(DependencyObject element) => (bool)element.GetValue(IsAlertProperty);

    public static void SetIsAlert(DependencyObject element, bool value) => element.SetValue(IsAlertProperty, value);
  }
}
