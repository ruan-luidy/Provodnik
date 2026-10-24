namespace Provodnik.Core.Modpack
{
  /// <summary>
  /// O mods.txt que vem de stalker-gamma.com/api/client/v1/mods/list: uma linha por addon ou separador, na ordem do
  /// MO2. A leitura replica o PowerShell do launcher original coluna por coluna (Split("`t") e Select -Index), porque
  /// os nomes de pasta que saem daqui têm que bater com o modlist.txt do GAMMA.
  /// </summary>
  public sealed class ModList
  {
    private ModList(IReadOnlyList<ModEntry> entries)
    {
      Entries = entries;
      Mods = entries.Where(e => !e.IsSeparator).ToList();
    }

    public IReadOnlyList<ModEntry> Entries { get; }

    /// <summary>Só os addons (sem os separadores).</summary>
    public IReadOnlyList<ModEntry> Mods { get; }

    public static ModList Parse(string text)
    {
      var lines = text.Replace("\r\n", "\n").Split('\n').ToList();
      // o Get-Content não devolve a linha vazia depois do último \n
      if (lines.Count > 0 && lines[^1].Length == 0)
        lines.RemoveAt(lines.Count - 1);

      var entries = new List<ModEntry>(lines.Count);
      for (var i = 0; i < lines.Count; i++)
        entries.Add(ParseLine(i + 1, lines[i].TrimEnd('\r')));

      return new ModList(entries);
    }

    internal static ModEntry ParseLine(int index, string line)
    {
      var columns = line.Split('\t');
      string? Column(int i) => i < columns.Length ? columns[i] : null;

      var url = Column(0) ?? string.Empty;
      var kind = KindOf(url);
      if (kind == ModKind.Separator)
        return new ModEntry(index, kind, null, [], string.Empty, url, null, null, null);

      var instructions = Column(1) is { Length: > 0 } raw && raw != "0"
        ? raw.Split(':').Where(i => i.Length > 0).ToList()
        : [];
      var suffix = Column(2) is { } patch && patch != "0" ? patch : string.Empty;
      var md5 = Column(6)?.Trim().ToLowerInvariant();

      return new ModEntry(
        index,
        kind,
        url,
        instructions,
        suffix,
        Column(3) ?? string.Empty,
        Column(4),
        ArchiveNameOf(kind, url, Column(5)),
        string.IsNullOrEmpty(md5) ? null : md5);
    }

    /// <summary>
    /// Mesma decisão do original: sem "http" é separador; "gamma_large_files" ganha de github e gofile; o resto pelo
    /// nome do site.
    /// </summary>
    internal static ModKind KindOf(string url)
    {
      if (!url.Contains("http", StringComparison.OrdinalIgnoreCase))
        return ModKind.Separator;
      if (url.Contains("gamma_large_files", StringComparison.OrdinalIgnoreCase))
        return ModKind.LargeFiles;
      if (url.Contains("moddb", StringComparison.OrdinalIgnoreCase))
        return ModKind.ModDb;
      if (url.Contains("gofile", StringComparison.OrdinalIgnoreCase))
        return ModKind.GoFile;
      return ModKind.GitHub;
    }

    /// <summary>
    /// O nome do arquivo em downloads\. ModDB usa a coluna do arquivo; GitHub o 5º pedaço do link
    /// ("github.com/ahuyn/anomaly-exo/..." vira anomaly-exo.zip); gofile e large files o 7º.
    /// </summary>
    internal static string? ArchiveNameOf(ModKind kind, string url, string? zipName)
    {
      var parts = url.Split('/');
      return kind switch
      {
        ModKind.ModDb => zipName is { Length: > 0 } ? zipName.Trim() : $"moddb-{parts[^1]}.zip",
        ModKind.GitHub => parts.Length > 4 ? $"{parts[4]}.zip" : null,
        ModKind.GoFile or ModKind.LargeFiles => parts.Length > 6 ? $"{parts[6]}.zip" : null,
        _ => null,
      };
    }
  }
}
