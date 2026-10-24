using Provodnik.Core.IO;
using Provodnik.Core.Layout;
using Provodnik.Core.Modpack;
using Provodnik.Core.Text;

namespace Provodnik.Features.ModOrganizer
{
  /// <summary>
  /// O perfil do MO2, como o original monta: apaga o Default, cria profiles\G.A.M.M.A com os arquivos de
  /// resources\profiles_files e põe por cima o modlist.txt da definição (a ordem e o que vem ligado) - com os nomes
  /// digitados errado na lista do GAMMA trocados pelo da pasta (<see cref="ModListNames"/>), senão o MO2 desliga o mod.
  /// </summary>
  public static class ProfileSetup
  {
    public static string Apply(GammaLayout layout, ModpackMetadata metadata, ICollection<(string From, string To)>? fixes = null)
    {
      FileTree.Delete(Path.Combine(layout.Profiles, "Default"));

      var profile = Path.Combine(layout.Profiles, metadata.FolderName);
      Directory.CreateDirectory(profile);
      FileTree.CopyContents(layout.ProfileTemplate, profile);

      var modList = Path.Combine(layout.Definition(metadata.FolderName), metadata.ModOrganizerModList.Replace('/', Path.DirectorySeparatorChar));
      if (!File.Exists(modList))
        throw new FileNotFoundException(L.T("features.profile.noModList", modList));
      var folders = Directory.Exists(layout.Mods) ? Directory.GetDirectories(layout.Mods).Select(Path.GetFileName).OfType<string>().ToList() : [];
      var text = ModListNames.Repair(File.ReadAllText(modList), folders, fixes ?? []);
      File.WriteAllText(Path.Combine(profile, "modlist.txt"), text);
      return profile;
    }
  }
}
