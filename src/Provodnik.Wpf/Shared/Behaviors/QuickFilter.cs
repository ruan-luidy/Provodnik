using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Provodnik.Wpf.Shared.Behaviors
{
  /// <summary>
  /// Filtro de uma caixa só pra qualquer tabela - o "quick filter" do AG Grid: compara o que foi digitado com
  /// todo texto da linha, sem acento e sem ligar pra maiúscula. Liga direto na caixa, sem passar pela ViewModel:
  ///   &lt;hc:TextBox x:Name="Filtro" … /&gt;
  ///   &lt;DataGrid behaviors:QuickFilter.Text="{Binding Text, ElementName=Filtro}" … /&gt;
  /// Cada palavra digitada tem que aparecer em algum campo, então "vale on" acha VALE3 e "petro pn" acha PETR4.
  /// </summary>
  public static class QuickFilter
  {
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
      "Text", typeof(string), typeof(QuickFilter), new PropertyMetadata(string.Empty, OnTextChanged));

    /// <summary>Cache de "quais propriedades de texto esse tipo de linha tem", por tipo.</summary>
    private static readonly Dictionary<Type, PropertyInfo[]> TextProperties = [];

    public static string GetText(DependencyObject target) => (string)target.GetValue(TextProperty);

    public static void SetText(DependencyObject target, string value) => target.SetValue(TextProperty, value);

    private static void OnTextChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
      if (target is not ItemsControl items)
        return;

      // A tabela troca de ItemsSource (refresh recria a coleção): sem reaplicar, o filtro sumia no primeiro
      // refresh e a pessoa via a lista inteira voltar sozinha.
      var descriptor = DependencyPropertyDescriptor.FromProperty(ItemsControl.ItemsSourceProperty, items.GetType());
      descriptor.RemoveValueChanged(items, OnItemsSourceChanged);
      descriptor.AddValueChanged(items, OnItemsSourceChanged);

      Apply(items);
    }

    private static void OnItemsSourceChanged(object? sender, EventArgs e)
    {
      if (sender is ItemsControl items)
        Apply(items);
    }

    private static void Apply(ItemsControl items)
    {
      if (items.ItemsSource is null)
        return;

      var view = CollectionViewSource.GetDefaultView(items.ItemsSource);
      if (view is null)
        return;

      var words = Normalize(GetText(items)).Split(' ', StringSplitOptions.RemoveEmptyEntries);
      view.Filter = words.Length == 0 ? null : row => Matches(row, words);
    }

    private static bool Matches(object? row, string[] words)
    {
      if (row is null)
        return false;

      var haystack = Normalize(Flatten(row));

      return words.All(haystack.Contains);
    }

    /// <summary>Junta num texto só tudo que a linha mostra: ticker, nome, setor, os números já formatados.</summary>
    private static string Flatten(object row)
    {
      var type = row.GetType();
      if (!TextProperties.TryGetValue(type, out var properties))
        TextProperties[type] = properties = type
          .GetProperties(BindingFlags.Public | BindingFlags.Instance)
          .Where(p => p.CanRead && p.GetIndexParameters().Length == 0 && p.PropertyType == typeof(string))
          .ToArray();

      var sb = new StringBuilder();
      foreach (var property in properties)
      {
        if (property.GetValue(row) is not string value || value.Length == 0)
          continue;

        sb.Append(value);
        sb.Append(' ');
      }

      return sb.ToString();
    }

    private static string Normalize(string? text)
    {
      if (string.IsNullOrWhiteSpace(text))
        return string.Empty;

      var decomposed = text.Trim().ToUpperInvariant().Normalize(NormalizationForm.FormD);
      var sb = new StringBuilder(decomposed.Length);
      foreach (var c in decomposed)
        if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
          sb.Append(c);

      return sb.ToString();
    }
  }
}
