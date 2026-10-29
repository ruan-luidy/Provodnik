namespace Provodnik.Features.Bootstrap
{
  /// <summary>
  /// Onde ficou o conteúdo de um arquivo extraído: o .7z pode trazer tudo solto ou dentro de uma pasta ("GAMMA\",
  /// "Anomaly-1.5.3\"). A raiz é a pasta mais rasa que tem o arquivo marcador (ModOrganizer.exe, AnomalyLauncher.exe).
  /// </summary>
  public static class ExtractedRoot
  {
    public static string? Find(string folder, string marker, int maxDepth = 3)
    {
      var level = new List<string> { folder };
      for (var depth = 0; depth <= maxDepth && level.Count > 0; depth++)
      {
        foreach (var dir in level)
        {
          if (File.Exists(Path.Combine(dir, marker)))
            return dir;
        }
        level = level.Where(Directory.Exists).SelectMany(Directory.EnumerateDirectories).ToList();
      }
      return null;
    }
  }
}
