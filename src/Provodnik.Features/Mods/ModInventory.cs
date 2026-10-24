using Provodnik.Core.Install;
using Provodnik.Core.IO;
using Provodnik.Core.Layout;
using Provodnik.Core.Modpack;

namespace Provodnik.Features.Mods
{
  /// <summary>Como um mod está no disco agora: o arquivo em downloads\, a pasta em mods\ e o que o Проводник lembra de ter posto lá.</summary>
  public sealed record ModInventoryItem(ModEntry Entry, long? ArchiveSize, bool HasPartial, bool FolderHasContent, InstalledMod? Installed)
  {
    public bool ArchivePresent => ArchiveSize is not null;
  }

  /// <summary>Varre downloads\ e mods\ pra tela de Mods (só existência e tamanho, sem md5: é rápido).</summary>
  public static class ModInventory
  {
    public static IReadOnlyList<ModInventoryItem> Scan(GammaLayout layout, ModList list, InstallState state)
    {
      var items = new List<ModInventoryItem>(list.Mods.Count);
      foreach (var mod in list.Mods)
      {
        var archive = mod.ArchiveName is { } name ? new FileInfo(Path.Combine(layout.Downloads, name)) : null;
        var partial = archive is not null && File.Exists(archive.FullName + ".part");
        items.Add(new ModInventoryItem(
          mod,
          archive is { Exists: true } ? archive.Length : null,
          partial,
          FileTree.HasContent(Path.Combine(layout.Mods, mod.FolderName)),
          state.Mods.GetValueOrDefault(mod.FolderName)));
      }
      return items;
    }
  }
}
