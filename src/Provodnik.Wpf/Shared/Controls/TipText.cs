using System.Windows;
using System.Windows.Controls;

namespace Provodnik.Wpf.Shared.Controls
{
  /// <summary>
  /// O texto de um tooltip. Uma linha só: texto normal. Várias: a primeira vira o título (semibold) e as outras vêm
  /// embaixo no cinza, uma por linha. Assim um "Enviada 05/11 14:30" fica simples e a célula da cotação ganha cabeçalho
  /// sem cada tela montar o próprio tooltip.
  /// </summary>
  public sealed class TipText : StackPanel
  {
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
      nameof(Source), typeof(string), typeof(TipText), new PropertyMetadata(null, (d, _) => ((TipText)d).Build()));

    private static readonly string[] Breaks = ["\r\n", "\n"];

    public string? Source
    {
      get => (string?)GetValue(SourceProperty);
      set => SetValue(SourceProperty, value);
    }

    private void Build()
    {
      Children.Clear();
      var lines = (Source ?? string.Empty).Split(Breaks, StringSplitOptions.TrimEntries);
      for (var i = 0; i < lines.Length; i++)
      {
        var line = new TextBlock { Text = lines[i], TextWrapping = TextWrapping.Wrap };
        if (lines.Length > 1 && i == 0)
        {
          line.FontWeight = FontWeights.SemiBold;
          line.Margin = new Thickness(0, 0, 0, 3);
        }
        else if (i > 0)
        {
          line.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryTextBrush");
          line.Margin = new Thickness(0, 1, 0, 0);
        }

        Children.Add(line);
      }
    }
  }
}
