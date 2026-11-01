namespace Provodnik.Core.Desktop
{
  /// <summary>Atalho .lnk na área de trabalho (a de verdade, mesmo quando o OneDrive mudou ela de lugar).</summary>
  public interface IShortcutWriter
  {
    void Create(string shortcutPath, string target, string? icon = null, string? workingDirectory = null, string? arguments = null);

    /// <summary>Pra onde o atalho que já existe aponta (nulo: não existe ou não deu pra ler).</summary>
    string? TargetOf(string shortcutPath);
  }
}
