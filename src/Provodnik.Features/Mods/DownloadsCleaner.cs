using Provodnik.Core.Layout;
using Provodnik.Core.Modpack;

namespace Provodnik.Features.Mods
{
  /// <summary>Um arquivo em downloads\ que nada mais usa.</summary>
  public sealed record OldDownload(string Path, long Size)
  {
    public string Name => System.IO.Path.GetFileName(Path);
  }

  /// <summary>O que a limpeza conseguiu apagar.</summary>
  public sealed record CleanupResult(int Deleted, long Freed, IReadOnlyList<string> Failed);

  /// <summary>
  /// O espaço que as versões velhas ocupam em downloads\: a cada atualização do GAMMA o arquivo novo de um mod chega e o
  /// velho fica (o launcher original também nunca apaga). Sai o arquivo que nada mais usa: nem a lista atual do GAMMA,
  /// nem o meta.ini de alguma pasta em mods\ (mod da pessoa ou sobra do GAMMA, que ainda pode ser reinstalado dele). O
  /// que tem um .meta do lado foi baixado pelo MO2, pela pessoa, e nunca sai; o .part de um arquivo sem uso
  /// (download parado de um mod que saiu da lista) sai junto.
  /// </summary>
  public static class DownloadsCleaner
  {
    private static readonly string[] Archives = [".zip", ".7z", ".rar"];

    public static IReadOnlyList<OldDownload> Find(GammaLayout layout, ModList list)
    {
      if (!Directory.Exists(layout.Downloads))
        return [];

      var used = list.Mods.Select(m => m.ArchiveName).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
      if (Directory.Exists(layout.Mods))
      {
        foreach (var folder in Directory.EnumerateDirectories(layout.Mods))
        {
          var (file, _) = MetaIni.ReadInstall(folder);
          if (!string.IsNullOrEmpty(file))
            used.Add(Path.GetFileName(file.Replace('/', Path.DirectorySeparatorChar)));
        }
      }

      var old = new List<OldDownload>();
      foreach (var file in new DirectoryInfo(layout.Downloads).EnumerateFiles())
      {
        var archive = file.Name.EndsWith(".part", StringComparison.OrdinalIgnoreCase) ? file.Name[..^".part".Length] : file.Name;
        if (!Archives.Contains(Path.GetExtension(archive), StringComparer.OrdinalIgnoreCase) || used.Contains(archive))
          continue;
        if (File.Exists(Path.Combine(layout.Downloads, archive + ".meta")))
          continue;
        old.Add(new OldDownload(file.FullName, file.Length));
      }
      return old.OrderBy(o => o.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static CleanupResult Delete(IEnumerable<OldDownload> files)
    {
      var deleted = 0;
      var freed = 0L;
      var failed = new List<string>();
      foreach (var file in files)
      {
        try
        {
          File.Delete(file.Path);
          deleted++;
          freed += file.Size;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
          failed.Add($"{file.Name}: {ex.Message}");
        }
      }
      return new CleanupResult(deleted, freed, failed);
    }
  }
}
