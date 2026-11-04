using Provodnik.Features.Download;

namespace Provodnik.Tests
{
  /// <summary>A vaga única do ModDB: nunca dois lá dentro, e quem sai libera pro próximo.</summary>
  public sealed class ModDbGateTests
  {
    [Fact]
    public async Task Dois_ao_mesmo_tempo_nunca_ficam_dentro_juntos()
    {
      var gate = new ModDbGate();
      var inside = 0;
      var most = 0;

      async Task Use()
      {
        for (var i = 0; i < 20; i++)
        {
          using var slot = await gate.EnterAsync(CancellationToken.None);
          var now = Interlocked.Increment(ref inside);
          InterlockedMax(ref most, now);
          await Task.Delay(1);
          Interlocked.Decrement(ref inside);
        }
      }

      await Task.WhenAll(Task.Run(Use), Task.Run(Use), Task.Run(Use));

      Assert.Equal(1, most);
    }

    [Fact]
    public async Task Sair_duas_vezes_nao_abre_vaga_a_mais()
    {
      var gate = new ModDbGate();
      var first = await gate.EnterAsync(CancellationToken.None);
      first.Dispose();
      first.Dispose();

      using var second = await gate.EnterAsync(CancellationToken.None);
      var third = gate.EnterAsync(CancellationToken.None);
      await Task.Delay(50);

      Assert.False(third.IsCompleted);
    }

    private static void InterlockedMax(ref int target, int value)
    {
      int current;
      while ((current = Volatile.Read(ref target)) < value && Interlocked.CompareExchange(ref target, value, current) != current)
      {
      }
    }
  }
}
