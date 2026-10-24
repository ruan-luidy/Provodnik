namespace Provodnik.Wpf.Shared
{
  /// <summary>Tom de uma linha: decide a cor do estado (bolinha, pílula, texto).</summary>
  public enum RowTone
  {
    Neutral,
    Success,
    Warning,
    Error,

    /// <summary>Fora de jogo (mod que o GAMMA deixa desligado): cinza, não pede nada.</summary>
    Muted,
  }
}
