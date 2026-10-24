using Provodnik.Core.Text;

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
    // as mensagens do próprio Проводник entram nos três idiomas (core.errors.*: o texto; os padrões: o pedaço da frase)
    private sealed record Rule(string[] Patterns, string Key);

    private static readonly Rule[] Rules =
    [
      new(["temporarily blocked", "bloqueou temporariamente", "временно заблокировал"], "blocked"),
      new(["failed to generate download link"], "dlcli"),
      new(["cloudflare", "challenge", "403 forbidden", "http 403", "error: 403"], "cloudflare"),
      new(["429", "too many requests"], "tooMany"),
      new(["could not resolve host", "couldn't resolve host", "não achou o servidor", "couldn't find the server", "сервер не найден", "name or service not known", "no such host"], "dns"),
      new(["failed to connect", "connection refused", "não conectou", "couldn't connect", "не удалось подключиться", "unable to access"], "connect"),
      new(["timed out", "timeout", "parou de responder", "stopped responding", "перестал отвечать", "operation too slow", "não respondeu a tempo", "didn't respond in time", "не ответил вовремя"], "timeout"),
      new(["early eof", "rpc failed", "connection reset", "a conexão caiu", "connection dropped", "соединение оборвалось", "recv failure", "unexpected disconnect", "curl 56", "curl 18", "veio cortado", "cut short", "загрузка оборвалась", "transfer closed"], "dropped"),
      new(["ssl", "tls", "certificate", "schannel"], "tls"),
      new(["http 404", "404 not found", "error: 404"], "notFound"),
      new(["http 5", "returned error: 5", "bad gateway", "service unavailable"], "server"),
      new(["md5 não bate", "md5 mismatch", "md5 не совпадает"], "md5"),
      new(["headers error", "data error", "unexpected end", "can not open the file as archive", "cannot open the file as archive", "crc failed", "is not archive", "arquivo abriu vazio", "archive opened empty", "архив открылся пустым", "7-zip parou com código 2", "7-zip stopped with code 2", "7-zip завершился с кодом 2"], "corrupt"),
      new(["being used by another process", "sendo usado por outro processo", "занят другим процессом", "the process cannot access the file"], "locked"),
      new(["access is denied", "acesso negado", "отказано в доступе", "access to the path", "unauthorizedaccess", "permission denied", "operation not permitted"], "denied"),
      new(["not enough space", "espaço suficiente", "недостаточно места", "no space left", "disk full", "disco cheio"], "disk"),
      new(["filename too long", "path too long", "nome do arquivo muito longo", "pathtoolong"], "longPath"),
      new(["não devolveu o link", "didn't return the download link", "не вернул ссылку", "stalker-gamma-dl-cli"], "noLink"),
      new(["não encontrado em", "не найден в", "not found", "cannot find the file", "não pode encontrar o arquivo"], "missingFile"),
      new(["virus", "potentially unwanted", "operation did not complete successfully because the file contains"], "antivirus"),
      new(["could not lock", "index.lock", "unable to create", "fatal: not a git repository"], "git"),
    ];

    public static Explanation? Explain(string? text)
    {
      if (string.IsNullOrWhiteSpace(text))
        return null;
      var lower = text.ToLowerInvariant();
      return Rules.FirstOrDefault(r => r.Patterns.Any(lower.Contains)) is { } rule
        ? new Explanation(L.T($"core.errors.{rule.Key}.meaning"), L.T($"core.errors.{rule.Key}.hint"))
        : null;
    }
  }
}
