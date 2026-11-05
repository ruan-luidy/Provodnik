using Provodnik.Core.Modpack;

namespace Provodnik.Features.Mods
{
  /// <summary>
  /// O crédito do fim da instalação: quantos mods do ModDB e de quantos autores. Conta os do ModDB que esta rodada
  /// instalou; numa rodada que não instalou nenhum (tudo em dia), os do ModDB da lista inteira.
  /// </summary>
  public sealed record ModDbCredits(int Mods, int Authors, bool ThisRun)
  {
    public static ModDbCredits? From(IEnumerable<ModEntry> installed, IEnumerable<ModEntry> all)
    {
      var now = installed.Where(e => e.Kind == ModKind.ModDb).ToList();
      var mods = now.Count > 0 ? now : all.Where(e => e.Kind == ModKind.ModDb).ToList();
      if (mods.Count == 0)
        return null;

      var authors = mods.Select(e => e.Author).Where(a => a.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Count();
      return new ModDbCredits(mods.Count, authors, now.Count > 0);
    }
  }
}
