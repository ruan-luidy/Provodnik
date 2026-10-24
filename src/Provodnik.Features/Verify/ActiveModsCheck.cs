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
      // o que o GAMMA manda (a definição); o perfil só recebe uma cópia dela no fim da instalação
      var definition = Path.Combine(layout.Definition(metadata.FolderName), metadata.ModOrganizerModList.Replace('/', Path.DirectorySeparatorChar));
      var source = File.Exists(definition) ? definition : Path.Combine(layout.Profiles, metadata.FolderName, "modlist.txt");
      if (!File.Exists(source))
        return new ActiveModsResult(0, 0, []);

      var active = Mo2ModList.Parse(File.ReadAllText(source)).ActiveMods;
      var missing = active
        .Where(m => !FileTree.HasContent(Path.Combine(layout.Mods, m.Name)))
        .Select(m => m.Name)
        .ToList();
      return new ActiveModsResult(active.Count, active.Count - missing.Count, missing);
    }
  }
}
