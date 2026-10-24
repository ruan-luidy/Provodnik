namespace Provodnik.Core.Install
{
  /// <summary>
  /// O que foi instalado em cada pasta de mod: de qual arquivo e com qual impressão digital (o md5, ou tamanho e data
  /// quando não tem md5). É isso que deixa a próxima atualização pular os mods que não mudaram em vez de reextrair os
  /// 450 como o original.
  /// </summary>
  public sealed record InstalledMod(string Archive, string Fingerprint, DateTime InstalledAt)
  {
    /// <summary>
    /// O md5 do arquivo mais as instruções de pasta: o mesmo zip com instrução nova (outra opção do mod escolhida pelo
    /// GAMMA) também conta como mudança e é reextraído.
    /// </summary>
    public static string FingerprintOf(string md5, IReadOnlyList<string> instructions) =>
      instructions.Count == 0 ? $"{Recipe}|{md5}" : $"{Recipe}|{md5}|{string.Join(':', instructions)}";

    /// <summary>
    /// A versão do jeito de montar a pasta. Sobe quando o Проводник muda como aplica as instruções: o que foi montado
    /// do jeito antigo é refeito sozinho na próxima instalação (r2: instrução com barra no começo ou com "/").
    /// </summary>
    public const string Recipe = "r2";
  }

  public sealed record InstallState
  {
    /// <summary>Pasta do mod (o nome em mods\) → o que tem nela.</summary>
    public Dictionary<string, InstalledMod> Mods { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Arquivo em downloads\ → ETag do servidor (os zips do GitHub).</summary>
    public Dictionary<string, string> ETags { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>A última instalação que chegou até o fim sem falha.</summary>
    public DateTime? LastCompleted { get; init; }

    /// <summary>A última que começou: se for mais nova que a completa, ficou pela metade.</summary>
    public DateTime? LastStarted { get; init; }

    public string? DefinitionVersion { get; init; }

    public bool IsIncomplete => LastStarted is { } started && (LastCompleted is not { } done || done < started);
  }
}
