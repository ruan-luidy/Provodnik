namespace Provodnik.Features.ModOrganizer
{
  /// <summary>
  /// Conserta nome digitado errado no modlist.txt do GAMMA. A lista liga "DarkasleQif's and iTheon's Parts Condition
  /// Color Thresholds Changes", mas a pasta que o próprio GAMMA manda é "Darkasleif's...": o MO2 não acha a pasta, joga a
  /// linha fora e põe a pasta de verdade desligada no topo da lista - o mod fica fora do jogo pra todo mundo. Aqui o
  /// nome errado vira o da pasta, na mesma posição e com o mesmo +/-.
  /// </summary>
  public static class ModListNames
  {
    /// <summary>A pasta de um nome do modlist: ele mesmo, ou a única pasta a um erro de digitação dele. Nulo se nenhuma.</summary>
    public static string? Resolve(string listed, IReadOnlyCollection<string> folders)
    {
      if (folders.Contains(listed, StringComparer.OrdinalIgnoreCase))
        return listed;
      var near = folders.Where(f => IsTypo(listed, f)).Take(2).ToList();
      return near.Count == 1 ? near[0] : null;
    }

    /// <summary>
    /// O texto do modlist com os nomes errados trocados pelo da pasta. Mantém a ordem, o prefixo, a quebra de linha e
    /// tudo o que não for nome errado; não troca pra uma pasta que já tem linha própria na lista.
    /// </summary>
    public static string Repair(string text, IReadOnlyCollection<string> folders, ICollection<(string From, string To)> fixes)
    {
      var lines = text.Split('\n');
      var listed = lines.Where(IsEntry).Select(NameOf).ToHashSet(StringComparer.OrdinalIgnoreCase);
      for (var i = 0; i < lines.Length; i++)
      {
        if (!IsEntry(lines[i]))
          continue;
        var name = NameOf(lines[i]);
        if (folders.Contains(name, StringComparer.OrdinalIgnoreCase) || Resolve(name, folders) is not { } folder || listed.Contains(folder))
          continue;
        var ending = lines[i].EndsWith('\r') ? "\r" : string.Empty;
        lines[i] = lines[i][0] + folder + ending;
        fixes.Add((name, folder));
      }
      return string.Join('\n', lines);
    }

    /// <summary>
    /// Nomes compridos que diferem por até 2 letras ("DarkasleQif's" x "Darkasleif's"): erro de digitação, não outro mod.
    /// Os numerados ("312- ...") a instalação cria com o nome da lista, então nunca são.
    /// </summary>
    public static bool IsTypo(string listed, string folder)
    {
      if (listed.Length < 12 || char.IsAsciiDigit(listed[0]) || Math.Abs(listed.Length - folder.Length) > 2 || listed.Equals(folder, StringComparison.OrdinalIgnoreCase))
        return false;
      var a = listed.ToUpperInvariant();
      var b = folder.ToUpperInvariant();
      var previous = Enumerable.Range(0, b.Length + 1).ToArray();
      for (var i = 1; i <= a.Length; i++)
      {
        var current = new int[b.Length + 1];
        current[0] = i;
        for (var j = 1; j <= b.Length; j++)
          current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
        previous = current;
      }
      return previous[b.Length] <= 2;
    }

    private static bool IsEntry(string line) => line.Length > 1 && line[0] is '+' or '-' or '*';

    // o modlist do GAMMA tem nome com TAB no fim (#425), igual ao Mo2ModList: compara sem as pontas
    private static string NameOf(string line) => line[1..].Trim();
  }
}
