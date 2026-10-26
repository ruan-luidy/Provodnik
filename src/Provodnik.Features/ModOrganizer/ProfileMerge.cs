namespace Provodnik.Features.ModOrganizer
{
  /// <summary>De quem é uma pasta em mods\ que não está na lista nova do GAMMA.</summary>
  public enum ModOwner
  {
    /// <summary>Não tem pasta: a linha não volta.</summary>
    None,

    /// <summary>Da pessoa: instalada pelo MO2 ou copiada à mão.</summary>
    User,

    /// <summary>Addon opcional do GAMMA (vem no modpack_addons e fica fora da lista até a pessoa ligar).</summary>
    GammaOptional,

    /// <summary>Do GAMMA: o que os instaladores puseram, mod que saiu da lista, numeração velha.</summary>
    Gamma,
  }

  /// <summary>O modlist.txt mesclado e o que ficou da pessoa nele.</summary>
  /// <param name="KeptMods">Os mods da pessoa (e os opcionais do GAMMA que ela ligou) que continuam no perfil, sem os separadores.</param>
  /// <param name="KeptChoices">Mods da lista do GAMMA que ficam ligados ou desligados como a pessoa deixou.</param>
  /// <param name="Separator">A pasta do separador "Meus mods" quando ele está na lista (pode ser preciso criar).</param>
  public sealed record ProfileMergeResult(string Text, IReadOnlyList<string> KeptMods, IReadOnlyList<string> KeptChoices, string? Separator);

  /// <summary>
  /// O perfil do GAMMA atualizado sem jogar fora o que a pessoa fez nele. O original (e o Проводник até a 0.3.2) trocava
  /// o modlist.txt inteiro pelo da definição: o MO2 punha de volta, desligados e misturados com as sobras do GAMMA, os
  /// mods que a pessoa tinha instalado, e mod do GAMMA que ela tinha desligado voltava ligado. Aqui, com três listas (a do
  /// GAMMA da última instalação, o perfil como está e a do GAMMA nova):
  ///   - os mods da lista do GAMMA e a ordem deles são os da lista nova;
  ///   - mod da lista que a pessoa ligou ou desligou fica como ela deixou, a não ser que o GAMMA também tenha mudado ele;
  ///   - os mods da pessoa, e os opcionais do GAMMA que ela ligou, vão juntos, na ordem e no estado em que estavam, pro
  ///     fim da lista do MO2 (a prioridade mais alta), embaixo do separador "Meus mods";
  ///   - as sobras do GAMMA (pasta dele fora da lista) vão desligadas pro começo da lista do MO2. Sem linha, o MO2 poria
  ///     elas no fim, no meio dos mods da pessoa.
  /// O modlist.txt é de trás pra frente: a primeira linha é a de prioridade mais alta (a última no MO2), e um separador
  /// vem depois dos mods dele.
  /// </summary>
  public static class ProfileMerge
  {
    /// <param name="gamma">A lista nova do GAMMA, já com os nomes corrigidos.</param>
    /// <param name="previousGamma">A lista do GAMMA da última instalação (nula: sem como saber o que a pessoa mudou nos da lista).</param>
    /// <param name="current">O modlist.txt do perfil como está (nulo: perfil novo).</param>
    /// <param name="ownerOf">De quem é uma pasta fora da lista nova.</param>
    /// <param name="gammaFolders">As pastas do GAMMA em mods\ (as sobras que o perfil ainda não tem também ganham linha).</param>
    /// <param name="separator">A pasta do separador "Meus mods" se for criado agora.</param>
    /// <param name="knownSeparators">As pastas que já contam como o "Meus mods" (o nome em todos os idiomas).</param>
    public static ProfileMergeResult Merge(
      string gamma,
      string? previousGamma,
      string? current,
      Func<string, ModOwner> ownerOf,
      IReadOnlyCollection<string> gammaFolders,
      string separator,
      IReadOnlyCollection<string> knownSeparators)
    {
      var newline = gamma.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
      var gammaLines = gamma.Replace("\r\n", "\n").Split('\n');
      var gammaNames = Entries(gammaLines).Select(e => e.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
      var previous = previousGamma is null ? null : States(Entries(previousGamma.Replace("\r\n", "\n").Split('\n')));
      var mine = current is null ? [] : Entries(current.Replace("\r\n", "\n").Split('\n')).Where(e => e.Prefix is '+' or '-').ToList();
      var now = States(mine);
      var ours = knownSeparators.ToHashSet(StringComparer.OrdinalIgnoreCase);

      // o que está fora da lista nova, na ordem do perfil: da pessoa, ou sobra do GAMMA
      var seen = new HashSet<string>(gammaNames, StringComparer.OrdinalIgnoreCase);
      string? existing = null;
      var user = new List<Entry>();
      var leftovers = new List<string>();
      foreach (var entry in mine)
      {
        if (!seen.Add(entry.Name))
          continue;
        if (ours.Contains(entry.Name))
        {
          existing ??= entry.Name;
          continue;
        }
        var owner = ownerOf(entry.Name);
        // estava na lista do GAMMA e saiu: é dele, mesmo que a pasta já não venha mais no modpack_addons
        if (owner != ModOwner.None && previous?.ContainsKey(entry.Name) == true)
          owner = ModOwner.Gamma;
        switch (owner)
        {
          case ModOwner.User:
          // o opcional que a pessoa ligou é escolha dela; o que estava na lista do GAMMA e saiu, não
          case ModOwner.GammaOptional when entry.Prefix == '+':
            user.Add(entry);
            break;
          case ModOwner.GammaOptional:
          case ModOwner.Gamma:
            leftovers.Add(entry.Name);
            break;
        }
      }
      leftovers.AddRange(gammaFolders.Where(seen.Add).Order(StringComparer.OrdinalIgnoreCase));

      var block = user.Select(e => e.Line).ToList();
      // separador da própria pessoa no fim do bloco já agrupa tudo; senão os mods dela ficariam no último separador do GAMMA
      var withSeparator = existing is not null || (user.Count > 0 && !user[^1].IsSeparator);
      var separatorName = existing ?? separator;
      if (withSeparator)
        block.Add("+" + separatorName);

      var choices = new List<string>();
      var output = new List<string>(gammaLines.Length + block.Count + leftovers.Count);
      var placed = false;
      foreach (var line in gammaLines)
      {
        if (Parse(line) is not { } entry)
        {
          output.Add(line);
          continue;
        }
        if (!placed)
        {
          output.AddRange(block);
          placed = true;
        }
        // a pessoa mudou e o GAMMA não: fica o dela. Separador não tem estado que importe
        if (entry.Prefix is '+' or '-' && !entry.IsSeparator && previous is not null
            && now.TryGetValue(entry.Name, out var hers) && previous.TryGetValue(entry.Name, out var before)
            && hers != before && before == (entry.Prefix == '+'))
        {
          output.Add((hers ? '+' : '-') + line[1..]);
          choices.Add(entry.Name);
        }
        else
        {
          output.Add(line);
        }
      }

      // o bloco da pessoa antes da primeira linha de mod (sem nenhuma, antes da linha vazia do fim), as sobras no fim
      var end = output.Count > 0 && output[^1].Length == 0 ? output.Count - 1 : output.Count;
      if (!placed)
      {
        output.InsertRange(end, block);
        end += block.Count;
      }
      output.InsertRange(end, leftovers.Select(name => "-" + name));

      return new ProfileMergeResult(
        string.Join(newline, output),
        user.Where(e => !e.IsSeparator).Select(e => e.Name).ToList(),
        choices,
        withSeparator ? separatorName : null);
    }

    private sealed record Entry(char Prefix, string Name, string Line)
    {
      public bool IsSeparator => Name.EndsWith("_separator", StringComparison.OrdinalIgnoreCase);
    }

    // o modlist do GAMMA tem nome com TAB no fim (#425), igual ao Mo2ModList: compara sem as pontas
    private static Entry? Parse(string line) =>
      line.Length > 1 && line[0] is '+' or '-' or '*' ? new Entry(line[0], line[1..].Trim(), line) : null;

    private static IEnumerable<Entry> Entries(IEnumerable<string> lines) => lines.Select(Parse).OfType<Entry>();

    private static Dictionary<string, bool> States(IEnumerable<Entry> entries)
    {
      var states = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
      foreach (var entry in entries)
        states.TryAdd(entry.Name, entry.Prefix is '+' or '*');
      return states;
    }
  }
}
