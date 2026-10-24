namespace Provodnik.Core.Install
{
  /// <summary>As etapas de uma instalação, na ordem.</summary>
  public enum InstallStage
  {
    Preparing,
    Repositories,
    Definition,
    ModList,
    Mods,
    Extras,
    Profile,
    Patches,
    Shortcuts,
    Verify,
  }

  public enum StageState
  {
    Pending,
    Running,
    Done,
    Warning,
    Failed,
    Skipped,
  }

  /// <summary>Onde cada mod está.</summary>
  public enum ModStatus
  {
    Pending,
    Checking,
    Resolving,
    Downloading,
    /// <summary>Falhou e espera a próxima tentativa (a barra para; o recado diz quando volta).</summary>
    Retrying,
    Verifying,
    WaitingExtraction,
    Extracting,
    Installed,
    UpToDate,
    Failed,
  }

  public enum LogLevel
  {
    Info,
    Success,
    Warning,
    Error,
  }

  /// <summary>Um momento de um mod: o estado, quanto já foi (download ou extração) e um detalhe pra tela.</summary>
  public sealed record ModProgress(
    int Index,
    ModStatus Status,
    double? Fraction = null,
    long Received = 0,
    long? Total = null,
    double? BytesPerSecond = null,
    string? Detail = null);

  /// <summary>
  /// Quem acompanha a instalação (a tela, o log em arquivo). Chamado de várias threads ao mesmo tempo: quem
  /// implementa passa pra thread da tela.
  /// </summary>
  public interface IInstallReporter
  {
    void Stage(InstallStage stage, StageState state, string? detail = null);

    void Mod(ModProgress progress);

    void Log(LogLevel level, string message);
  }
}
