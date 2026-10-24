namespace Provodnik.Core.Install
{
  /// <summary>Um mod que não entrou, com o motivo pronto pra tela.</summary>
  public sealed record ModFailure(int Index, string Name, string Reason);

  /// <summary>Como terminou uma instalação.</summary>
  public sealed record InstallReport(
    int Installed,
    int UpToDate,
    IReadOnlyList<ModFailure> Failures,
    IReadOnlyList<string> Warnings,
    TimeSpan Elapsed,
    bool Cancelled)
  {
    public bool Succeeded => !Cancelled && Failures.Count == 0;
  }
}
