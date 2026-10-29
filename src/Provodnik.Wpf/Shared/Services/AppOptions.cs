namespace Provodnik.Wpf.Shared.Services
{
  /// <summary>
  /// Como o app foi aberto. Demo: a simulação (--demo), com GAMMA e ferramentas de mentira em %TEMP%. FromScratch
  /// (--do-zero): finge que não tem GAMMA nenhum, pra ver e testar a instalação do zero numa máquina que já tem.
  /// </summary>
  public sealed record AppOptions(bool Demo, string? DemoRoot = null, string? ShotsFolder = null, bool FromScratch = false)
  {
    public const string FromScratchFlag = "--do-zero";

    /// <summary>"--continuar-instalacao exe": aberto pela instalação do zero, apaga o exe baixado e começa a instalação.</summary>
    public const string ContinueFlag = "--continuar-instalacao";

    /// <summary>Tirando as capturas do README (--demo --shots pasta): a tela sai sem o selo DEMO.</summary>
    public bool Shots => ShotsFolder is not null;
  }
}
