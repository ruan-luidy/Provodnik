namespace Provodnik.Wpf.Shared.Services
{
  /// <summary>Como o app foi aberto. Demo: a simulação (--demo), com GAMMA e ferramentas de mentira em %TEMP%.</summary>
  public sealed record AppOptions(bool Demo, string? DemoRoot = null, string? ShotsFolder = null)
  {
    /// <summary>Tirando as capturas do README (--demo --shots pasta): a tela sai sem o selo DEMO.</summary>
    public bool Shots => ShotsFolder is not null;
  }
}
