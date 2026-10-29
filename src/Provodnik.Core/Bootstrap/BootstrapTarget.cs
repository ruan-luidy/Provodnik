using Provodnik.Core.Layout;
using Provodnik.Core.Text;

namespace Provodnik.Core.Bootstrap
{
  public enum BootstrapIssueLevel
  {
    Info,
    Warning,
    Blocker,
  }

  public sealed record BootstrapIssue(BootstrapIssueLevel Level, string Text);

  /// <summary>O que a checagem precisa saber do disco escolhido (o Core não lê o Windows).</summary>
  public sealed record BootstrapFacts(DriveType Drive, long FreeBytes, IReadOnlyList<string> ForbiddenFolders);

  /// <summary>
  /// Onde o GAMMA e o Anomaly vão morar numa instalação do zero, com as regras da wiki: duas pastas separadas, no mesmo
  /// disco, uma fora da outra, longe da pasta do usuário (Documentos, Área de Trabalho, OneDrive), do Program Files e de
  /// pendrive. O padrão é a pasta escolhida com GAMMA\ e Anomaly\ dentro; o Anomaly pode ser um 1.5.3 que já existe.
  /// O que o Проводник baixa e extrai no caminho fica em GAMMA\.provodnik-bootstrap e sai no fim.
  /// </summary>
  public sealed record BootstrapTarget(string Gamma, string Anomaly)
  {
    private const long GB = 1L << 30;

    /// <summary>O mínimo da wiki (150 GB); com o GAMMA ou o Anomaly já no lugar, menos o que eles ocupam.</summary>
    public const long MinimumFreeBytes = 150 * GB;

    public const long RecommendedFreeBytes = 200 * GB;

    public static BootstrapTarget Under(string parent)
    {
      var root = Path.GetFullPath(parent);
      return new BootstrapTarget(Path.Combine(root, "GAMMA"), Path.Combine(root, "Anomaly"));
    }

    public string WorkDir => Path.Combine(Gamma, ".provodnik-bootstrap");

    public string Downloads => Path.Combine(WorkDir, "downloads");

    public string Tools => Path.Combine(WorkDir, "tools");

    /// <summary>Onde um arquivo é extraído antes de ir pro lugar (mesmo disco: ir pro lugar é renomear).</summary>
    public string Staging(string name) => Path.Combine(WorkDir, "staging", name);

    public GammaLayout Layout => new(Gamma);

    /// <summary>O RC3 já foi extraído: o MO2 e as ferramentas do instalador estão lá.</summary>
    public bool GammaReady => File.Exists(Path.Combine(Gamma, "ModOrganizer.exe")) && Directory.Exists(Layout.Resources);

    /// <summary>Um Anomaly inteiro na pasta (o launcher, o exe do jogo e os .db).</summary>
    public bool AnomalyReady =>
      File.Exists(Path.Combine(Anomaly, "AnomalyLauncher.exe"))
      && File.Exists(Path.Combine(Anomaly, "bin", "AnomalyDX11AVX.exe"))
      && Directory.Exists(Path.Combine(Anomaly, "db"));

    /// <summary>Quanto espaço livre precisa agora, descontando o que já está no lugar.</summary>
    public long RequiredFreeBytes => Math.Max(0, MinimumFreeBytes - (GammaReady ? 39 * GB : 0) - (AnomalyReady ? 25 * GB : 0));

    public IReadOnlyList<BootstrapIssue> Check(BootstrapFacts facts)
    {
      var issues = new List<BootstrapIssue>();
      void Block(string key, params object?[] args) => issues.Add(new(BootstrapIssueLevel.Blocker, L.T(key, args)));
      void Warn(string key, params object?[] args) => issues.Add(new(BootstrapIssueLevel.Warning, L.T(key, args)));

      if (!Path.IsPathFullyQualified(Gamma) || !Path.IsPathFullyQualified(Anomaly))
      {
        Block("core.bootstrap.notRooted");
        return issues;
      }

      var gamma = Normalize(Gamma);
      var anomaly = Normalize(Anomaly);
      if (!string.Equals(Path.GetPathRoot(gamma), Path.GetPathRoot(anomaly), StringComparison.OrdinalIgnoreCase))
        Block("core.bootstrap.sameDrive");
      if (Inside(gamma, anomaly) || Inside(anomaly, gamma))
        Block("core.bootstrap.nested");
      if (Path.GetPathRoot(gamma) is { } root && (Same(gamma, root) || Same(anomaly, root)))
        Block("core.bootstrap.driveRoot");

      foreach (var forbidden in facts.ForbiddenFolders.Where(f => f.Length > 0).Select(Normalize))
      {
        if (Inside(forbidden, gamma) || Same(forbidden, gamma) || Inside(forbidden, anomaly) || Same(forbidden, anomaly))
        {
          Block("core.bootstrap.forbidden", forbidden);
          break;
        }
      }

      switch (facts.Drive)
      {
        case DriveType.Removable:
          Block("core.bootstrap.removable");
          break;
        case DriveType.Network:
          Block("core.bootstrap.network");
          break;
        case DriveType.Unknown or DriveType.NoRootDirectory or DriveType.CDRom:
          Block("core.bootstrap.noDrive");
          break;
      }

      if (facts.FreeBytes < RequiredFreeBytes)
        Block("core.bootstrap.space", ByteSize.Format(facts.FreeBytes), ByteSize.Format(RequiredFreeBytes));
      else if (facts.FreeBytes < RecommendedFreeBytes && !GammaReady)
        Warn("core.bootstrap.spaceTight", ByteSize.Format(facts.FreeBytes));

      // o motor do jogo (X-Ray) se perde com acento e cirílico no caminho
      if ((gamma + anomaly).Any(c => c > 127))
        Warn("core.bootstrap.nonAscii");

      if (AnomalyReady)
        issues.Add(new(BootstrapIssueLevel.Info, L.T("core.bootstrap.anomalyFound")));
      else if (HasContent(Anomaly, except: null))
        Warn("core.bootstrap.anomalyNotEmpty");

      if (GammaReady)
        issues.Add(new(BootstrapIssueLevel.Info, L.T("core.bootstrap.gammaFound")));
      else if (HasContent(Gamma, except: Path.GetFileName(WorkDir)))
        Warn("core.bootstrap.gammaNotEmpty");

      return issues;
    }

    private static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static bool Same(string a, string b) => string.Equals(Normalize(a), Normalize(b), StringComparison.OrdinalIgnoreCase);

    /// <summary><paramref name="child"/> mora dentro de <paramref name="parent"/>.</summary>
    private static bool Inside(string parent, string child) =>
      child.StartsWith(Path.TrimEndingDirectorySeparator(parent) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static bool HasContent(string folder, string? except) =>
      Directory.Exists(folder)
      && Directory.EnumerateFileSystemEntries(folder).Any(e => except is null || !string.Equals(Path.GetFileName(e), except, StringComparison.OrdinalIgnoreCase));
  }
}
