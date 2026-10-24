using System.IO;

namespace Provodnik.Wpf.Shared.Services
{
  /// <summary>
  /// O settings.json vivo: quem muda uma preferência passa por <see cref="Update"/>, que grava na hora e avisa quem
  /// depende dela. O <see cref="AppSettings"/> é imutável; aqui fica o atual.
  /// </summary>
  public sealed class SettingsStore(AppSettings initial)
  {
    public AppSettings Current { get; private set; } = initial;

    public event EventHandler? Changed;

    public void Update(Func<AppSettings, AppSettings> change)
    {
      Current = change(Current);
      try
      {
        Current.Save();
      }
      catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
      {
        AppLog.Error(ex, "Salvando settings.json");
      }

      Changed?.Invoke(this, EventArgs.Empty);
    }
  }
}
