using System.Security.Principal;
using Microsoft.Win32;
using Provodnik.Core.Desktop;

namespace Provodnik.Infrastructure.Desktop
{
  /// <summary>Administrador e caminhos longos, direto do Windows.</summary>
  public sealed class SystemInfo : ISystemInfo
  {
    public bool IsElevated { get; } = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

    private const string WebView2Key = @"Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}";

    public string? WebView2Version =>
      Read(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\" + WebView2Key) ?? Read(Registry.LocalMachine, @"SOFTWARE\" + WebView2Key) ?? Read(Registry.CurrentUser, @"Software\" + WebView2Key);

    private static string? Read(RegistryKey root, string path)
    {
      try
      {
        using var key = root.OpenSubKey(path);
        return key?.GetValue("pv") is string version && version.Length > 0 && version != "0.0.0.0" ? version : null;
      }
      catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
      {
        return null;
      }
    }

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
