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
        checks.Add(new("Pasta do GAMMA", CheckLevel.Failed, $"{layout.Root} não tem o ModOrganizer.exe ou a pasta {GammaLayout.InstallerFolderName}."));
        return checks;
      }

      checks.Add(Writable(layout, system));
      checks.Add(Anomaly(layout, system));
      checks.Add(Running());
      checks.Add(Space(layout));
      checks.Add(Tools(layout));
      checks.Add(Browser(layout, system));
      checks.Add(LongPaths(layout, system));
      return checks;
    }

    private static PreflightCheck Writable(GammaLayout layout, ISystemInfo system)
    {
      var denied = new[] { layout.Root, layout.Installer, layout.Mods, layout.Downloads }.FirstOrDefault(d => !CanWrite(d));
      if (denied is null)
        return new("Permissão nas pastas", CheckLevel.Ok, system.IsElevated ? "Gravando normalmente (rodando como administrador)." : "Gravando normalmente, sem precisar de administrador.");

      return new(
        "Permissão nas pastas",
        CheckLevel.Failed,
        system.IsElevated
          ? $"Nem como administrador dá pra gravar em {denied}. Feche o MO2 e o jogo e confira o antivírus (Acesso controlado a pastas)."
          : $"Sem permissão pra gravar em {denied}. Reabra como administrador ou mova o GAMMA pra uma pasta sua, como C:\\GAMMA.",
        system.IsElevated ? CheckFix.None : CheckFix.RunAsAdmin);
    }

    private static PreflightCheck Anomaly(GammaLayout layout, ISystemInfo system)
    {
      var anomaly = Mo2Config.Load(layout.ModOrganizerIni).GamePath;
      if (anomaly is null || !Directory.Exists(Path.Combine(anomaly, "bin")))
        return new("Anomaly", CheckLevel.Warning, "O MO2 ainda não sabe onde está o Anomaly: abra o MO2 uma vez e escolha a pasta (os patches do fim precisam dela).");
      if (!CanWrite(Path.Combine(anomaly, "bin")))
        return new("Anomaly", CheckLevel.Failed, $"Sem permissão pra gravar em {anomaly} (os exes do GAMMA vão pra lá).", system.IsElevated ? CheckFix.None : CheckFix.RunAsAdmin);
      return new("Anomaly", CheckLevel.Ok, anomaly);
    }

    private static PreflightCheck Running()
    {
      if (GameLauncher.IsGameRunning())
        return new("Jogo e MO2 fechados", CheckLevel.Failed, "O Anomaly está aberto. Feche o jogo antes de instalar.");
      if (GameLauncher.IsModOrganizerRunning())
        return new("Jogo e MO2 fechados", CheckLevel.Failed, "O Mod Organizer está aberto e segura os arquivos dos mods. Feche ele antes de instalar.");
      return new("Jogo e MO2 fechados", CheckLevel.Ok, "Nada segurando os arquivos.");
    }

    private static PreflightCheck Space(GammaLayout layout)
    {
      var free = new DriveInfo(Path.GetPathRoot(layout.Root)!).AvailableFreeSpace;
      var text = $"{ByteSize.Format(free)} livres em {Path.GetPathRoot(layout.Root)}";
      return free switch
      {
        < 5 * Gigabyte => new("Espaço em disco", CheckLevel.Failed, $"{text}. Precisa de bem mais: uma instalação do zero ocupa uns 150 GB."),
        < 40 * Gigabyte => new("Espaço em disco", CheckLevel.Warning, $"{text}. Dá pra atualizar, mas uma instalação do zero precisa de uns 150 GB."),
        _ => new("Espaço em disco", CheckLevel.Ok, text),
      };
    }

    private static PreflightCheck Tools(GammaLayout layout)
    {
      var tools = new[] { layout.SevenZip, layout.Curl, layout.CurlConfig, layout.Git };
      var missing = tools.Where(t => !File.Exists(t)).Select(Path.GetFileName).ToList();
      return missing.Count == 0
        ? new("Ferramentas do GAMMA", CheckLevel.Ok, "7-Zip, curl-impersonate e Git no lugar.")
        : new("Ferramentas do GAMMA", missing.SequenceEqual(["git.exe"]) ? CheckLevel.Warning : CheckLevel.Failed,
          $"Faltam em resources\\: {string.Join(", ", missing)}. Quase sempre é o antivírus: restaure da quarentena, coloque a pasta do GAMMA nas exclusões ou baixe o instalador do GAMMA de novo.");
    }

    /// <summary>O ModDB agora exige navegador de verdade pra passar pelo Cloudflare: sem o WebView2 sobra o dl-cli do GAMMA, que está falhando.</summary>
    private static PreflightCheck Browser(GammaLayout layout, ISystemInfo system)
    {
      if (system.WebView2Version is { } version)
        return new("Navegador do ModDB", CheckLevel.Ok, $"WebView2 {version}: passa pelo Cloudflare do ModDB.");
      return new("Navegador do ModDB", CheckLevel.Warning, File.Exists(layout.ModDbCli)
        ? "Sem o WebView2 Runtime: os links do ModDB vão pelo stalker-gamma-dl-cli, que hoje está falhando. Instale o WebView2 Runtime da Microsoft."
        : "Sem o WebView2 Runtime e sem o stalker-gamma-dl-cli: os mods do ModDB não vão baixar. Instale o WebView2 Runtime da Microsoft.");
    }

    private static PreflightCheck LongPaths(GammaLayout layout, ISystemInfo system)
    {
      if (system.LongPathsEnabled == false)
        return new("Caminhos longos", CheckLevel.Warning, "Desligados no Windows: alguns addons têm caminhos compridos e podem falhar ao extrair ou no jogo.", CheckFix.EnableLongPaths);
      if (layout.Root.Length > 40)
        return new("Caminhos longos", CheckLevel.Warning, $"A pasta do GAMMA tem um caminho comprido ({layout.Root.Length} letras). O guia do GAMMA recomenda algo curto, como C:\\GAMMA.");
      return new("Caminhos longos", CheckLevel.Ok, system.LongPathsEnabled == true ? "Ligados no Windows." : "Caminho curto.");
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
