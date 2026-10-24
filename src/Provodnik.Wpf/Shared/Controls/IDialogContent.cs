namespace Provodnik.Wpf.Shared.Controls
{
  /// <summary>
  /// Um diálogo aberto pelo <see cref="Services.DialogService.ShowAsync(IDialogContent)"/>: avisa quando terminou,
  /// com true quando a pessoa confirmou (Entrar, Salvar) e false quando cancelou ou fechou no X.
  /// </summary>
  public interface IDialogContent
  {
    event EventHandler<bool>? Closed;
  }
}
