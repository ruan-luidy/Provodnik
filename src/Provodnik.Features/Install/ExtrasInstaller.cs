using Provodnik.Core.IO;
using Provodnik.Core.Layout;
using Provodnik.Core.Modpack;
using Provodnik.Core.Repos;

namespace Provodnik.Features.Install
{
  /// <summary>
  /// O que entra no mods\ depois dos addons, na ordem do original:
  ///   1. o gamma_large_files_v2 inteiro (menos o .git);
  ///   2. as armas do Teivaz: todo gamedata em teivaz_anomaly_gunslinger\*\ e \*\*\ vai pro gamedata do mod
  ///      "Gunslinger Guns for Anomaly" (a linha 312 do mods.txt existe só pra criar essa pasta);
  ///   3. o modpack_addons da definição, que completa pastas de mods já instaladas.
  /// </summary>
  public static class ExtrasInstaller
  {
    private const string GunslingerName = "Gunslinger Guns for Anomaly";
    private const string GunslingerFallback = "312- Gunslinger Guns for Anomaly - Teivazcz & Gunslinger Team";

    public static void CopyLargeFiles(GammaLayout layout, CancellationToken cancellationToken) =>
      FileTree.CopyContents(layout.Repository(GitRepository.LargeFiles.Name), layout.Mods, [".git"], cancellationToken);

    public static int CopyGunslinger(GammaLayout layout, ModList list, CancellationToken cancellationToken)
    {
      var source = layout.Repository(GitRepository.Gunslinger.Name);
      if (!Directory.Exists(source))
        return 0;

      var folder = list.Mods.FirstOrDefault(m => m.Title.Equals(GunslingerName, StringComparison.OrdinalIgnoreCase))?.FolderName ?? GunslingerFallback;
      var target = Path.Combine(layout.Mods, folder, "gamedata");
      var weapons = Directory.EnumerateDirectories(source).Where(d => !Path.GetFileName(d).Equals(".git", StringComparison.OrdinalIgnoreCase)).ToList();

      // o original copia primeiro todos os *\gamedata e depois os *\*\gamedata (patches por cima das armas)
      var copied = 0;
      foreach (var weapon in weapons)
        copied += FileTree.CopyContents(Path.Combine(weapon, "gamedata"), target, cancellationToken: cancellationToken);
      foreach (var variant in weapons.SelectMany(Directory.EnumerateDirectories))
        copied += FileTree.CopyContents(Path.Combine(variant, "gamedata"), target, cancellationToken: cancellationToken);
      return copied;
    }

    public static int CopyModpackAddons(GammaLayout layout, ModpackMetadata metadata, CancellationToken cancellationToken) =>
      FileTree.CopyContents(Path.Combine(layout.Definition(metadata.FolderName), metadata.AdditionalFiles), layout.Mods, cancellationToken: cancellationToken);
  }
}
