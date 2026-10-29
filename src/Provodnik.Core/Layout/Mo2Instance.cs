using System.Text;

namespace Provodnik.Core.Layout
{
  /// <summary>
  /// O "First Install Initialization" do launcher original sem abrir o MO2: em vez de a pessoa apontar o Anomaly no
  /// "Browse..." da primeira vez, o ModOrganizer.ini já sai com o jogo (gameName, gamePath), o perfil do GAMMA, a
  /// primeira abertura marcada como feita e os executáveis que o MO2 cadastra pro Anomaly (os mesmos títulos, na
  /// mesma ordem). O resto do ini (janela, plugins) fica como estava.
  /// </summary>
  public static class Mo2Instance
  {
    public const string GameName = "STALKER Anomaly";

    private static readonly (string Title, string Exe)[] GameExecutables =
    [
      ("Anomaly (DX11-AVX)", "AnomalyDX11AVX.exe"),
      ("Anomaly (DX11)", "AnomalyDX11.exe"),
      ("Anomaly (DX10-AVX)", "AnomalyDX10AVX.exe"),
      ("Anomaly (DX10)", "AnomalyDX10.exe"),
      ("Anomaly (DX9-AVX)", "AnomalyDX9AVX.exe"),
      ("Anomaly (DX9)", "AnomalyDX9.exe"),
      ("Anomaly (DX8-AVX)", "AnomalyDX8AVX.exe"),
      ("Anomaly (DX8)", "AnomalyDX8.exe"),
    ];

    /// <summary>O ini novo a partir do de agora (nulo: não existe).</summary>
    public static string Configure(string? existing, string gamma, string anomaly, string profile, string? version)
    {
      var sections = Parse(existing ?? string.Empty);

      var general = Section(sections, "General");
      Set(general, "gameName", GameName);
      Set(general, "gamePath", $"@ByteArray({anomaly.Replace(@"\", @"\\")})");
      if (!general.Any(l => Key(l) == "selected_profile"))
        Set(general, "selected_profile", $"@ByteArray({profile})");
      if (version is { Length: > 0 } && !general.Any(l => Key(l) == "version"))
        Set(general, "version", version);
      Set(general, "first_start", "false");

      // o MO2 tenta subir o Python à toa sem isso (o GAMMA não usa plugin em Python)
      Set(Section(sections, "PluginPersistance"), @"Python%20Proxy\tryInit", "false");

      var executables = Section(sections, "customExecutables");
      executables.Clear();
      executables.AddRange(Executables(gamma, anomaly));

      var text = new StringBuilder();
      foreach (var (name, lines) in sections)
      {
        if (name.Length > 0)
          text.Append('[').Append(name).Append("]\r\n");
        foreach (var line in lines)
          text.Append(line).Append("\r\n");
        text.Append("\r\n");
      }
      return text.ToString();
    }

    /// <summary>O Anomaly Launcher, os 8 exes do jogo e o Explorer++ que vem com o MO2 do GAMMA, como o MO2 grava.</summary>
    private static IEnumerable<string> Executables(string gamma, string anomaly)
    {
      var game = anomaly.Replace('\\', '/').TrimEnd('/');
      var mo2 = gamma.Replace('\\', '/').TrimEnd('/');
      var entries = new List<(string Title, string Binary, string Arguments, string Directory)>
      {
        ("Anomaly Launcher", $"{game}/AnomalyLauncher.exe", string.Empty, game),
      };
      entries.AddRange(GameExecutables.Select(e => (e.Title, $"{game}/bin/{e.Exe}", string.Empty, $"{game}/bin")));
      entries.Add(("Explore Virtual Folder", $"{mo2}/explorer++/Explorer++.exe", $"\\\"{anomaly.Replace(@"\", @"\\")}\\\"", $"{mo2}/explorer++"));

      yield return $"size={entries.Count}";
      for (var i = 0; i < entries.Count; i++)
      {
        var n = i + 1;
        var (title, binary, arguments, directory) = entries[i];
        yield return $@"{n}\arguments={arguments}";
        yield return $@"{n}\binary={binary}";
        yield return $@"{n}\hide=false";
        yield return $@"{n}\ownicon=true";
        yield return $@"{n}\steamAppID=";
        yield return $@"{n}\title={title}";
        yield return $@"{n}\toolbar=false";
        yield return $@"{n}\workingDirectory={directory}";
      }
    }

    private static List<(string Name, List<string> Lines)> Parse(string text)
    {
      var sections = new List<(string Name, List<string> Lines)> { (string.Empty, []) };
      foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
      {
        var line = raw.TrimEnd();
        if (line.StartsWith('[') && line.EndsWith(']'))
          sections.Add((line[1..^1], []));
        else if (line.Length > 0)
          sections[^1].Lines.Add(line);
      }
      if (sections[0].Lines.Count == 0)
        sections.RemoveAt(0);
      return sections;
    }

    private static List<string> Section(List<(string Name, List<string> Lines)> sections, string name)
    {
      var found = sections.FirstOrDefault(s => s.Name == name);
      if (found.Lines is not null)
        return found.Lines;
      var lines = new List<string>();
      // o [General] vai na frente, como o MO2 grava
      if (name == "General")
        sections.Insert(0, (name, lines));
      else
        sections.Add((name, lines));
      return lines;
    }

    private static string? Key(string line) => line.IndexOf('=') is var i and > 0 ? line[..i] : null;

    private static void Set(List<string> lines, string key, string value)
    {
      var index = lines.FindIndex(l => Key(l) == key);
      if (index >= 0)
        lines[index] = $"{key}={value}";
      else
        lines.Add($"{key}={value}");
    }
  }
}
