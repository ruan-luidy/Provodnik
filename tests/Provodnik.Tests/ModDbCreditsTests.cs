using Provodnik.Core.Modpack;
using Provodnik.Features.Mods;

namespace Provodnik.Tests
{
  /// <summary>O crédito do fim da instalação: só o ModDB conta, e cada autor uma vez.</summary>
  public sealed class ModDbCreditsTests
  {
    private static ModEntry Mod(int index, ModKind kind, string author) =>
      new(index, kind, "https://x", ["0"], author.Length > 0 ? $" - {author}" : "", $"Mod {index}", null, $"{index}.zip", null);

    private static readonly ModEntry[] All =
    [
      Mod(1, ModKind.ModDb, "Ana"),
      Mod(2, ModKind.ModDb, "ana"),
      Mod(3, ModKind.ModDb, "Bia"),
      Mod(4, ModKind.GitHub, "Caio"),
      Mod(5, ModKind.ModDb, ""),
    ];

    [Fact]
    public void Conta_so_os_do_ModDB_instalados_agora_e_cada_autor_uma_vez()
    {
      var credits = ModDbCredits.From([All[0], All[1], All[3]], All);

      Assert.Equal(new ModDbCredits(2, 1, ThisRun: true), credits);
    }

    [Fact]
    public void Sem_nada_do_ModDB_nesta_rodada_conta_a_lista_inteira()
    {
      var credits = ModDbCredits.From([All[3]], All);

      Assert.Equal(new ModDbCredits(4, 2, ThisRun: false), credits);
    }

    [Fact]
    public void Lista_sem_ModDB_nao_tem_credito()
    {
      Assert.Null(ModDbCredits.From([], [All[3]]));
    }
  }
}
