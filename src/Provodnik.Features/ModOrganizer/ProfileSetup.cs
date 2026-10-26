using Provodnik.Core.IO;
using Provodnik.Core.Layout;
using Provodnik.Core.Modpack;
using Provodnik.Core.Repos;
using Provodnik.Core.Text;

namespace Provodnik.Features.ModOrganizer
{
  /// <summary>O perfil montado: onde, o que ficou da pessoa e a cópia do modlist.txt de antes (nula se nada mudou).</summary>
  public sealed record ProfileSetupResult(string Profile, ProfileMergeResult Merge, string? Backup);

  /// <summary>
  /// O perfil do MO2, como o original monta: apaga o Default, cria profiles\G.A.M.M.A com os arquivos de
  /// resources\profiles_files e põe o modlist.txt da definição (a ordem e o que vem ligado) - com os nomes digitados
  /// errado na lista do GAMMA trocados pelo da pasta (<see cref="ModListNames"/>), senão o MO2 desliga o mod. Diferente
  /// do original, o modlist.txt que já estava lá é mesclado (<see cref="ProfileMerge"/>) em vez de substituído, e uma
  /// cópia dele fica em .provodnik\profile-backups.
  /// </summary>
  public static class ProfileSetup
  {
    private const int BackupsKept = 10;

    /// <summary>A lista do GAMMA que entrou no perfil da última vez: a base pra saber o que a pessoa mudou.</summary>
    public static string BasePath(GammaLayout layout) => Path.Combine(layout.DataDir, "modlist.gamma.txt");

    public static string BackupsDir(GammaLayout layout) => Path.Combine(layout.DataDir, "profile-backups");

    /// <param name="previousGamma">
    /// A lista do GAMMA de antes desta instalação, quando ainda não tem a base gravada (a primeira vez depois do launcher
    /// original ou de uma versão antiga do Проводник).
    /// </param>
    /// <param name="installed">As pastas que o Проводник instalou (do GAMMA, então nunca são da pessoa).</param>
    public static ProfileSetupResult Apply(
      GammaLayout layout,
      ModpackMetadata metadata,
      ICollection<(string From, string To)>? fixes = null,
      string? previousGamma = null,
      IReadOnlySet<string>? installed = null,
      string? separatorTemplate = null)
    {
      FileTree.Delete(Path.Combine(layout.Profiles, "Default"));

      var profile = Path.Combine(layout.Profiles, metadata.FolderName);
      var target = Path.Combine(profile, "modlist.txt");
      // antes do profiles_files, que também traz um modlist.txt
      var current = File.Exists(target) ? File.ReadAllText(target) : null;
      Directory.CreateDirectory(profile);
      FileTree.CopyContents(layout.ProfileTemplate, profile);

      var definition = layout.Definition(metadata.FolderName);
      var modList = Path.Combine(definition, metadata.ModOrganizerModList.Replace('/', Path.DirectorySeparatorChar));
      if (!File.Exists(modList))
        throw new FileNotFoundException(L.T("features.profile.noModList", modList));
      var folders = Directory.Exists(layout.Mods) ? Directory.GetDirectories(layout.Mods).Select(Path.GetFileName).OfType<string>().ToList() : [];
      var gamma = ModListNames.Repair(File.ReadAllText(modList), folders, fixes ?? []);

      var basePath = BasePath(layout);
      var stored = File.Exists(basePath) ? File.ReadAllText(basePath) : null;
      var previous = stored ?? (previousGamma is null ? null : ModListNames.Repair(previousGamma, folders, []));

      // as pastas que o GAMMA copia inteiras pro mods\: o modpack_addons da definição e o gamma_large_files_v2
      var addons = new[] { Path.Combine(definition, metadata.AdditionalFiles), layout.Repository(GitRepository.LargeFiles.Name) }
        .Where(Directory.Exists)
        .SelectMany(Directory.GetDirectories)
        .Select(Path.GetFileName)
        .OfType<string>()
        .Where(name => name != ".git")
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
      ModOwner OwnerOf(string name) => Owner(layout, name, installed, addons);
      var merge = ProfileMerge.Merge(
        gamma,
        previous,
        current,
        OwnerOf,
        folders.Where(f => OwnerOf(f) is ModOwner.Gamma or ModOwner.GammaOptional).ToList(),
        L.T("features.profile.myMods") + "_separator",
        L.Variants("features.profile.myMods").Select(v => v + "_separator").ToList());

      if (merge.Separator is { } separator)
      {
        var folder = Path.Combine(layout.Mods, separator);
        Directory.CreateDirectory(folder);
        var meta = Path.Combine(folder, "meta.ini");
        if (!File.Exists(meta))
          File.WriteAllText(meta, separatorTemplate ?? MetaIni.SeparatorTemplate);
      }

      string? backup = null;
      if (current is not null && current != merge.Text)
        backup = Backup(layout, current);
      File.WriteAllText(target, merge.Text);
      Directory.CreateDirectory(layout.DataDir);
      File.WriteAllText(basePath, gamma);
      return new ProfileSetupResult(profile, merge, backup);
    }

    /// <summary>
    /// De quem é uma pasta em mods\ fora da lista do GAMMA. Do GAMMA: o que o Проводник instalou, o que vem no
    /// modpack_addons ou no gamma_large_files_v2 (opcional), o separador numerado ("12- Armas_separator", numeração
    /// velha) e o que tem o meta.ini que os instaladores do GAMMA escrevem. O resto é da pessoa.
    /// </summary>
    public static ModOwner Owner(GammaLayout layout, string name, IReadOnlySet<string>? installed, IReadOnlySet<string> addons)
    {
      if (name is "." or ".." || name != Path.GetFileName(name))
        return ModOwner.None;
      var folder = Path.Combine(layout.Mods, name);
      if (!Directory.Exists(folder))
        return ModOwner.None;
      if (installed?.Contains(name) == true)
        return ModOwner.Gamma;
      if (addons.Contains(name))
        return ModOwner.GammaOptional;
      if (name.EndsWith("_separator", StringComparison.OrdinalIgnoreCase) && IsNumbered(name))
        return ModOwner.Gamma;
      return MetaIni.WrittenByGammaInstaller(folder) ? ModOwner.Gamma : ModOwner.User;
    }

    // "12- Armas_separator"
    private static bool IsNumbered(string name)
    {
      var dash = name.IndexOf('-');
      return dash > 0 && name[..dash].All(char.IsAsciiDigit);
    }

    /// <summary>A cópia mais nova do modlist.txt do perfil (nula: nenhuma ainda).</summary>
    public static FileInfo? LatestBackup(GammaLayout layout)
    {
      var dir = BackupsDir(layout);
      return Directory.Exists(dir)
        ? Newest(dir).FirstOrDefault()
        : null;
    }

    /// <summary>
    /// Põe de volta no perfil a cópia mais nova do modlist.txt. O que está no perfil agora vira cópia também, então
    /// voltar de novo desfaz a volta. Nulo quando não tem cópia.
    /// </summary>
    public static FileInfo? RestoreLatest(GammaLayout layout, ModpackMetadata metadata)
    {
      if (LatestBackup(layout) is not { } latest)
        return null;
      var restored = File.ReadAllText(latest.FullName);
      var target = Path.Combine(layout.Profiles, metadata.FolderName, "modlist.txt");
      if (File.Exists(target))
      {
        var current = File.ReadAllText(target);
        if (current != restored)
          Backup(layout, current);
      }
      Directory.CreateDirectory(Path.GetDirectoryName(target)!);
      File.WriteAllText(target, restored);
      return latest;
    }

    // pela hora de gravar: o nome tem só o segundo, e a do mesmo segundo ganha "-2"
    private static IEnumerable<FileInfo> Newest(string dir) =>
      new DirectoryInfo(dir).GetFiles("modlist-*.txt").OrderByDescending(f => f.LastWriteTimeUtc).ThenByDescending(f => f.Name, StringComparer.Ordinal);

    private static string Backup(GammaLayout layout, string current)
    {
      var dir = BackupsDir(layout);
      Directory.CreateDirectory(dir);
      // duas no mesmo segundo (voltar logo depois da instalação): a segunda ganha um número, sem apagar a primeira
      var stamp = $"modlist-{DateTime.Now:yyyyMMdd-HHmmss}";
      var path = Path.Combine(dir, stamp + ".txt");
      for (var n = 2; File.Exists(path); n++)
        path = Path.Combine(dir, $"{stamp}-{n}.txt");
      File.WriteAllText(path, current);
      foreach (var old in Newest(dir).Skip(BackupsKept))
        old.Delete();
      return path;
    }
  }
}
