using System.Runtime.InteropServices;
using Provodnik.Core.Desktop;

namespace Provodnik.Infrastructure.Desktop
{
  /// <summary>O .lnk pelo WScript.Shell, como o original, mas o caminho da área de trabalho vem de quem chama.</summary>
  public sealed class ShortcutWriter : IShortcutWriter
  {
    public void Create(string shortcutPath, string target, string? icon = null, string? workingDirectory = null)
    {
      var type = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("WScript.Shell indisponível");
      dynamic shell = Activator.CreateInstance(type)!;
      try
      {
        var link = shell.CreateShortcut(shortcutPath);
        link.TargetPath = target;
        link.WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(target);
        if (icon is not null && File.Exists(icon))
          link.IconLocation = icon;
        link.Save();
        Marshal.FinalReleaseComObject(link);
      }
      finally
      {
        Marshal.FinalReleaseComObject(shell);
      }
    }
  }
}
