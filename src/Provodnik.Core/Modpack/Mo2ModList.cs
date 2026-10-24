namespace Provodnik.Core.Modpack
{
  /// <summary>Uma linha do modlist.txt do MO2: "+Nome" ligado, "-Nome" desligado, "*Nome" travado (DLC, sempre ligado).</summary>
  public sealed record Mo2ModListEntry(string Name, bool Enabled)
  {
    public bool IsSeparator => Name.EndsWith("_separator", StringComparison.OrdinalIgnoreCase);
  }

  /// <summary>
  /// O modlist.txt de um perfil do MO2 (o do GAMMA vem em G.A.M.M.A\modpack_data). Dele sai a "regra dos 5xx mods"
  /// da comunidade: depois de instalar, o MO2 tem que mostrar exatamente esse tanto de mods ativos. O número muda a
  /// cada versão da definição, então é contado da lista em vez de decorado.
  /// </summary>
  public sealed class Mo2ModList
  {
    private Mo2ModList(IReadOnlyList<Mo2ModListEntry> entries) => Entries = entries;

    public IReadOnlyList<Mo2ModListEntry> Entries { get; }

    /// <summary>Os mods ligados, sem os separadores: o "Active" que o MO2 mostra.</summary>
    public IReadOnlyList<Mo2ModListEntry> ActiveMods => Entries.Where(e => e.Enabled && !e.IsSeparator).ToList();

    public static Mo2ModList Parse(string text)
    {
      var entries = new List<Mo2ModListEntry>();
      foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
      {
        if (raw.Length < 2 || raw[0] == '#')
          continue;
        var enabled = raw[0] switch
        {
          '+' or '*' => true,
          '-' => false,
          _ => (bool?)null,
        };
        if (enabled is { } on)
          entries.Add(new Mo2ModListEntry(raw[1..], on));
      }
      return new Mo2ModList(entries);
    }
  }
}
