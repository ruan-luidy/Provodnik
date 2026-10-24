using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using Microsoft.Win32;
using Provodnik.Core.Text;
using Provodnik.Wpf.Shared.Services;

namespace Provodnik.Wpf.Shared.Behaviors
{
  /// <summary>
  /// Botão direito em qualquer tabela do terminal → "Copiar tabela" (cola no Excel/Sheets) ou "Exportar CSV…".
  /// Ligado no estilo TerminalDataGrid, então toda tabela ganha sem mexer em widget nenhum.
  ///
  /// Exporta o que está na tela, coluna por coluna: coluna com Binding lê o caminho dela; coluna de template tem o
  /// template instanciado uma vez pra achar os Bindings de texto dentro (TextBlock.Text e Run.Text) - é assim que
  /// "ATIVO" sai como o ticker e "VAR %" como "+1.23%", igual ao que se vê. Coluna sem cabeçalho (botões) fica de fora.
  /// </summary>
  public static class DataGridExport
  {
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
      "IsEnabled",
      typeof(bool),
      typeof(DataGridExport),
      new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
      if (d is not DataGrid grid || e.NewValue is not true)
        return;

      var copy = new MenuItem { Header = L.T("shared.grid.copy") };
      copy.Click += (_, _) => Copy(grid);
      var export = new MenuItem { Header = L.T("shared.grid.export") };
      export.Click += (_, _) => Export(grid);

      // O filtro do cabeçalho (DataGridFilterBehavior) já pode ter criado o menu com o "Limpar todos os filtros":
      // os nossos entram em cima dele. Antes a grade com filtro ficava só com o dele
      var menu = grid.ContextMenu ?? new ContextMenu();
      var ours = new Control[] { copy, export };
      if (menu.Items.Count > 0)
        ours = [.. ours, new Separator()];
      foreach (var (index, item) in ours.Index())
        menu.Items.Insert(index, item);
      grid.ContextMenu = menu;
    }

    private static void Copy(DataGrid grid)
    {
      var (headers, rows) = Read(grid);
      var text = new StringBuilder();
      text.AppendLine(string.Join('\t', headers));
      foreach (var row in rows)
        text.AppendLine(string.Join('\t', row.Select(v => v.Replace('\t', ' '))));

      Clipboard.SetText(text.ToString());
      ToastService.Success(L.T("shared.lines.copied", rows.Count));
    }

    private static void Export(DataGrid grid)
    {
      var (headers, rows) = Read(grid);
      var dialog = new SaveFileDialog
      {
        Filter = "CSV (*.csv)|*.csv",
        FileName = $"provodnik_{DateTime.Now:yyyy-MM-dd_HHmm}.csv",
      };

      if (dialog.ShowDialog(Window.GetWindow(grid)) != true)
        return;

      // Ponto e vírgula + BOM: o Excel em português abre direto nas colunas certas e com acento.
      var csv = new StringBuilder();
      csv.AppendLine(string.Join(';', headers.Select(Quote)));
      foreach (var row in rows)
        csv.AppendLine(string.Join(';', row.Select(Quote)));

      File.WriteAllText(dialog.FileName, csv.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
      ToastService.Success(L.T("shared.grid.exported", rows.Count, Path.GetFileName(dialog.FileName)));
    }

    private static string Quote(string value) =>
      value.IndexOfAny([';', '"', '\n', '\r']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;

    private static (List<string> Headers, List<List<string>> Rows) Read(DataGrid grid)
    {
      var columns = grid.Columns
        .Where(c => c.Visibility == Visibility.Visible && c.Header is not null && !string.IsNullOrWhiteSpace(c.Header.ToString()))
        .OrderBy(c => c.DisplayIndex)
        .Select(c => (Header: c.Header!.ToString()!, Paths: PathsOf(c)))
        .Where(c => c.Paths.Count > 0)
        .ToList();

      var rows = new List<List<string>>();
      foreach (var item in (IEnumerable?)grid.ItemsSource ?? grid.Items)
      {
        if (item is null || item == CollectionView.NewItemPlaceholder)
          continue;

        rows.Add(columns.Select(c => string.Join(" ", c.Paths.Select(p => Evaluate(item, p)).Where(v => v.Length > 0))).ToList());
      }

      return (columns.Select(c => c.Header).ToList(), rows);
    }

    /// <summary>Os caminhos de Binding que dão o texto da coluna.</summary>
    private static List<string> PathsOf(DataGridColumn column)
    {
      if (column is DataGridBoundColumn { Binding: Binding { Path.Path: { Length: > 0 } path } })
        return [path];

      if (column is not DataGridTemplateColumn { CellTemplate: { } template })
        return [];

      var paths = new List<string>();
      if (template.LoadContent() is DependencyObject root)
        Collect(root, paths);

      return paths;
    }

    private static void Collect(DependencyObject node, List<string> paths)
    {
      if (node is TextBlock textBlock)
      {
        if (BindingOperations.GetBinding(textBlock, TextBlock.TextProperty) is { Path.Path: { Length: > 0 } path })
          paths.Add(path);

        foreach (var run in textBlock.Inlines.OfType<Run>())
        {
          if (BindingOperations.GetBinding(run, Run.TextProperty) is { Path.Path: { Length: > 0 } runPath })
            paths.Add(runPath);
        }
      }

      // O template ainda não está na árvore visual: anda pela lógica (filhos de painel, Child de Border...).
      foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())
        Collect(child, paths);
    }

    /// <summary>Lê "Symbol.Ticker" do item por reflexão (o que a tela mostra, formatado se já for string).</summary>
    private static string Evaluate(object item, string path)
    {
      object? current = item;
      foreach (var part in path.Split('.'))
      {
        if (current is null)
          return string.Empty;

        current = current.GetType().GetProperty(part)?.GetValue(current);
      }

      return current switch
      {
        null => string.Empty,
        string text => text,
        Freezable => string.Empty,
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => current.ToString() ?? string.Empty,
      };
    }
  }
}
