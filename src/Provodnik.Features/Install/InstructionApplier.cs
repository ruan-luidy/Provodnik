using Provodnik.Core.IO;

namespace Provodnik.Features.Install
{
  /// <summary>
  /// As instruções da 2ª coluna do mods.txt ("pasta1:pasta2\sub"), aplicadas como o launcher original faz, passo a
  /// passo, pra pasta do mod sair igual à do instalador oficial:
  ///   1. pra cada caminho: se ele tem um gamedata dentro, o conteúdo dele sobe pra raiz do mod;
  ///      senão o conteúdo dele vai pra dentro de gamedata\ (criada mesmo que o caminho não exista);
  ///   2. no fim, toda pasta da raiz que não for gamedata, appdata, db ou fomod é apagada.
  /// O "mover" é o robocopy /s /move: a origem some depois. Por isso um caminho dentro de outro já movido não
  /// existe mais quando chega a vez dele; o original também é assim, e aqui vira aviso no log em vez de silêncio.
  /// </summary>
  public static class InstructionApplier
  {
    private static readonly string[] Kept = ["gamedata", "appdata", "db", "fomod"];

    /// <summary>
    /// Aplica as instruções. Devolve true quando alguma pasta pedida não existia. Com <paramref name="rescue"/>, pasta que
    /// sumiu é procurada pelo nome parecido (<see cref="FindRenamed"/>); o instalador só liga isso na segunda passada,
    /// quando o jeito do original deixou o mod vazio.
    /// </summary>
    public static bool Apply(string root, IReadOnlyList<string> instructions, Action<string> warn, bool rescue = false)
    {
      var missing = false;
      foreach (var raw in instructions)
      {
        // o original junta como texto ("$Extract_path\$i"): "\Black_Market-main" e "pasta/sub" valem igual a "pasta\sub"
        var instruction = Normalize(raw);
        if (instruction.Length == 0)
          continue;
        var source = Path.Combine(root, instruction);
        if (Directory.Exists(Path.Combine(source, "gamedata")))
        {
          FileTree.MoveContents(source, root);
          continue;
        }

        if (!Directory.Exists(source) && rescue && FindRenamed(root, instruction) is { } renamed)
        {
          // o autor renomeou a pasta e a lista do GAMMA não acompanhou ("Spawn Options\No Bloodsuckers" virou
          // "Spawn - No Bloodsuckers"): o original deixava o mod vazio; aqui usa a única pasta que termina igual
          warn($"a pasta \"{instruction}\" não existe no arquivo; usando \"{Path.GetRelativePath(root, renamed)}\", que é a mesma opção com outro nome");
          FileTree.MoveContents(renamed, root);
          continue;
        }

        var gamedata = Path.Combine(root, "gamedata");
        Directory.CreateDirectory(gamedata);
        if (!Directory.Exists(source))
        {
          missing = true;
          if (!rescue)
            warn($"a pasta \"{instruction}\" não existe no arquivo");
          continue;
        }
        if (!FileTree.MoveContents(source, gamedata))
          warn($"a pasta \"{instruction}\" não pode ir pra dentro dela mesma");
      }

      foreach (var folder in Directory.EnumerateDirectories(root))
      {
        if (!Kept.Contains(Path.GetFileName(folder), StringComparer.OrdinalIgnoreCase))
          FileTree.Delete(folder);
      }
      return missing;
    }

    internal static string Normalize(string instruction) =>
      instruction.Replace('/', '\\').Trim().Trim('\\');

    /// <summary>
    /// A pasta que o autor renomeou: a única (até 3 níveis), com gamedata dentro, cujo nome termina igual ao fim da
    /// instrução mas não é igual a ele ("Spawn - No Bloodsuckers" pra "No Bloodsuckers"). Nome igual fica de fora de
    /// propósito: é a pasta que o original já tinha movido antes (Ammo Maker, MagsRedux) e o resultado tem que sair igual.
    /// </summary>
    internal static string? FindRenamed(string root, string instruction)
    {
      var key = Squash(Path.GetFileName(instruction));
      if (key.Length < 4)
        return null;

      var candidates = Folders(root, depth: 3)
        .Where(d => Squash(Path.GetFileName(d)) is var name
          && ((name != key && name.EndsWith(key, StringComparison.Ordinal))
            // o mesmo nome só vale embrulhado numa pasta nova ("Mod_0.4.3-c\pasta"); na raiz é a que o original já moveu
            || (name == key && !string.Equals(Path.GetDirectoryName(d), Path.GetFullPath(root).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))))
        .Where(d => Directory.Exists(Path.Combine(d, "gamedata")))
        .Take(2)
        .ToList();
      return candidates.Count == 1 ? candidates[0] : null;
    }

    private static IEnumerable<string> Folders(string root, int depth)
    {
      if (depth == 0)
        yield break;
      foreach (var folder in Directory.EnumerateDirectories(root))
      {
        if (Path.GetFileName(folder).Equals("gamedata", StringComparison.OrdinalIgnoreCase))
          continue;
        yield return folder;
        foreach (var inner in Folders(folder, depth - 1))
          yield return inner;
      }
    }

    /// <summary>Só letras e números, minúsculas: "Timer - 10 minutes" e "10 minutes" comparam pelo fim.</summary>
    private static string Squash(string name) =>
      new(name.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
  }
}
