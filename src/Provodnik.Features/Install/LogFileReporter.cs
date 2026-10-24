using Provodnik.Core.Install;

namespace Provodnik.Features.Install
{
  /// <summary>
  /// Repassa tudo pra tela e escreve o que importa num arquivo (.provodnik\logs\install-*.log): etapas, avisos, erros
  /// e o fim de cada mod. O original tinha a escrita do log comentada; quando algo dava errado não sobrava rastro.
  /// </summary>
  public sealed class LogFileReporter : IInstallReporter, IDisposable
  {
    private readonly IInstallReporter _inner;
    private readonly StreamWriter? _writer;
    private readonly Lock _gate = new();

    public LogFileReporter(IInstallReporter inner, string logsDir)
    {
      _inner = inner;
      try
      {
        Directory.CreateDirectory(logsDir);
        Path = System.IO.Path.Combine(logsDir, $"install-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");
        _writer = new StreamWriter(Path, append: false) { AutoFlush = true };
        // guarda só os 20 últimos
        foreach (var old in Directory.GetFiles(logsDir, "install-*.log").OrderDescending().Skip(20))
          File.Delete(old);
      }
      catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
      {
        _writer = null;
      }
    }

    public string? Path { get; }

    public void Stage(InstallStage stage, StageState state, string? detail = null)
    {
      _inner.Stage(stage, state, detail);
      if (state != StageState.Running)
        Write($"[{stage}] {state}{(detail is null ? string.Empty : $" · {detail}")}");
    }

    public void Mod(ModProgress progress)
    {
      _inner.Mod(progress);
      if (progress.Status is ModStatus.Installed or ModStatus.UpToDate or ModStatus.Failed)
        Write($"#{progress.Index} {progress.Status}{(progress.Detail is null ? string.Empty : $" · {progress.Detail}")}");
    }

    public void Log(LogLevel level, string message)
    {
      _inner.Log(level, message);
      Write($"{level.ToString().ToUpperInvariant()} {message}");
    }

    private void Write(string line)
    {
      if (_writer is null)
        return;
      lock (_gate)
        _writer.WriteLine($"{DateTime.Now:HH:mm:ss} {line}");
    }

    public void Dispose()
    {
      lock (_gate)
        _writer?.Dispose();
    }
  }
}
