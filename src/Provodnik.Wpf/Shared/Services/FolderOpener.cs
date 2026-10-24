using System.Diagnostics;
using System.IO;

namespace Provodnik.Wpf.Shared.Services
{
  /// <summary>Abre pasta no Explorer e link no navegador.</summary>
  public static class FolderOpener
  {
    public static void Open(string? path)
    {
      if (string.IsNullOrEmpty(path))
        return;
      if (File.Exists(path))
      {
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = false });
        return;
      }
      if (Directory.Exists(path))
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
      else
        ToastService.Warning($"{path} não existe.");
    }

    public static void Browse(string? url)
    {
      if (url is { Length: > 0 } && url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
  }
}
