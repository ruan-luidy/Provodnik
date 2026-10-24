namespace Provodnik.Core.Modpack
{
  /// <summary>
  /// Uma linha do mods.txt, com as colunas separadas por tab:
  /// link · instruções · autor (" - Fulano") · nome · página · arquivo · md5.
  /// O <see cref="Index"/> conta todas as linhas a partir de 1, separadores inclusive: é ele que numera a pasta no
  /// mods\ do MO2 ("12- PDA Radio Extended - Starcry_") e o modlist.txt do GAMMA depende desse número.
  /// </summary>
  public sealed record ModEntry(
    int Index,
    ModKind Kind,
    string? Url,
    IReadOnlyList<string> Instructions,
    string Suffix,
    string Name,
    string? PageUrl,
    string? ArchiveName,
    string? Md5)
  {
    public bool IsSeparator => Kind == ModKind.Separator;

    /// <summary>A pasta dentro de mods\, exatamente como o launcher original monta.</summary>
    public string FolderName => IsSeparator ? $"{Index}-{Name}_separator" : $"{Index}-{Name}{Suffix}";

    public string Title => Name.Trim();

    /// <summary>"DesmanMetzger" a partir de " - DesmanMetzger".</summary>
    public string Author => Suffix.Trim().TrimStart('-').Trim();

    /// <summary>Página pra abrir no navegador: a do addon quando tem, senão o próprio link.</summary>
    public string? WebPage =>
      PageUrl is { Length: > 0 } page && page.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? page : Url;
  }
}
