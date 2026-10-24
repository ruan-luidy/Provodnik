namespace Provodnik.Core.Layout
{
  /// <summary>
  /// A pasta do GAMMA (a instância portátil do MO2) e tudo que mora nela:
  ///   GAMMA\ModOrganizer.exe, downloads\, mods\, profiles\
  ///   GAMMA\.Grok's Modpack Installer\ (o launcher original, mods.txt, version.txt e resources\ com as ferramentas)
  /// O Проводник guarda o que é dele em .Grok's Modpack Installer\.provodnik.
  /// </summary>
  public sealed record GammaLayout(string Root)
  {
    public const string InstallerFolderName = ".Grok's Modpack Installer";

    public string Installer => Path.Combine(Root, InstallerFolderName);

    public string Resources => Path.Combine(Installer, "resources");

    public string Downloads => Path.Combine(Root, "downloads");

    public string Mods => Path.Combine(Root, "mods");

    public string Profiles => Path.Combine(Root, "profiles");

    public string ModOrganizerExe => Path.Combine(Root, "ModOrganizer.exe");

    public string ModOrganizerIni => Path.Combine(Root, "ModOrganizer.ini");

    public string ModListFile => Path.Combine(Installer, "mods.txt");

    public string VersionFile => Path.Combine(Installer, "version.txt");

    public string MetadataFile => Path.Combine(Installer, "modpack_maker_metadata.txt");

    /// <summary>O que é do Проводник: estado da instalação, cache de md5, logs e a pasta de trabalho.</summary>
    public string DataDir => Path.Combine(Installer, ".provodnik");

    public string LogsDir => Path.Combine(DataDir, "logs");

    /// <summary>Onde os mods são extraídos antes de entrar no lugar (mesmo disco do mods\, então a troca é um rename).</summary>
    public string WorkDir => Path.Combine(DataDir, "work");

    // ===== As ferramentas que o GAMMA já traz =====

    public string SevenZip => Path.Combine(Resources, "7zip", "7z.exe");

    public string Curl => Path.Combine(Resources, "curl-impersonate-win", "curl.exe");

    public string CurlConfig => Path.Combine(Resources, "curl-impersonate-win", "config", "safari15_5.config");

    public string CurlHeader => Path.Combine(Resources, "curl-impersonate-win", "config", "safari15_5.header");

    public string ModDbCli => Path.Combine(Resources, "stalker-gamma-dl-cli.exe");

    public string Git => Path.Combine(Resources, "cmd", "git.exe");

    public string MetaIniTemplate => Path.Combine(Resources, "meta.ini");

    public string SeparatorMetaIni => Path.Combine(Resources, "separator", "meta.ini");

    public string ProfileTemplate => Path.Combine(Resources, "profiles_files");

    public string Repository(string name) => Path.Combine(Resources, name);

    /// <summary>A definição do modpack copiada do repositório (G.A.M.M.A\modpack_data, modpack_addons, modpack_patches).</summary>
    public string Definition(string modpackFolder) => Path.Combine(Installer, modpackFolder);

    public bool Exists => File.Exists(ModOrganizerExe) && Directory.Exists(Installer);

    /// <summary>
    /// Procura a pasta do GAMMA a partir de onde o exe está: ele pode morar na raiz (GAMMA\Provodnik.exe), dentro do
    /// instalador (GAMMA\.Grok's Modpack Installer\Provodnik.exe) ou numa subpasta qualquer abaixo deles.
    /// </summary>
    public static GammaLayout? Find(string start)
    {
      var dir = new DirectoryInfo(start);
      for (var depth = 0; dir is not null && depth < 5; depth++, dir = dir.Parent)
      {
        var layout = new GammaLayout(dir.FullName);
        if (layout.Exists)
          return layout;
      }
      return null;
    }
  }
}
