using Provodnik.Core.Diagnostics;

namespace Provodnik.Tests
{
  public class ErrorExplainerTests
  {
    [Theory]
    [InlineData("error: RPC failed; curl 56 Recv failure: Connection was reset", "A conexão caiu")]
    [InlineData("fatal: early EOF", "A conexão caiu")]
    [InlineData("ERROR: Headers Error : D:\\GAMMA\\downloads\\x.7z", "corrompido")]
    [InlineData("ERROR: Can not open the file as archive", "corrompido")]
    [InlineData("curl: (22) The requested URL returned error: 403", "Cloudflare")]
    [InlineData("curl: (6) Could not resolve host: cdn.dbolical.com", "DNS")]
    [InlineData("The process cannot access the file 'x.zip' because it is being used by another process.", "segurando o arquivo")]
    [InlineData("Access to the path 'C:\\Program Files\\GAMMA\\mods' is denied.", "permissão")]
    [InlineData("There is not enough space on the disk.", "disco encheu")]
    [InlineData("error: unable to create file gamedata/x.ltx: Filename too long", "comprido demais")]
    [InlineData("download: curl 22: o servidor respondeu com erro HTTP. The requested URL returned error: 503", "servidor está com problema")]
    [InlineData("download: md5 não bate (veio 1a2b3c4d…, a lista diz 9f8e7d6c…)", "não é o que a lista")]
    public void Erro_conhecido_vira_explicacao(string error, string expected)
    {
      var explanation = ErrorExplainer.Explain(error);

      Assert.NotNull(explanation);
      Assert.Contains(expected, explanation.Meaning, StringComparison.OrdinalIgnoreCase);
      Assert.False(string.IsNullOrWhiteSpace(explanation.Hint));
    }

    [Theory]
    [InlineData("Receiving objects: 45% (1234/2742)")]
    [InlineData("baixando https://www.moddb.com/addons/start/222467")]
    [InlineData("")]
    public void Linha_normal_nao_e_erro(string line)
    {
      Assert.Null(ErrorExplainer.Explain(line));
    }
  }
}
