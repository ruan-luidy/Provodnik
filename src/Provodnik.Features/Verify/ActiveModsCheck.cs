using Provodnik.Core.IO;
using Provodnik.Core.Layout;
using Provodnik.Core.Modpack;

namespace Provodnik.Features.Verify
{
  /// <summary>Quantos mods o modlist pede ligados e quantos têm pasta com conteúdo em mods\.</summary>
  public sealed record ActiveModsResult(int Expected, int Found, IReadOnlyList<string> Missing)
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
        return new ActiveModsResult(0, 0, []);

      var active = modList.ActiveMods;
      var missing = active
        .Where(m => !FileTree.HasContent(Path.Combine(layout.Mods, m.Name)))
        .Select(m => m.Name)
        .ToList();
      return new ActiveModsResult(active.Count, active.Count - missing.Count, missing);
    }

    /// <summary>Os mods que o próprio GAMMA deixa desligados ("-Nome"): pasta vazia neles não faz falta no jogo.</summary>
    public static IReadOnlySet<string> Disabled(GammaLayout layout, ModpackMetadata metadata) =>
      ReadModList(layout, metadata)?.Entries.Where(e => !e.Enabled && !e.IsSeparator).Select(e => e.Name).ToHashSet(StringComparer.OrdinalIgnoreCase)
        ?? new HashSet<string>();

    private static Mo2ModList? ReadModList(GammaLayout layout, ModpackMetadata metadata)
    {
      // o que o GAMMA manda (a definição); o perfil só recebe uma cópia dela no fim da instalação
      var definition = Path.Combine(layout.Definition(metadata.FolderName), metadata.ModOrganizerModList.Replace('/', Path.DirectorySeparatorChar));
      var source = File.Exists(definition) ? definition : Path.Combine(layout.Profiles, metadata.FolderName, "modlist.txt");
      return File.Exists(source) ? Mo2ModList.Parse(File.ReadAllText(source)) : null;
    }
  }
}
