using System.Diagnostics;
using System.Text;
using Provodnik.Core.Text;

namespace Provodnik.Infrastructure.Processes
{
  /// <summary>Como terminou um processo: o código de saída e as últimas linhas de cada saída (pra mensagem de erro).</summary>
  public sealed record ProcessResult(int ExitCode, IReadOnlyList<string> Output, IReadOnlyList<string> Errors)
  {
    /// <summary>As últimas linhas do erro (ou da saída, quando o erro veio vazio), numa linha só.</summary>
    public string Tail(int lines = 4)
    {
      var source = Errors.Count > 0 ? Errors : Output;
      return string.Join(" · ", source.TakeLast(lines).Select(l => l.Trim()).Where(l => l.Length > 0));
    }
  }

  public sealed record ProcessOptions(
    string? WorkingDirectory = null,
    Action<string>? OnOutput = null,
    Action<string>? OnError = null,
    IReadOnlyDictionary<string, string>? Environment = null,
    TimeSpan? Timeout = null);

  /// <summary>
  /// Roda uma ferramenta sem janela e lê as duas saídas enquanto ela trabalha, linha a linha, cortando também no \r
  /// (é assim que o curl, o 7-Zip e o git redesenham o progresso). A entrada fica fechada: ferramenta que parar
  /// pra perguntar alguma coisa (senha de rar, login do git) recebe fim de arquivo e sai em vez de travar. Cancelar
  /// mata a árvore inteira do processo.
  /// </summary>
  public static class ProcessRunner
  {
    private const int KeepLines = 40;

    public static async Task<ProcessResult> RunAsync(string fileName, IEnumerable<string> arguments, ProcessOptions? options, CancellationToken cancellationToken)
    {
      options ??= new ProcessOptions();
      var info = new ProcessStartInfo(fileName)
      {
        UseShellExecute = false,
        RedirectStandardInput = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true,
        StandardOutputEncoding = Encoding.UTF8,
        StandardErrorEncoding = Encoding.UTF8,
      };
      if (options.WorkingDirectory is { } directory)
        info.WorkingDirectory = directory;
      foreach (var argument in arguments)
        info.ArgumentList.Add(argument);
      if (options.Environment is { } environment)
      {
        foreach (var (key, value) in environment)
          info.Environment[key] = value;
      }

      using var process = new Process { StartInfo = info };
      if (!process.Start())
        throw new InvalidOperationException(L.T("infra.process.startFailed", Path.GetFileName(fileName)));
      process.StandardInput.Close();

      var output = new Queue<string>();
      var errors = new Queue<string>();
      var outputTask = PumpAsync(process.StandardOutput, output, options.OnOutput);
      var errorTask = PumpAsync(process.StandardError, errors, options.OnError);

      using var timeout = new CancellationTokenSource(options.Timeout ?? Timeout.InfiniteTimeSpan);
      using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
      try
      {
        await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
      }
      catch (OperationCanceledException)
      {
        Kill(process);
        cancellationToken.ThrowIfCancellationRequested();
        throw new TimeoutException(L.T("infra.process.timeout", Path.GetFileName(fileName), options.Timeout?.TotalSeconds));
      }

      await Task.WhenAll(outputTask, errorTask).ConfigureAwait(false);
      return new ProcessResult(process.ExitCode, output.ToList(), errors.ToList());
    }

    private static async Task PumpAsync(StreamReader reader, Queue<string> keep, Action<string>? onLine)
    {
      var buffer = new char[4096];
      var line = new StringBuilder();

      void Flush()
      {
        if (line.Length == 0)
          return;
        var text = line.ToString();
        line.Clear();
        if (keep.Count == KeepLines)
          keep.Dequeue();
        keep.Enqueue(text);
        onLine?.Invoke(text);
      }

      int read;
      while ((read = await reader.ReadAsync(buffer).ConfigureAwait(false)) > 0)
      {
        for (var i = 0; i < read; i++)
        {
          if (buffer[i] is '\r' or '\n')
            Flush();
          else
            line.Append(buffer[i]);
        }
      }
      Flush();
    }

    private static void Kill(Process process)
    {
      try
      {
        if (!process.HasExited)
          process.Kill(entireProcessTree: true);
      }
      catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
      {
      }
    }
  }
}
