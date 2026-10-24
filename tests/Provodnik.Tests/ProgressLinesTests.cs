using Provodnik.Core.Text;

namespace Provodnik.Tests
{
  public class ProgressLinesTests
  {
    [Fact]
    public void Medidor_do_curl_da_recebido_total_e_velocidade()
    {
      Assert.True(ProgressLines.TryParseCurl(" 45  123M   45 55.6M    0     0  10.2M      0  0:00:12  0:00:05  0:00:07 10.5M", out var received, out var total, out var speed));

      Assert.Equal(123L * 1024 * 1024, total);
      Assert.Equal((long)(55.6 * 1024 * 1024), received);
      Assert.Equal(10.5 * 1024 * 1024, speed);
    }

    [Fact]
    public void Cabecalho_do_curl_nao_e_progresso()
    {
      Assert.False(ProgressLines.TryParseCurl("  % Total    % Received % Xferd  Average Speed   Time    Time     Time  Current", out _, out _, out _));
    }

    [Fact]
    public void Porcentagem_do_7zip()
    {
      Assert.True(ProgressLines.TryParseSevenZip(" 37% 12 - gamedata\\configs\\system.ltx", out var fraction));
      Assert.Equal(0.37, fraction, 3);
    }

    [Fact]
    public void Fase_e_porcentagem_do_git()
    {
      Assert.True(ProgressLines.TryParseGit("Receiving objects:  45% (1234/2742), 1.20 MiB | 2.40 MiB/s", out var phase, out var fraction));
      Assert.Equal("Receiving objects", phase);
      Assert.Equal(0.45, fraction, 3);
      Assert.True(ProgressLines.TryParseGit("remote: Counting objects: 100% (12/12), done.", out phase, out _));
      Assert.Equal("Counting objects", phase);
    }
  }
}
