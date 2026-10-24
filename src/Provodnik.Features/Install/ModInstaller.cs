using System.Text;
using Provodnik.Core.Archives;
using Provodnik.Core.IO;
using Provodnik.Core.Layout;
using Provodnik.Core.Modpack;

namespace Provodnik.Features.Install
{
  /// <summary>
  /// Coloca um mod no mods\ sem nunca deixar a pasta pela metade: extrai numa pasta de trabalho, aplica as
  /// instruções, escreve o meta.ini e só então troca com a pasta antiga (dois renames no mesmo disco). Arquivo que não
  /// abre deixa a versão anterior intacta. O original apagava a pasta, extraía em cima e escrevia o meta.ini mesmo
  /// quando a extração falhava: o MO2 mostrava o mod instalado e vazio.
  /// </summary>
  public sealed class ModInstaller(IArchiveExtractor extractor)
  {
    private static readonly UTF8Encoding NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public async Task InstallAsync(GammaLayout layout, ModEntry mod, string archive, string metaTemplate, IProgress<double>? progress, Action<string> warn, CancellationToken cancellationToken)
    {
      var staging = Path.Combine(layout.WorkDir, $"{mod.Index}-{Guid.NewGuid():N}"[..12]);
      try
      {
        await extractor.ExtractAsync(archive, staging, progress, cancellationToken).ConfigureAwait(false);
        if (!Directory.EnumerateFileSystemEntries(staging).Any())
          throw new ArchiveException("o arquivo abriu vazio");

        if (mod.Instructions.Count > 0)
          InstructionApplier.Apply(staging, mod.Instructions, warn);

        File.WriteAllText(Path.Combine(staging, "meta.ini"), MetaIni.Build(metaTemplate, mod.ArchiveName ?? string.Empty, mod.PageUrl), NoBom);
        cancellationToken.ThrowIfCancellationRequested();
        Swap(layout, staging, Path.Combine(layout.Mods, mod.FolderName));
      }
      finally
      {
        TryDelete(staging);
      }
    }

    /// <summary>A pasta do separador com o meta.ini dele (resources\separator\meta.ini), se ainda não existir.</summary>
    public static void CreateSeparator(GammaLayout layout, ModEntry separator, string template)
    {
      var folder = Path.Combine(layout.Mods, separator.FolderName);
      Directory.CreateDirectory(folder);
      var meta = Path.Combine(folder, "meta.ini");
      if (!File.Exists(meta))
        File.WriteAllText(meta, template, NoBom);
    }

    private static void Swap(GammaLayout layout, string staging, string target)
    {
      Directory.CreateDirectory(layout.Mods);
      if (!Directory.Exists(target))
      {
        Move(staging, target);
        return;
      }

      var old = Path.Combine(layout.WorkDir, $"old-{Guid.NewGuid():N}");
      Move(target, old);
      try
      {
        Move(staging, target);
      }
      catch
      {
        // não entrou: a versão anterior volta pro lugar
        Move(old, target);
        throw;
      }
      TryDelete(old);
    }

    private static void Move(string source, string destination)
    {
      if (string.Equals(Path.GetPathRoot(source), Path.GetPathRoot(destination), StringComparison.OrdinalIgnoreCase))
      {
        Directory.Move(source, destination);
        return;
      }
      // mods\ em outro disco (junção): copia e apaga
      Directory.CreateDirectory(destination);
      FileTree.MoveContents(source, destination);
    }

    private static void TryDelete(string path)
    {
      try
      {
        FileTree.Delete(path);
      }
      catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
      {
        // sobra na pasta de trabalho, que é limpa no começo da próxima instalação
      }
    }
  }
}
