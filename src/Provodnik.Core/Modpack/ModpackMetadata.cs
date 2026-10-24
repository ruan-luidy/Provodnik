namespace Provodnik.Core.Modpack
{
  /// <summary>
  /// O modpack_maker_metadata.txt da pasta do instalador ("modpack name = G.A.M.M.A." e companhia). O original lê os
  /// valores pela posição da linha; aqui pela chave, com a posição de reserva.
  /// </summary>
  public sealed record ModpackMetadata(string Author, string Name, string ModOrganizerModList, string AdditionalFiles)
  {
    public static readonly ModpackMetadata Gamma = new("Grokitach", "G.A.M.M.A.", "modpack_data/modlist.txt", "modpack_addons");

    /// <summary>
    /// O nome como pasta: o Windows corta o ponto final, então "G.A.M.M.A." vira G.A.M.M.A (é assim que o perfil do
    /// MO2 e a pasta da definição existem no disco).
    /// </summary>
    public string FolderName => Name.TrimEnd('.', ' ');

    public static ModpackMetadata Parse(string text)
    {
      var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
      var ordered = new List<string>();
      foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
      {
        var equals = line.IndexOf('=');
        if (equals < 0)
          continue;
        var value = line[(equals + 1)..].Trim();
        values[line[..equals].Trim()] = value;
        ordered.Add(value);
      }

      string Pick(string key, int index, string fallback) =>
        values.TryGetValue(key, out var value) && value.Length > 0 ? value
        : index < ordered.Count && ordered[index].Length > 0 ? ordered[index]
        : fallback;

      return new ModpackMetadata(
        Pick("author", 0, Gamma.Author),
        Pick("modpack name", 1, Gamma.Name),
        Pick("ModOrganier2 modlist", 3, Gamma.ModOrganizerModList),
        Pick("modpack additional files", 4, Gamma.AdditionalFiles));
    }
  }
}
