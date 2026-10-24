using Provodnik.Core.IO;
using Provodnik.Core.Layout;
using Provodnik.Core.Modpack;

namespace Provodnik.Features.ModOrganizer
{
  /// <summary>
  /// O perfil do MO2, como o original monta: apaga o Default, cria profiles\G.A.M.M.A com os arquivos de
  /// resources\profiles_files e põe por cima o modlist.txt da definição (a ordem e o que vem ligado).
  /// </summary>
  public static class ProfileSetup
  {
    public static string Apply(GammaLayout layout, ModpackMetadata metadata)
    {
      FileTree.Delete(Path.Combine(layout.Profiles, "Default"));

      var profile = Path.Combine(layout.Profiles, metadata.FolderName);
      Directory.CreateDirectory(profile);
      FileTree.CopyContents(layout.ProfileTemplate, profile);

      var modList = Path.Combine(layout.Definition(metadata.FolderName), metadata.ModOrganizerModList.Replace('/', Path.DirectorySeparatorChar));
      if (!File.Exists(modList))
        throw new FileNotFoundException($"O modlist.txt da definição não está em {modList}");
      File.Copy(modList, Path.Combine(profile, "modlist.txt"), overwrite: true);
      return profile;
    }
  }
}
