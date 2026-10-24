using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using Provodnik.Core.Install;

namespace Provodnik.Wpf.Shared.Services
{
  /// <summary>Uma linha do registro.</summary>
  public sealed record LogLine(DateTime Time, LogLevel Level, string Text)
  {
    public string TimeText => Time.ToString("HH:mm:ss");

    /// <summary>A etiqueta pequena na frente da linha; info fica sem.</summary>
    public string LevelText => Level switch
    {
      LogLevel.Warning => "aviso",
      LogLevel.Error => "erro",
      LogLevel.Success => "pronto",
      _ => string.Empty,
    };

    public RowTone Tone => Level switch
    {
      LogLevel.Success => RowTone.Success,
      LogLevel.Warning => RowTone.Warning,
      LogLevel.Error => RowTone.Error,
      _ => RowTone.Neutral,
    };
  }

  /// <summary>
  /// O registro da sessão pra tela de Registro: qualquer thread escreve, a tela recebe em lotes (a instalação pode
  /// mandar centenas de linhas por segundo). Guarda as últimas 5000.
  /// </summary>
  public sealed class LogBook
  {
    private const int Limit = 5000;
    private readonly ConcurrentQueue<LogLine> _pending = new();

    public LogBook()
    {
      // um tique de 200 ms que só trabalha quando tem linha nova
      _ = new DispatcherTimer(TimeSpan.FromMilliseconds(200), DispatcherPriority.Background, (_, _) => Flush(), Application.Current.Dispatcher);
    }

    public ObservableCollection<LogLine> Lines { get; } = [];

    public void Write(LogLevel level, string text) => _pending.Enqueue(new LogLine(DateTime.Now, level, text));

    public void Clear() => Lines.Clear();

    private void Flush()
    {
      if (_pending.IsEmpty)
        return;
      while (_pending.TryDequeue(out var line))
        Lines.Add(line);
      while (Lines.Count > Limit)
        Lines.RemoveAt(0);
    }
  }
}
