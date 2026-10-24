using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using Provodnik.Core.Desktop;

namespace Provodnik.Wpf.Shared.Services
{
  /// <summary>
  /// Administrador só quando precisa: a checagem antes de instalar descobre que a pasta não deixa gravar e oferece
  /// reabrir elevado. E o único conserto que mexe no Windows (caminhos longos) pede o administrador só pro reg.exe,
  /// sem elevar o app inteiro.
  /// </summary>
  public sealed class ElevationService(ISystemInfo system, IShortcutWriter shortcuts)
  {
    public bool IsElevated => system.IsElevated;

    /// <summary>Com o app elevado, o MO2 e o jogo abrem por aqui pra não herdarem o administrador.</summary>
    public IShortcutWriter? Unelevated => system.IsElevated ? shortcuts : null;

    /// <summary>Reabre como administrador (o UAC pergunta). Falso quando a pessoa disse não.</summary>
    public bool RestartAsAdmin() => ((App)Application.Current).RestartElevated();

    /// <summary>LongPathsEnabled = 1 pelo reg.exe elevado. Falso quando a pessoa disse não ao UAC ou deu erro.</summary>
    public async Task<bool> EnableLongPathsAsync()
    {
      try
      {
        using var process = Process.Start(new ProcessStartInfo(
          "reg.exe",
          @"add ""HKLM\SYSTEM\CurrentControlSet\Control\FileSystem"" /v LongPathsEnabled /t REG_DWORD /d 1 /f")
        {
          Verb = "runas",
          UseShellExecute = true,
          WindowStyle = ProcessWindowStyle.Hidden,
        });
        if (process is null)
          return false;
        await process.WaitForExitAsync();
        return process.ExitCode == 0;
      }
      catch (Win32Exception)
      {
        // cancelou o UAC
        return false;
      }
    }
  }
}
