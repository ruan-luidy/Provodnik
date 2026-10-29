using Provodnik.Core.Downloads;
using Provodnik.Features.Download;

namespace Provodnik.Tests
{
  public sealed class SlowMirrorTests
  {
    private const long MB = 1024 * 1024;

    private long _now;

    private SlowMirror Watch() => new(() => _now);

    private bool At(SlowMirror watch, double seconds, long received, long? total = 500 * MB)
    {
      _now = (long)(seconds * 1000);
      return watch.IsSlow(new TransferProgress(received, total, null));
    }

    [Fact]
    public void Espelho_lento_com_muito_por_vir_troca_uma_vez()
    {
      var watch = Watch();
      // o .part já tinha 100 MB: a medida conta só o que veio agora
      Assert.False(At(watch, 0, 100 * MB));
      Assert.False(At(watch, 20, 102 * MB));
      Assert.True(At(watch, 30, 103 * MB));
      Assert.False(At(watch, 40, 104 * MB));
    }

    [Fact]
    public void Espelho_rapido_fica()
    {
      var watch = Watch();
      Assert.False(At(watch, 0, 0));
      Assert.False(At(watch, 30, 60 * MB));
      Assert.False(At(watch, 300, 60 * MB));
    }

    [Fact]
    public void Lento_mas_quase_no_fim_fica()
    {
      var watch = Watch();
      Assert.False(At(watch, 0, 0, 30 * MB));
      Assert.False(At(watch, 30, 3 * MB, 30 * MB));
    }

    [Fact]
    public void Sem_tamanho_nao_troca()
    {
      var watch = Watch();
      Assert.False(At(watch, 0, 0, null));
      Assert.False(At(watch, 30, 1 * MB, null));
    }
  }
}
