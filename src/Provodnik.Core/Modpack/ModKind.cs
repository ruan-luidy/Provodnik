namespace Provodnik.Core.Modpack
{
  /// <summary>De onde vem uma linha do mods.txt. O launcher original decide pelo texto do link, e aqui é igual.</summary>
  public enum ModKind
  {
    Separator,
    ModDb,
    GitHub,
    GoFile,
    LargeFiles,
  }
}
