using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Provodnik.Core.Install;
using Provodnik.Wpf.Features.Install;
using Provodnik.Wpf.Shared;
using Provodnik.Wpf.Shared.Services;

namespace Provodnik.Wpf.Features.Logs
{
  /// <summary>
  /// Um log de instalação em .provodnik\logs (install-2026-10-24_18-19-53.log), pra lista da esquerda: quando começou e
  /// como terminou (a última linha que a instalação escreve, "Pronto em ..." ou "Terminou em ... com 9 falha(s)").
  /// </summary>
  public sealed record LogFile(string Path, DateTime Started, string Caption, RowTone Tone)
  {
    private static readonly Regex Finished = new(@"(Pronto|Terminou) em (\d+):(\d+):(\d+)(?:.* com (\d+) falha)?", RegexOptions.Compiled);

    public string Title => Started.ToString("dd/MM · HH:mm", CultureInfo.InvariantCulture);

    public static IReadOnlyList<LogFile> List(string? folder)
    {
      if (folder is null || !Directory.Exists(folder))
        return [];
      return Directory.GetFiles(folder, "install-*.log")
        .Select(path => new FileInfo(path))
        .Select(info =>
        {
          var (caption, tone) = Outcome(info.FullName);
          return new LogFile(info.FullName, StartedOf(info), caption, tone);
        })
        .OrderByDescending(f => f.Started)
        .ToList();
    }

    /// <summary>
    /// As linhas do arquivo com o nível de volta, do jeito que o LogFileReporter escreve: "WARNING ..." e "ERROR ..."
    /// na frente da mensagem, "[Etapa] Failed" e "#312 Failed" pra falha.
    /// </summary>
    public IReadOnlyList<LogLine> Read()
    {
      if (ReadAll(Path) is not { } content)
        return [];
      var raw = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

      var lines = new List<LogLine>(raw.Length);
      foreach (var line in raw.Select(l => l.TrimEnd('\r')))
      {
        if (line.Length < 9 || !TimeOnly.TryParseExact(line[..8], "HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
          continue;
        // a falha do mod já vem na linha ERROR logo em seguida, com o nome e o motivo
        if (ModLine.Match(line[9..]) is { Success: true } failed && failed.Groups[3].Value == nameof(ModStatus.Failed))
          continue;
        var (level, text) = Classify(line[9..]);
        lines.Add(new LogLine(Started.Date + time.ToTimeSpan(), level, text));
      }
      return lines;
    }

    private static readonly Regex StageLine = new(@"^\[(\w+)\] (\w+)(?: · (.*))?$", RegexOptions.Compiled);
    private static readonly Regex ModLine = new(@"^#(\d+)(?: \((.+)\))? (Installed|UpToDate|Failed)(?: · (.*))?$", RegexOptions.Compiled);

    // os nomes das etapas como a tela de instalação mostra
    private static readonly Dictionary<string, string> StageTitles =
      StageRow.CreateAll().ToDictionary(s => s.Stage.ToString(), s => s.Title);

    /// <summary>
    /// O nível e o texto de uma linha do arquivo. As linhas de etapa ("[Mods] Warning · 9 com falha") e de mod
    /// ("#312 (Nome) Failed · motivo") são gravadas no formato da máquina; aqui viram frase ("Addons: com aviso, 9 com
    /// falha", "#312 Nome: falhou - motivo").
    /// </summary>
    internal static (LogLevel Level, string Text) Classify(string text)
    {
      foreach (var level in new[] { LogLevel.Info, LogLevel.Success, LogLevel.Warning, LogLevel.Error })
      {
        var tag = level.ToString().ToUpperInvariant() + " ";
        if (text.StartsWith(tag, StringComparison.Ordinal))
          return (level, text[tag.Length..]);
      }

      if (StageLine.Match(text) is { Success: true } stage)
      {
        var title = StageTitles.GetValueOrDefault(stage.Groups[1].Value, stage.Groups[1].Value);
        var detail = stage.Groups[3].Success ? $", {stage.Groups[3].Value}" : string.Empty;
        return stage.Groups[2].Value switch
        {
          nameof(StageState.Done) => (LogLevel.Success, $"{title}: pronto{detail}"),
          nameof(StageState.Warning) => (LogLevel.Warning, $"{title}: com aviso{detail}"),
          nameof(StageState.Failed) => (LogLevel.Error, $"{title}: falhou{detail}"),
          nameof(StageState.Skipped) => (LogLevel.Info, $"{title}: pulado, nada mudou{detail}"),
          var state => (LogLevel.Info, $"{title}: {state}{detail}"),
        };
      }

      if (ModLine.Match(text) is { Success: true } mod)
      {
        var who = mod.Groups[2].Success ? $"#{mod.Groups[1].Value} {mod.Groups[2].Value}" : $"#{mod.Groups[1].Value}";
        var detail = mod.Groups[4].Success ? mod.Groups[4].Value : null;
        return mod.Groups[3].Value switch
        {
          nameof(ModStatus.Failed) => (LogLevel.Error, detail is null ? $"{who}: falhou" : $"{who}: falhou - {detail}"),
          nameof(ModStatus.UpToDate) => (LogLevel.Info, $"{who}: já estava em dia"),
          _ => (LogLevel.Info, $"{who}: instalado"),
        };
      }

      return (LogLevel.Info, text);
    }

    private static (string Caption, RowTone Tone) Outcome(string path)
    {
      if (ReadAll(path) is not { } text)
        return ("não deu pra ler", RowTone.Neutral);
      if (Finished.Matches(text).LastOrDefault() is not { } match)
        return ("parou no meio", RowTone.Warning);

      var took = new TimeSpan(int.Parse(match.Groups[2].Value), int.Parse(match.Groups[3].Value), int.Parse(match.Groups[4].Value));
      var duration = took.TotalHours >= 1 ? $"{(int)took.TotalHours} h {took.Minutes} min" : took.TotalMinutes >= 1 ? $"{took.Minutes} min" : $"{took.Seconds} s";
      if (!match.Groups[5].Success)
        return ($"{duration} · sem falha", RowTone.Success);
      var failures = match.Groups[5].Value;
      return ($"{duration} · {failures} {(failures == "1" ? "falha" : "falhas")}", RowTone.Error);
    }

    // o arquivo da instalação que está rodando continua aberto pra escrita
    private static string? ReadAll(string path)
    {
      try
      {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
      }
      catch (IOException)
      {
        return null;
      }
    }

    private static DateTime StartedOf(FileInfo info)
    {
      var stamp = System.IO.Path.GetFileNameWithoutExtension(info.Name)["install-".Length..];
      return DateTime.TryParseExact(stamp, "yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var started)
        ? started
        : info.CreationTime;
    }
  }
}
