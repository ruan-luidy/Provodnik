namespace Provodnik.Core.Diagnostics
{
  /// <summary>O que um erro quer dizer e o que fazer, em português de gente.</summary>
  public sealed record Explanation(string Meaning, string Hint);

  /// <summary>
  /// Traduz as mensagens que o git, o curl, o 7-Zip, o stalker-gamma-dl-cli e o Windows cospem ("RPC failed; curl 56",
  /// "Headers Error", "being used by another process"...) pra uma frase do que aconteceu e do que fazer. É o que faltava
  /// no console do launcher original: os erros apareciam, mas ninguém entendia.
  /// </summary>
  public static class ErrorExplainer
  {
    private sealed record Rule(string[] Patterns, Explanation Explanation);

    private static readonly Rule[] Rules =
    [
      new(["cloudflare", "challenge", "403 forbidden", "http 403", "error: 403"],
        new("O ModDB (Cloudflare) recusou o pedido.", "Espere uns minutos e tente de novo; se repetir, diminua os downloads do ModDB nas Configurações (1 ou 2).")),
      new(["429", "too many requests"],
        new("Pedidos demais seguidos pro servidor.", "Diminua os downloads simultâneos nas Configurações e tente de novo daqui a pouco.")),
      new(["could not resolve host", "couldn't resolve host", "não achou o servidor", "name or service not known", "no such host"],
        new("Sem resolver o nome do site (DNS).", "Confira a internet. Se o resto da internet funciona, troque o DNS pra 1.1.1.1 ou 8.8.8.8.")),
      new(["failed to connect", "connection refused", "não conectou", "couldn't connect", "unable to access"],
        new("Não conseguiu conectar no servidor.", "Confira a internet, VPN e firewall. O servidor também pode estar fora do ar: tente mais tarde.")),
      new(["timed out", "timeout", "parou de responder", "operation too slow", "não respondeu a tempo"],
        new("A conexão ficou parada tempo demais.", "Internet lenta ou instável. Ele já tenta de novo sozinho; se continuar, rode de novo mais tarde.")),
      new(["early eof", "rpc failed", "connection reset", "a conexão caiu", "recv failure", "unexpected disconnect", "curl 56", "curl 18", "veio cortado", "transfer closed"],
        new("A conexão caiu no meio do download.", "Normal em internet instável: ele continua de onde parou na próxima tentativa.")),
      new(["ssl", "tls", "certificate", "schannel"],
        new("Falhou a conexão segura (TLS) com o servidor.", "Confira a data e a hora do Windows e se algum antivírus está inspecionando HTTPS.")),
      new(["http 404", "404 not found", "error: 404"],
        new("O arquivo não existe mais no servidor.", "O link da lista do GAMMA quebrou. Espere a correção (costuma sair rápido) e atualize.")),
      new(["http 5", "returned error: 5", "bad gateway", "service unavailable"],
        new("O servidor está com problema.", "Não é com você: tente de novo daqui a alguns minutos.")),
      new(["md5 não bate", "md5 mismatch"],
        new("O arquivo baixado não é o que a lista do GAMMA espera.", "Ele baixa de novo sozinho. Se ficar igual duas vezes, o autor atualizou no ModDB e ele aceita com aviso.")),
      new(["headers error", "data error", "unexpected end", "can not open the file as archive", "cannot open the file as archive", "crc failed", "is not archive", "arquivo abriu vazio", "7-zip parou com código 2"],
        new("O arquivo está corrompido ou incompleto.", "Apague ele de downloads\\ (ou use Reinstalar na tela de Mods) que ele baixa de novo.")),
      new(["being used by another process", "sendo usado por outro processo", "the process cannot access the file"],
        new("Algum programa está segurando o arquivo.", "Feche o MO2, o jogo e janelas do Explorer abertas na pasta do GAMMA. Antivírus escaneando também prende: tente de novo.")),
      new(["access is denied", "acesso negado", "access to the path", "unauthorizedaccess", "permission denied", "operation not permitted"],
        new("Sem permissão pra escrever na pasta.", "Feche o MO2 e o jogo. Se o GAMMA estiver em Arquivos de Programas ou outra pasta protegida, reabra o Проводник como administrador (ou mova o GAMMA pra C:\\GAMMA).")),
      new(["not enough space", "espaço suficiente", "no space left", "disk full", "disco cheio"],
        new("O disco encheu.", "Libere espaço: uma instalação do zero precisa de uns 150 GB. Dá pra apagar arquivos velhos de downloads\\ depois de instalar.")),
      new(["filename too long", "path too long", "nome do arquivo muito longo", "pathtoolong"],
        new("Caminho de arquivo comprido demais pro Windows.", "Ative os caminhos longos (a checagem antes de instalar tem o botão) ou mova o GAMMA pra uma pasta curta como C:\\GAMMA.")),
      new(["não devolveu o link", "stalker-gamma-dl-cli"],
        new("O ModDB não entregou o link de download.", "O addon pode ter saído do ar ou o ModDB mudou a página. Tente de novo; se insistir, avise no Discord do GAMMA.")),
      new(["não encontrado em", "not found", "cannot find the file", "não pode encontrar o arquivo"],
        new("Falta um arquivo que deveria estar lá.", "O antivírus pode ter apagado uma ferramenta do GAMMA (curl, 7z, stalker-gamma-dl-cli). Restaure da quarentena ou baixe o instalador do GAMMA de novo.")),
      new(["virus", "potentially unwanted", "operation did not complete successfully because the file contains"],
        new("O antivírus bloqueou um arquivo.", "Adicione a pasta do GAMMA nas exclusões do antivírus e tente de novo.")),
      new(["could not lock", "index.lock", "unable to create", "fatal: not a git repository"],
        new("O repositório git ficou travado ou estragado.", "Rode de novo: se continuar, apague a pasta do repositório em resources\\ que ele clona do zero.")),
    ];

    public static Explanation? Explain(string? text)
    {
      if (string.IsNullOrWhiteSpace(text))
        return null;
      var lower = text.ToLowerInvariant();
      return Rules.FirstOrDefault(r => r.Patterns.Any(lower.Contains))?.Explanation;
    }
  }
}
