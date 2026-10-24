using Provodnik.Core.IO;
using Provodnik.Core.Layout;
using Provodnik.Core.Modpack;

namespace Provodnik.Features.Verify
{
  /// <summary>
  /// Quantos mods o modlist pede ligados e quantos têm pasta com conteúdo em mods\. Os de nome errado na lista do GAMMA
  /// (sem pasta, mas com uma pasta de nome quase igual) ficam fora da conta: o MO2 descarta a linha e mostra um a menos
  /// pra todo mundo, e reinstalar não muda nada.
  /// </summary>
  public sealed record ActiveModsResult(int Expected, int Found, IReadOnlyList<string> Missing, IReadOnlyList<string> Misnamed)
  {
    public bool Complete => Expected > 0 && Missing.Count == 0;
  }

  /// <summary>
  /// A conferência que a comunidade faz de olho ("o MO2 tem que mostrar 5xx mods ativos"), feita direito: cada mod
  /// ligado no modlist.txt do GAMMA precisa de uma pasta em mods\ com algo além do meta.ini. Os que faltam saem pelo
  /// nome, pra saber o que reinstalar.
  /// </summary>
  public static class ActiveModsCheck
  {
    public static ActiveModsResult Run(GammaLayout layout, ModpackMetadata metadata)
    {
      if (ReadModList(layout, metadata) is not { } modList)
        return new ActiveModsResult(0, 0, [], []);

      var active = modList.ActiveMods;
      var absent = active
        .Where(m => !FileTree.HasContent(Path.Combine(layout.Mods, m.Name)))
        .Select(m => m.Name)
        .ToList();
      var folders = Directory.Exists(layout.Mods) ? Directory.GetDirectories(layout.Mods).Select(Path.GetFileName).OfType<string>().ToList() : [];
      var misnamed = absent.Where(n => !Directory.Exists(Path.Combine(layout.Mods, n)) && folders.Any(f => IsTypo(n, f))).ToList();
      var missing = absent.Except(misnamed).ToList();
      var expected = active.Count - misnamed.Count;
      return new ActiveModsResult(expected, expected - missing.Count, missing, misnamed);
    }

    /// <summary>Os mods que o próprio GAMMA deixa desligados ("-Nome"): pasta vazia neles não faz falta no jogo.</summary>
    public static IReadOnlySet<string> Disabled(GammaLayout layout, ModpackMetadata metadata) =>
      ReadModList(layout, metadata)?.Entries.Where(e => !e.Enabled && !e.IsSeparator).Select(e => e.Name).ToHashSet(StringComparer.OrdinalIgnoreCase)
        ?? new HashSet<string>();

    /// <summary>
    /// Nomes compridos que diferem por até 2 letras ("DarkasleQif's" x "Darkasleif's"): erro de digitação, não outro mod.
    /// Os numerados ("312- ...") a instalação cria com o nome da lista, então nunca são.
    /// </summary>
    internal static bool IsTypo(string listed, string folder)
    {
      if (listed.Length < 12 || char.IsAsciiDigit(listed[0]) || Math.Abs(listed.Length - folder.Length) > 2 || listed.Equals(folder, StringComparison.OrdinalIgnoreCase))
        return false;
      var a = listed.ToUpperInvariant();
      var b = folder.ToUpperInvariant();
      var previous = Enumerable.Range(0, b.Length + 1).ToArray();
      for (var i = 1; i <= a.Length; i++)
      {
        var current = new int[b.Length + 1];
        current[0] = i;
        for (var j = 1; j <= b.Length; j++)
          current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
        previous = current;
      }
      return previous[b.Length] <= 2;
    }

    private static Mo2ModList? ReadModList(GammaLayout layout, ModpackMetadata metadata)
    {
      // o que o GAMMA manda (a definição); o perfil só recebe uma cópia dela no fim da instalação
      var definition = Path.Combine(layout.Definition(metadata.FolderName), metadata.ModOrganizerModList.Replace('/', Path.DirectorySeparatorChar));
      var source = File.Exists(definition) ? definition : Path.Combine(layout.Profiles, metadata.FolderName, "modlist.txt");
      return File.Exists(source) ? Mo2ModList.Parse(File.ReadAllText(source)) : null;
    }
  }
}
