using System.Security.Principal;
using Microsoft.Win32;
using Provodnik.Core.Desktop;

namespace Provodnik.Infrastructure.Desktop
{
  /// <summary>Administrador e caminhos longos, direto do Windows.</summary>
  public sealed class SystemInfo : ISystemInfo
  {
    public bool IsElevated { get; } = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

    public bool? LongPathsEnabled
    {
      get
      {
        try
        {
          using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\FileSystem");
          return key?.GetValue("LongPathsEnabled") is int value ? value == 1 : null;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
          return null;
        }
      }
    }
  }
}
