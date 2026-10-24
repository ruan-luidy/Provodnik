using Provodnik.Core.IO;
using Provodnik.Core.Layout;
using Provodnik.Core.Modpack;
using Provodnik.Core.Text;

namespace Provodnik.Features.ModOrganizer
{
  /// <summary>
  /// O que o GAMMA muda direto na pasta do Anomaly (fora do MO2): o modpack_patches (appdata, bin, db, gamedata com
  /// os exes modificados e o user.ltx), as DLLs do Reshade apagadas (opcional) e o cache de shaders limpo.
  /// </summary>
  public static class AnomalyPatcher
  {
    public static string FindAnomaly(GammaLayout layout)
    {
      var anomaly = Mo2Config.Load(layout.ModOrganizerIni).GamePath;
      if (anomaly is null || !Directory.Exists(Path.Combine(anomaly, "bin")))
        throw new DirectoryNotFoundException(L.T("features.patches.noAnomaly"));
      return anomaly;
    }

    public static int Apply(GammaLayout layout, ModpackMetadata metadata, string anomaly, bool deleteReshade)
    {
      var copied = FileTree.CopyContents(Path.Combine(layout.Definition(metadata.FolderName), "modpack_patches"), anomaly);

      if (deleteReshade)
      {
        foreach (var dll in new[] { "dxgi.dll", "d3d9.dll" })
        {
          var path = Path.Combine(anomaly, "bin", dll);
          if (File.Exists(path))
            File.Delete(path);
        }
      }

      FileTree.Delete(Path.Combine(anomaly, "appdata", "shaders_cache"));
      return copied;
    }
  }
}
