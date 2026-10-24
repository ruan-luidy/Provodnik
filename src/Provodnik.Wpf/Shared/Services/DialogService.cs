using System.Windows;
using HandyControl.Controls;
using Provodnik.Wpf.Shared.Controls;

namespace Provodnik.Wpf.Shared.Services
{
  /// <summary>
  /// Diálogos do Provodnik (o DialogService do rnzNegativeObserver): o <see cref="AlertDialog"/> por cima da janela,
  /// com o fundo escurecido do Dialog do HandyControl - nada de janela nova nem do MessageBox cru. Tudo assíncrono:
  /// quem pergunta espera a resposta com await.
  /// Sem janela principal na tela ainda (erro na partida), cai no MessageBox do Windows, que não depende de nada.
  /// </summary>
  public static class DialogService
  {
    /// <summary>
    /// A janela principal se registra com este token (hc:Dialog.Token): o diálogo abre sempre nela, e não na janela
    /// que estiver ativa - o toast da área de trabalho também é janela, e o diálogo ia parar lá.
    /// </summary>
    public const string Token = "Provodnik.Main";

    public static Task InfoAsync(string title, string message, string? details = null) =>
      AlertAsync(DialogKind.Info, title, message, details);

    public static Task SuccessAsync(string title, string message, string? details = null) =>
      AlertAsync(DialogKind.Success, title, message, details);

    public static Task WarningAsync(string title, string message, string? details = null) =>
      AlertAsync(DialogKind.Warning, title, message, details);

    public static Task ErrorAsync(string title, string message, string? details = null) =>
      AlertAsync(DialogKind.Error, title, message, details);

    /// <summary>Um botão (OK). Termina quando o usuário fecha.</summary>
    public static async Task AlertAsync(DialogKind kind, string title, string message, string? details = null)
    {
      if (!CanShowOverlay())
      {
        var icon = kind == DialogKind.Error ? MessageBoxImage.Error : kind == DialogKind.Warning ? MessageBoxImage.Warning : MessageBoxImage.Information;
        System.Windows.MessageBox.Show(details is null ? message : $"{message}\n\n{details}", title, MessageBoxButton.OK, icon);
        return;
      }

      await ShowAsync(new AlertDialog(kind, title, message, "OK", details: details));
    }

    /// <summary>Dois botões: true no principal (confirmar), false no secundário ou no Esc.</summary>
    public static async Task<bool> ConfirmAsync(string title, string message, string confirmText, string? cancelText = null, DialogKind kind = DialogKind.Warning)
    {
      cancelText ??= "Cancelar";
      if (!CanShowOverlay())
        return System.Windows.MessageBox.Show(message, title, MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK;

      return await ShowAsync(new AlertDialog(kind, title, message, confirmText, cancelText));
    }

    /// <summary>
    /// Abre um diálogo próprio (login, configurações, sobre) por cima da janela. O diálogo implementa
    /// <see cref="IDialogContent"/> e diz quando terminou; o resultado volta pelo await.
    /// </summary>
    public static Task<bool> ShowAsync(IDialogContent content)
    {
      var result = new TaskCompletionSource<bool>();
      Dialog? dialog = null;
      content.Closed += (_, ok) =>
      {
        dialog?.Close();
        if (result.TrySetResult(ok))
          Track(-1);
      };
      Track(+1);
      dialog = Dialog.Show(content, Token);
      return result.Task;
    }

    private static Task<bool> ShowAsync(AlertDialog content)
    {
      var result = new TaskCompletionSource<bool>();
      Dialog? dialog = null;

      void Finish(bool value)
      {
        dialog?.Close();
        if (result.TrySetResult(value))
          Track(-1);
      }

      content.PrimaryClicked += (_, _) => Finish(true);
      content.SecondaryClicked += (_, _) => Finish(false);
      Track(+1);
      dialog = Dialog.Show(content, Token);
      return result.Task;
    }

    private static int _open;

    /// <summary>
    /// Tem diálogo aberto. A máscara escura do HandyControl cobre só a área da janela; a barra de título (que é nossa,
    /// com o Mica) escuta isto pra escurecer junto, senão ela ficava de outra cor por cima do diálogo.
    /// </summary>
    public static bool IsOpen => _open > 0;

    public static event EventHandler? OpenChanged;

    private static void Track(int delta)
    {
      var was = IsOpen;
      _open = Math.Max(0, _open + delta);
      if (was != IsOpen)
        OpenChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>O overlay precisa de uma janela carregada (ele entra na camada de adorno dela).</summary>
    private static bool CanShowOverlay() =>
      Application.Current?.MainWindow is { IsLoaded: true, IsVisible: true };
  }
}
