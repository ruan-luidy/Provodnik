using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using Provodnik.Core.Install;
using Provodnik.Core.Layout;
using Provodnik.Core.Modpack;
using Provodnik.Core.Text;
using Provodnik.Features.Install;

namespace Provodnik.Wpf.Shared.Services
{
  /// <summary>
  /// A instalação do GAMMA que está aberta: a pasta, o mods.txt local, o ModOrganizer.ini e o que o Проводник lembra de
  /// ter instalado. Lido do disco no começo e de novo depois de cada instalação (<see cref="Reload"/>).
  ///
  /// A pasta é achada sozinha quando o exe mora dentro do GAMMA (na raiz ou no .Grok's Modpack Installer), que é o
  /// jeito pensado de usar; senão vale a escolhida nas configurações, e por último um GAMMA na raiz de algum disco.
  /// </summary>
  public sealed partial class GammaSession : ObservableObject
  {
    private readonly SettingsStore _settings;

    [ObservableProperty] private GammaLayout? _layout;
    [ObservableProperty] private ModList? _modList;
    [ObservableProperty] private Mo2Config _mo2 = Mo2Config.Empty;
    [ObservableProperty] private InstallState _state = new();
    [ObservableProperty] private ModpackMetadata _metadata = ModpackMetadata.Gamma;
    [ObservableProperty] private string? _installedVersion;

    public GammaSession(SettingsStore settings, AppOptions options)
    {
      _settings = settings;
      // na simulação, só a pasta de mentira: nunca o GAMMA de verdade ao lado do exe
      Layout = options.FromScratch ? null : options.DemoRoot is { } demo ? GammaLayout.Find(demo) : Detect(settings.Current.GammaRoot);
      Reload();
    }

    /// <summary>Releu o disco: as telas atualizam o que mostram.</summary>
    public event EventHandler? Reloaded;

    /// <summary>O exe mora dentro da pasta do GAMMA (não precisa de configuração nenhuma).</summary>
    public bool IsPortable => Layout is { } layout && AppContext.BaseDirectory.StartsWith(layout.Root, StringComparison.OrdinalIgnoreCase);

    public void Reload()
    {
      if (Layout is not { } layout || !layout.Exists)
      {
        ModList = null;
        Mo2 = Mo2Config.Empty;
        State = new InstallState();
        InstalledVersion = null;
        Reloaded?.Invoke(this, EventArgs.Empty);
        return;
      }

      ModList = Read(layout.ModListFile, ModList.Parse);
      Mo2 = Mo2Config.Load(layout.ModOrganizerIni);
      State = new InstallStateStore(Path.Combine(layout.DataDir, "state.json")).Load();
      Metadata = Read(layout.MetadataFile, ModpackMetadata.Parse) ?? ModpackMetadata.Gamma;
      InstalledVersion = Read(layout.VersionFile, t => t.Trim());
      Reloaded?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Troca a pasta do GAMMA (as configurações). Devolve o motivo quando a pasta não serve.</summary>
    public string? SetRoot(string folder)
    {
      var layout = GammaLayout.Find(folder);
      if (layout is null)
        return L.T("shared.gamma.notFound", folder, GammaLayout.InstallerFolderName);

      Layout = layout;
      _settings.Update(s => s with { GammaRoot = IsPortable ? null : layout.Root });
      Reload();
      return null;
    }

    private static GammaLayout? Detect(string? saved)
    {
      if (GammaLayout.Find(AppContext.BaseDirectory) is { } beside)
        return beside;
      if (saved is { Length: > 0 } && GammaLayout.Find(saved) is { } chosen)
        return chosen;

      // o caminho que o guia do GAMMA recomenda: X:\GAMMA
      foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady))
      {
        foreach (var name in new[] { "GAMMA", @"Games\GAMMA", @"Jogos\GAMMA" })
        {
          if (GammaLayout.Find(Path.Combine(drive.RootDirectory.FullName, name)) is { } found)
            return found;
        }
      }
      return null;
    }

    private static T? Read<T>(string path, Func<string, T> parse)
    {
      try
      {
        return File.Exists(path) ? parse(File.ReadAllText(path)) : default;
      }
      catch (IOException)
      {
        return default;
      }
    }
  }
}
