namespace Provodnik.Core.Install
{
  /// <summary>
  /// Como rodar uma instalação. Os padrões são os do launcher original (MD5 ligado, GitHub sempre atualizado,
  /// DLLs do Reshade apagadas), menos o "Force zip extraction": aqui só reextrai o que mudou, a não ser que peça.
  /// </summary>
  public sealed record InstallOptions
  {
    /// <summary>A chave da API do mods.txt (o campo de texto escondido do original; vazio pra todo mundo).</summary>
    public string ModListKey { get; init; } = string.Empty;

    public bool CheckMd5 { get; init; } = true;

    /// <summary>Pergunta pro GitHub se o zip mudou (ETag) em vez de confiar no que já está em downloads\.</summary>
    public bool UpdateGitHubAddons { get; init; } = true;

    /// <summary>Reextrai todo mod, mesmo os que não mudaram (o "Force zip extraction" do original).</summary>
    public bool ForceExtract { get; init; }

    public bool DeleteReshadeDlls { get; init; } = true;

    public bool CreateShortcuts { get; init; } = true;

    /// <summary>Downloads do ModDB ao mesmo tempo, no CDN (os pedidos de link vão um por vez, no ritmo de <see cref="ModDbSpacing"/>).</summary>
    public int ModDbParallel { get; init; } = 3;

    /// <summary>O respiro entre dois pedidos de link ao ModDB (a página que ele limita).</summary>
    public TimeSpan ModDbSpacing { get; init; } = TimeSpan.FromSeconds(1.5);

    /// <summary>A primeira espera quando o ModDB reclama (dobra a cada nova reclamação, até 5 min).</summary>
    public TimeSpan ModDbCooldown { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Downloads do GitHub e afins ao mesmo tempo.</summary>
    public int HttpParallel { get; init; } = 4;

    /// <summary>Extrações ao mesmo tempo (o gargalo é o disco).</summary>
    public int ExtractParallel { get; init; } = 2;

    /// <summary>Tentativas por arquivo antes de desistir dele.</summary>
    public int Attempts { get; init; } = 4;

    /// <summary>
    /// Só estes mods (pelo número da linha): o "tentar de novo os que falharam" e o reinstalar de um mod. Sem nada,
    /// o pacote inteiro.
    /// </summary>
    public IReadOnlySet<int>? OnlyMods { get; init; }

    /// <summary>Pula os repositórios, a definição e a lista (eles já foram atualizados nesta sessão).</summary>
    public bool SkipPreparation { get; init; }
  }
}
