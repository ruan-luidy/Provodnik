using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Provodnik.Core.Install;
using Provodnik.Core.Text;
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
    // o {0} (ou {0:formato}) das frases do catálogo (fica antes do Finished, que depende dele)
    private static readonly Regex Placeholder = new(@"\{(\d+)(?:[,:][^}]*)?\}", RegexOptions.Compiled);

    // a frase do fim nos idiomas do catálogo, mais a de antes dos catálogos (logs antigos)
    private static readonly IReadOnlyList<Regex> Finished =
    [
      .. L.Variants("features.install.finished.ok").Select(t => Pattern(t, failures: false)).OfType<Regex>(),
      .. L.Variants("features.install.finished.failures").Select(t => Pattern(t, failures: true)).OfType<Regex>(),
      new(@"(?:Pronto|Terminou) em (?<h>\d+):(?<m>\d+):(?<s>\d+)(?:.* com (?<failures>\d+) falha)?", RegexOptions.Compiled),
    ];

    public string Title => L.T("logs.file.title", Started);

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
          nameof(StageState.Done) => (LogLevel.Success, L.T("logs.stage.done", title, detail)),
          nameof(StageState.Warning) => (LogLevel.Warning, L.T("logs.stage.warning", title, detail)),
          nameof(StageState.Failed) => (LogLevel.Error, L.T("logs.stage.failed", title, detail)),
          nameof(StageState.Skipped) => (LogLevel.Info, L.T("logs.stage.skipped", title, detail)),
          var state => (LogLevel.Info, $"{title}: {state}{detail}"),
        };
      }

      if (ModLine.Match(text) is { Success: true } mod)
      {
        var who = mod.Groups[2].Success ? $"#{mod.Groups[1].Value} {mod.Groups[2].Value}" : $"#{mod.Groups[1].Value}";
        var detail = mod.Groups[4].Success ? mod.Groups[4].Value : null;
        return mod.Groups[3].Value switch
        {
          nameof(ModStatus.Failed) => (LogLevel.Error, detail is null ? L.T("logs.mod.failed", who) : L.T("logs.mod.failedWith", who, detail)),
          nameof(ModStatus.UpToDate) => (LogLevel.Info, L.T("logs.mod.upToDate", who)),
          _ => (LogLevel.Info, L.T("logs.mod.installed", who)),
        };
      }

      return (LogLevel.Info, text);
    }

    private static (string Caption, RowTone Tone) Outcome(string path)
    {
      if (ReadAll(path) is not { } text)
        return (L.T("logs.outcome.unreadable"), RowTone.Neutral);
      // a última frase do fim no arquivo, em qualquer idioma
      var match = Finished.Select(f => f.Matches(text).LastOrDefault()).OfType<Match>().MaxBy(m => m.Index);
      if (match is null)
        return (L.T("logs.outcome.interrupted"), RowTone.Warning);

      var took = new TimeSpan(int.Parse(match.Groups["h"].Value), int.Parse(match.Groups["m"].Value), int.Parse(match.Groups["s"].Value));
      var duration = took.TotalHours >= 1
        ? L.T("logs.duration.hours", (int)took.TotalHours, took.Minutes)
        : took.TotalMinutes >= 1 ? L.T("logs.duration.minutes", took.Minutes) : L.T("logs.duration.seconds", took.Seconds);
      if (!match.Groups["failures"].Success)
        return (L.T("logs.outcome.ok", duration), RowTone.Success);
      var failures = int.Parse(match.Groups["failures"].Value);
      return (L.T(failures == 1 ? "logs.outcome.failure" : "logs.outcome.failures", duration, failures), RowTone.Error);
    }

    /// <summary>
    /// A frase do catálogo virando regex: {0} é o tempo (hh:mm:ss), {1} o número de falhas na de falha, o resto
    /// qualquer número. Para no último {n}: o que vem depois é conselho e pode mudar de uma versão pra outra.
    /// </summary>
    private static Regex? Pattern(string template, bool failures)
    {
      var pattern = new StringBuilder();
      var at = 0;
      var hasTime = false;
      foreach (Match placeholder in Placeholder.Matches(template))
      {
        pattern.Append(Regex.Escape(template[at..placeholder.Index]));
        var index = placeholder.Groups[1].Value;
        hasTime |= index == "0";
        pattern.Append(index switch
        {
          "0" => @"(?<h>\d+):(?<m>\d+):(?<s>\d+)",
          "1" when failures => @"(?<failures>\d+)",
          _ => @"\d+",
        });
        at = placeholder.Index + placeholder.Length;
      }
      return hasTime ? new Regex(pattern.ToString(), RegexOptions.Compiled) : null;
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
