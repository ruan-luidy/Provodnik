using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Provodnik.Core.Diagnostics;
using Provodnik.Core.Text;

namespace Provodnik.Wpf.Shared.Services
{
  public enum ConsoleKind
  {
    Normal,
    Error,
    Hint,
  }

  /// <summary>Uma linha do console: de qual ferramenta veio e o texto cru; ou a explicação de um erro, logo abaixo dele.</summary>
  public sealed record ConsoleLine(DateTime Time, string Tool, string Text, ConsoleKind Kind)
  {
    public string TimeText => Time.ToString("HH:mm:ss");

    public RowTone Tone => Kind switch
    {
      ConsoleKind.Error => RowTone.Error,
      ConsoleKind.Hint => RowTone.Warning,
      _ => RowTone.Neutral,
    };
  }

  /// <summary>
  /// O console das ferramentas, o cmd que o launcher original abria junto, mas legível: cada linha do git, curl,
  /// 7-Zip e stalker-gamma-dl-cli aparece crua, e embaixo de todo erro conhecido entra uma linha "→ o que é e o que
  /// fazer" (<see cref="ErrorExplainer"/>). Durante uma instalação também vai pra .provodnik\logs\console-*.log.
  /// </summary>
  public sealed class ToolConsole : IToolConsole
  {
    private const int Limit = 4000;
    private readonly ConcurrentQueue<ConsoleLine> _pending = new();
    private StreamWriter? _file;
    private Explanation? _lastHint;
    private DateTime _lastHintAt;

    public ToolConsole()
    {
      _ = new DispatcherTimer(TimeSpan.FromMilliseconds(200), DispatcherPriority.Background, (_, _) => Flush(), Application.Current.Dispatcher);
    }

    public ObservableCollection<ConsoleLine> Lines { get; } = [];

    public void Write(string tool, string line)
    {
      if (string.IsNullOrWhiteSpace(line))
        return;
      _pending.Enqueue(new ConsoleLine(DateTime.Now, tool, line.TrimEnd(), ErrorExplainer.Explain(line) is null ? ConsoleKind.Normal : ConsoleKind.Error));
    }

    /// <summary>Começa a gravar o console num arquivo (a instalação chama no começo e passa nulo no fim).</summary>
    public void RecordTo(string? path)
    {
      Flush();
      _file?.Dispose();
      _file = null;
      if (path is null)
        return;
      try
      {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _file = new StreamWriter(path, append: false) { AutoFlush = true };
      }
      catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
      {
        _file = null;
      }
    }

    public void Clear() => Lines.Clear();

    private void Flush()
    {
      if (_pending.IsEmpty)
        return;

      while (_pending.TryDequeue(out var line))
      {
        Add(line);
        if (line.Kind != ConsoleKind.Error || ErrorExplainer.Explain(line.Text) is not { } explanation)
          continue;

        // o mesmo recado seguido (o curl tentando de novo) não se repete a cada linha
        if (explanation == _lastHint && line.Time - _lastHintAt < TimeSpan.FromSeconds(20))
          continue;
        _lastHint = explanation;
        _lastHintAt = line.Time;
        Add(new ConsoleLine(line.Time, L.T("shared.console.hint"), $"{explanation.Meaning} {explanation.Hint}", ConsoleKind.Hint));
      }

      while (Lines.Count > Limit)
        Lines.RemoveAt(0);
    }

    private void Add(ConsoleLine line)
    {
      Lines.Add(line);
      try
      {
        _file?.WriteLine($"{line.TimeText} [{line.Tool}] {line.Text}");
      }
      catch (IOException)
      {
      }
    }
  }
}
