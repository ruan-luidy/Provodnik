using Provodnik.Core.Install;
using Provodnik.Core.IO;
using Provodnik.Core.Layout;
using Provodnik.Core.Modpack;

namespace Provodnik.Features.Mods
{
  /// <summary>Como um mod está no disco agora: o arquivo em downloads\, a pasta em mods\ e o que o Проводник lembra de ter posto lá.</summary>
  public sealed record ModInventoryItem(ModEntry Entry, long? ArchiveSize, bool HasPartial, bool FolderHasContent, InstalledMod? Installed, bool DisabledInGamma = false)
  {
    public bool ArchivePresent => ArchiveSize is not null;

    /// <summary>
    /// Pasta vazia que faz falta. A vazia de um mod que o GAMMA deixa desligado não faz: é o caso do #142 (a pasta da
    /// v2.5 que a lista pede não existe mais no arquivo v2.6) e do #456 (o arquivo do autor só tem pastas).
    /// </summary>
    public bool NeedsInstall => !FolderHasContent && !DisabledInGamma;
  }

  /// <summary>Varre downloads\ e mods\ pra tela de Mods (só existência e tamanho, sem md5: é rápido).</summary>
  public static class ModInventory
  {
    public static IReadOnlyList<ModInventoryItem> Scan(GammaLayout layout, ModList list, InstallState state, IReadOnlySet<string>? disabled = null)
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
          state.Mods.GetValueOrDefault(mod.FolderName),
          disabled?.Contains(mod.FolderName) == true));
      }
      return items;
    }
  }
}
