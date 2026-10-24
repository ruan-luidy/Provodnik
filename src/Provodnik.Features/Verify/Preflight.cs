using Provodnik.Core.Desktop;
using Provodnik.Core.Layout;
using Provodnik.Core.Text;
using Provodnik.Features.Launch;

namespace Provodnik.Features.Verify
{
  public enum CheckLevel
  {
    Ok,
    Warning,
    Failed,
  }

  /// <summary>O conserto que a tela oferece num botão, quando tem.</summary>
  public enum CheckFix
  {
    None,
    RunAsAdmin,
    EnableLongPaths,
  }

  public sealed record PreflightCheck(string Title, CheckLevel Level, string Message, CheckFix Fix = CheckFix.None);

  /// <summary>
  /// A checagem antes de instalar, pra que o problema apareça aqui, explicado, e não no meio dos 470 mods: pasta
  /// gravável (é aqui que entra o administrador, e só quando precisa), Anomaly achado, MO2 e jogo fechados, espaço,
  /// ferramentas do GAMMA presentes (antivírus adora apagar) e caminhos longos.
  /// </summary>
  public static class Preflight
  {
    private const long Gigabyte = 1024L * 1024 * 1024;

    public static IReadOnlyList<PreflightCheck> Run(GammaLayout layout, ISystemInfo system)
    {
      var checks = new List<PreflightCheck>();
      if (!layout.Exists)
      {
        checks.Add(new(L.T("features.preflight.folder.title"), CheckLevel.Failed, L.T("features.preflight.folder.invalid", layout.Root, GammaLayout.InstallerFolderName)));
        return checks;
      }

      checks.Add(Writable(layout, system));
      checks.Add(Anomaly(layout, system));
      checks.Add(Running(system));
      checks.Add(Space(layout, system));
      checks.Add(Tools(layout));
      checks.Add(Browser(layout, system));
      checks.Add(LongPaths(layout, system));
      return checks;
    }

    private static PreflightCheck Writable(GammaLayout layout, ISystemInfo system)
    {
      var denied = new[] { layout.Root, layout.Installer, layout.Mods, layout.Downloads }.FirstOrDefault(d => !CanWrite(d));
      if (denied is null)
        return new(L.T("features.preflight.writable.title"), CheckLevel.Ok, system.IsElevated ? L.T("features.preflight.writable.okAdmin") : L.T("features.preflight.writable.ok"));

      return new(
        L.T("features.preflight.writable.title"),
        CheckLevel.Failed,
        system.IsElevated
          ? L.T("features.preflight.writable.deniedAdmin", denied)
          : L.T("features.preflight.writable.denied", denied),
        system.IsElevated ? CheckFix.None : CheckFix.RunAsAdmin);
    }

    private static PreflightCheck Anomaly(GammaLayout layout, ISystemInfo system)
    {
      var anomaly = Mo2Config.Load(layout.ModOrganizerIni).GamePath;
      if (anomaly is null || !Directory.Exists(Path.Combine(anomaly, "bin")))
        return new("Anomaly", CheckLevel.Warning, L.T("features.preflight.anomaly.unknown"));
      if (!CanWrite(Path.Combine(anomaly, "bin")))
        return new("Anomaly", CheckLevel.Failed, L.T("features.preflight.anomaly.denied", anomaly), system.IsElevated ? CheckFix.None : CheckFix.RunAsAdmin);
      return new("Anomaly", CheckLevel.Ok, anomaly);
    }

    private static PreflightCheck Running(ISystemInfo system)
    {
      if (system.IsGameRunning)
        return new(L.T("features.preflight.running.title"), CheckLevel.Failed, L.T("features.preflight.running.game"));
      if (system.IsModOrganizerRunning)
        return new(L.T("features.preflight.running.title"), CheckLevel.Failed, L.T("features.preflight.running.mo2"));
      return new(L.T("features.preflight.running.title"), CheckLevel.Ok, L.T("features.preflight.running.ok"));
    }

    private static PreflightCheck Space(GammaLayout layout, ISystemInfo system)
    {
      var free = system.FreeSpace(layout.Root);
      var title = L.T("features.preflight.space.title");
      var text = L.T("features.preflight.space.free", ByteSize.Format(free), Path.GetPathRoot(layout.Root));
      return free switch
      {
        < 5 * Gigabyte => new(title, CheckLevel.Failed, L.T("features.preflight.space.tooLow", text)),
        < 40 * Gigabyte => new(title, CheckLevel.Warning, L.T("features.preflight.space.low", text)),
        _ => new(title, CheckLevel.Ok, text),
      };
    }

    private static PreflightCheck Tools(GammaLayout layout)
    {
      var tools = new[] { layout.SevenZip, layout.Curl, layout.CurlConfig, layout.Git };
      var missing = tools.Where(t => !File.Exists(t)).Select(Path.GetFileName).ToList();
      return missing.Count == 0
        ? new(L.T("features.preflight.tools.title"), CheckLevel.Ok, L.T("features.preflight.tools.ok"))
        : new(L.T("features.preflight.tools.title"), missing.SequenceEqual(["git.exe"]) ? CheckLevel.Warning : CheckLevel.Failed,
          L.T("features.preflight.tools.missing", string.Join(", ", missing)));
    }

    /// <summary>O ModDB agora exige navegador de verdade pra passar pelo Cloudflare: sem o WebView2 sobra o dl-cli do GAMMA, que está falhando.</summary>
    private static PreflightCheck Browser(GammaLayout layout, ISystemInfo system)
    {
      if (system.WebView2Version is { } version)
        return new(L.T("features.preflight.browser.title"), CheckLevel.Ok, L.T("features.preflight.browser.ok", version));
      return new(L.T("features.preflight.browser.title"), CheckLevel.Warning, File.Exists(layout.ModDbCli)
        ? L.T("features.preflight.browser.dlCliOnly")
        : L.T("features.preflight.browser.none"));
    }

    private static PreflightCheck LongPaths(GammaLayout layout, ISystemInfo system)
    {
      if (system.LongPathsEnabled == false)
        return new(L.T("features.preflight.longPaths.title"), CheckLevel.Warning, L.T("features.preflight.longPaths.off"), CheckFix.EnableLongPaths);
      if (layout.Root.Length > 40)
        return new(L.T("features.preflight.longPaths.title"), CheckLevel.Warning, L.T("features.preflight.longPaths.longRoot", layout.Root.Length));
      return new(L.T("features.preflight.longPaths.title"), CheckLevel.Ok, system.LongPathsEnabled == true ? L.T("features.preflight.longPaths.on") : L.T("features.preflight.longPaths.short"));
    }

    private static bool CanWrite(string directory)
    {
      try
      {
        var target = directory;
        while (!Directory.Exists(target))
          target = Path.GetDirectoryName(target) ?? throw new DirectoryNotFoundException(directory);
        var probe = Path.Combine(target, $".provodnik-{Guid.NewGuid():N}.tmp");
        using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose))
        {
        }
        return true;
      }
      catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
      {
        return false;
      }
    }
  }
}
