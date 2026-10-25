using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Provodnik.Core.Desktop;

namespace Provodnik.Infrastructure.Desktop
{
  /// <summary>
  /// O .lnk pelo IShellLinkW do próprio Windows, em Unicode: o WScript.Shell do original gravava o nome em ANSI e o
  /// "Проводник" virava "?????????". O caminho da área de trabalho vem de quem chama.
  /// </summary>
  public sealed class ShortcutWriter : IShortcutWriter
  {
    public void Create(string shortcutPath, string target, string? icon = null, string? workingDirectory = null, string? arguments = null)
    {
      var link = (IShellLinkW)new ShellLink();
      try
      {
        link.SetPath(target);
        if (arguments is not null)
          link.SetArguments(arguments);
        link.SetWorkingDirectory(workingDirectory ?? Path.GetDirectoryName(target) ?? "");
        if (icon is not null && File.Exists(icon))
          link.SetIconLocation(icon, 0);
        ((IPersistFile)link).Save(shortcutPath, true);
      }
      finally
      {
        Marshal.FinalReleaseComObject(link);
      }
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink { }

    // a ordem dos métodos é a da vtable do shobjidl.h: não mexer
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
      void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int size, IntPtr findData, uint flags);
      void GetIDList(out IntPtr idList);
      void SetIDList(IntPtr idList);
      void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int size);
      void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
      void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder dir, int size);
      void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string dir);
      void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder args, int size);
      void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string args);
      void GetHotkey(out short hotkey);
      void SetHotkey(short hotkey);
      void GetShowCmd(out int showCmd);
      void SetShowCmd(int showCmd);
      void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder iconPath, int size, out int index);
      void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int index);
      void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string relativePath, uint reserved);
      void Resolve(IntPtr hwnd, uint flags);
      void SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);
    }
  }
}
