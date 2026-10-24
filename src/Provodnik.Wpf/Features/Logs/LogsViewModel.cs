using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Provodnik.Core.Install;
using Provodnik.Wpf.Features.Install;
using Provodnik.Wpf.Shared;
using Provodnik.Wpf.Shared.Services;

namespace Provodnik.Wpf.Features.Logs
{
  /// <summary>Quais linhas aparecem: tudo, só avisos e erros, ou só erros.</summary>
  public enum LogFilter
  {
    All,
    Warnings,
    Errors,
  }

  /// <summary>
  /// O registro: o desta sessão (o que a caixa de texto do launcher original mostrava), ao vivo, e os logs das
  /// instalações anteriores em .provodnik\logs, pra abrir sem sair do app. Filtra por nível e por texto.
  /// </summary>
  public sealed partial class LogsViewModel : PageViewModel
  {
    private readonly LogBook _log;
    private readonly GammaSession _session;

    [ObservableProperty] private ObservableCollection<LogFile> _files = [];
    [ObservableProperty] private LogFile? _selectedFile;
    [ObservableProperty] private ICollectionView _lines;
    [ObservableProperty] private LogFilter _filter = LogFilter.All;
    [ObservableProperty] private string _search = string.Empty;
    [ObservableProperty] private int _total;
    [ObservableProperty] private int _warnings;
    [ObservableProperty] private int _errors;

    private IList<LogLine> _source;

    public LogsViewModel(LogBook log, GammaSession session, InstallService install)
    {
      _log = log;
      _session = session;
      // cada instalação deixa um arquivo novo na lista
      install.Finished += (_, _) => _ = RunAsync(LoadAsync);
      _source = log.Lines;
      _lines = View(_source);
      log.Lines.CollectionChanged += OnSessionChanged;
      Count();
    }

    /// <summary>Mostrando o desta sessão (ao vivo); falso quando é um arquivo de antes.</summary>
    public bool IsSession => SelectedFile is null;

    protected override Task LoadAsync()
    {
      var selected = SelectedFile?.Path;
      Files = new ObservableCollection<LogFile>(LogFile.List(_session.Layout?.LogsDir));
      SelectedFile = Files.FirstOrDefault(f => f.Path == selected);
      return Task.CompletedTask;
    }

    partial void OnSelectedFileChanged(LogFile? value)
    {
      _source = value is null ? _log.Lines : value.Read().ToList();
      Lines = View(_source);
      OnPropertyChanged(nameof(IsSession));
      Count();
    }

    partial void OnFilterChanged(LogFilter value) => Lines.Refresh();

    partial void OnSearchChanged(string value) => Lines.Refresh();

    private ICollectionView View(IList<LogLine> source)
    {
      var view = new ListCollectionView((System.Collections.IList)source) { Filter = Matches };
      return view;
    }

    private bool Matches(object item) =>
      item is LogLine line
      && Filter switch
      {
        LogFilter.Warnings => line.Level is LogLevel.Warning or LogLevel.Error,
        LogFilter.Errors => line.Level == LogLevel.Error,
        _ => true,
      }
      && (Search.Length == 0 || line.Text.Contains(Search, StringComparison.CurrentCultureIgnoreCase));

    // a instalação manda centenas de linhas por segundo: só soma as novas, e reconta quando limpa ou corta o começo
    private void OnSessionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
      if (!IsSession)
        return;
      if (e.Action != NotifyCollectionChangedAction.Add || e.NewItems is null)
      {
        Count();
        return;
      }
      foreach (LogLine line in e.NewItems)
      {
        Total++;
        if (line.Level == LogLevel.Warning)
          Warnings++;
        else if (line.Level == LogLevel.Error)
          Errors++;
      }
    }

    private void Count()
    {
      Total = _source.Count;
      Warnings = _source.Count(l => l.Level == LogLevel.Warning);
      Errors = _source.Count(l => l.Level == LogLevel.Error);
    }

    [RelayCommand]
    private void ShowSession() => SelectedFile = null;

    [RelayCommand]
    private void Copy()
    {
      var visible = Lines.Cast<LogLine>().ToList();
      Clipboard.SetText(string.Join(Environment.NewLine, visible.Select(l => $"{l.TimeText} {l.Level} {l.Text}")));
      ToastService.Success($"{visible.Count} linhas copiadas");
    }

    [RelayCommand]
    private void Clear() => _log.Clear();

    [RelayCommand]
    private void OpenFolder() => FolderOpener.Open(_session.Layout?.LogsDir);
  }
}
