namespace Provodnik.Wpf.Shared.Services
{
  /// <summary>Como o app foi aberto. Demo: a simulação (--demo), com GAMMA e ferramentas de mentira em %TEMP%.</summary>
  public sealed record AppOptions(bool Demo, string? DemoRoot = null);
}
