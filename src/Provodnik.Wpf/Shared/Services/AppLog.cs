using System.IO;

namespace Provodnik.Wpf.Shared.Services
{
  /// <summary>Erros que escaparam vão pro errors.log em vez de sumir. Passou de 1 MB, começa de novo.</summary>
  public static class AppLog
  {
    private static readonly Lock Gate = new();

    public static void Error(Exception exception, string where)
    {
      try
      {
        lock (Gate)
        {
          var path = AppPaths.ErrorLog;
          if (File.Exists(path) && new FileInfo(path).Length > 1_000_000)
            File.Delete(path);

          File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {where}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
        }
      }
      catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
      {
      }
    }
  }
}
