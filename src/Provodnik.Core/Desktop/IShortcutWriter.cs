namespace Provodnik.Core.Desktop
{
  /// <summary>Atalho .lnk na área de trabalho (a de verdade, mesmo quando o OneDrive mudou ela de lugar).</summary>
  public interface IShortcutWriter
  {
    void Create(string shortcutPath, string target, string? icon = null, string? workingDirectory = null);
  }
}
