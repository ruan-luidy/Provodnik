namespace Provodnik.Core.Layout
{
  /// <summary>Um executável cadastrado no MO2 (customExecutables): "Anomaly (DX11-AVX)" → bin\AnomalyDX11AVX.exe.</summary>
  public sealed record Mo2Executable(string Title, string Binary);

  /// <summary>
  /// O que interessa do ModOrganizer.ini: onde está o Anomaly (gamePath=@ByteArray(D:\\Anomaly)), o perfil escolhido e
  /// os executáveis. O original achava o Anomaly cortando a linha do binary= do AnomalyLauncher; aqui é o gamePath, com
  /// essa linha de reserva.
  /// </summary>
  public sealed record Mo2Config(string? GamePath, string? SelectedProfile, IReadOnlyList<Mo2Executable> Executables)
  {
    public static readonly Mo2Config Empty = new(null, null, []);

    public static Mo2Config Load(string path) => File.Exists(path) ? Parse(File.ReadAllText(path)) : Empty;

    public static Mo2Config Parse(string text)
    {
      string? section = null;
      string? gamePath = null;
      string? profile = null;
      var titles = new SortedDictionary<int, string>();
      var binaries = new SortedDictionary<int, string>();

      foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
      {
        var line = raw.Trim();
        if (line.StartsWith('[') && line.EndsWith(']'))
        {
          section = line[1..^1];
          continue;
        }

        var equals = line.IndexOf('=');
        if (equals < 0)
          continue;
        var key = line[..equals];
        var value = line[(equals + 1)..];

        if (section == "General" && key == "gamePath")
          gamePath = Unwrap(value);
        else if (section == "General" && key == "selected_profile")
          profile = Unwrap(value);
        else if (section == "customExecutables" && key.Split('\\') is [var number, var field] && int.TryParse(number, out var n))
        {
          if (field == "title")
            titles[n] = value;
          else if (field == "binary")
            binaries[n] = value.Replace('/', '\\');
        }
      }

      var executables = titles
        .Where(t => binaries.ContainsKey(t.Key))
        .Select(t => new Mo2Executable(t.Value, binaries[t.Key]))
        .ToList();

      // sem gamePath: a pasta do AnomalyLauncher.exe (o jeito do original)
      gamePath ??= executables
        .Where(e => e.Binary.EndsWith("AnomalyLauncher.exe", StringComparison.OrdinalIgnoreCase))
        .Select(e => Path.GetDirectoryName(e.Binary))
        .FirstOrDefault();

      return new Mo2Config(gamePath, profile, executables);
    }

    /// <summary>@ByteArray(D:\\Anomaly) → D:\Anomaly. O Qt escapa a barra.</summary>
    internal static string Unwrap(string value)
    {
      if (value.StartsWith("@ByteArray(", StringComparison.Ordinal) && value.EndsWith(')'))
        value = value["@ByteArray(".Length..^1];
      return value.Replace(@"\\", @"\").Trim();
    }

    /// <summary>Os executáveis do jogo (bin\Anomaly*.exe), sem o launcher e as ferramentas.</summary>
    public IReadOnlyList<Mo2Executable> GameExecutables =>
      Executables.Where(e => Path.GetFileName(e.Binary).StartsWith("AnomalyDX", StringComparison.OrdinalIgnoreCase)).ToList();
  }
}
