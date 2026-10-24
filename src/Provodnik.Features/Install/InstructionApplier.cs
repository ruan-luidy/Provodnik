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

    public static void Apply(string root, IReadOnlyList<string> instructions, Action<string> warn)
    {
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

        var gamedata = Path.Combine(root, "gamedata");
        Directory.CreateDirectory(gamedata);
        if (!Directory.Exists(source))
        {
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
    }

    internal static string Normalize(string instruction) =>
      instruction.Replace('/', '\\').Trim().Trim('\\');
  }
}
