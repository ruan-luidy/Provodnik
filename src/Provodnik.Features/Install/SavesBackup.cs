using System.IO.Compression;
using Provodnik.Core.Layout;
using Provodnik.Core.Modpack;

namespace Provodnik.Features.Install
{
  /// <summary>
  /// Os saves num zip antes de instalar ou atualizar (.provodnik\save-backups, os 5 últimos): save de uma versão do
  /// GAMMA nem sempre abre na seguinte, e é o que mais dói perder. Os saves ficam no appdata\savedgames do Anomaly, ou
  /// no saves\ do perfil quando ele usa saves próprios (LocalSaves=true no settings.ini do MO2).
  /// </summary>
  public static class SavesBackup
  {
    private const int Kept = 5;

    public static string Dir(GammaLayout layout) => Path.Combine(layout.DataDir, "save-backups");

    public static string SavesFolder(GammaLayout layout, ModpackMetadata metadata, string anomaly)
    {
      var profile = Path.Combine(layout.Profiles, metadata.FolderName);
      var settings = Path.Combine(profile, "settings.ini");
      var local = File.Exists(settings) && File.ReadLines(settings)
        .Any(line => line.Replace(" ", string.Empty).Equals("LocalSaves=true", StringComparison.OrdinalIgnoreCase));
      return local ? Path.Combine(profile, "saves") : Path.Combine(anomaly, "appdata", "savedgames");
    }

    /// <summary>O zip dos saves (nulo: não tem save nenhum pra guardar).</summary>
    public static string? Create(GammaLayout layout, string saves)
    {
      if (!Directory.Exists(saves) || !Directory.EnumerateFiles(saves, "*", SearchOption.AllDirectories).Any())
        return null;

      var dir = Dir(layout);
      Directory.CreateDirectory(dir);
      var stamp = $"saves-{DateTime.Now:yyyyMMdd-HHmmss}";
      var zip = Path.Combine(dir, stamp + ".zip");
      for (var n = 2; File.Exists(zip); n++)
        zip = Path.Combine(dir, $"{stamp}-{n}.zip");
      // o zip vai pela metade se o disco encher: só entra com o nome certo depois de pronto
      var partial = zip + ".tmp";
      try
      {
        ZipFile.CreateFromDirectory(saves, partial, CompressionLevel.Fastest, includeBaseDirectory: false);
        File.Move(partial, zip);
      }
      finally
      {
        if (File.Exists(partial))
          File.Delete(partial);
      }

      foreach (var old in new DirectoryInfo(dir).GetFiles("saves-*.zip").OrderByDescending(f => f.LastWriteTimeUtc).Skip(Kept))
        old.Delete();
      return zip;
    }
  }
}
