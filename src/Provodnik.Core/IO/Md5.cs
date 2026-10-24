using System.Security.Cryptography;

namespace Provodnik.Core.IO
{
  /// <summary>O md5 do arquivo em hexa minúsculo, como vem no mods.txt.</summary>
  public static class Md5
  {
    public static async Task<string> HashAsync(string path, CancellationToken cancellationToken)
    {
      await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, FileOptions.Asynchronous | FileOptions.SequentialScan);
      var hash = await MD5.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
      return Convert.ToHexStringLower(hash);
    }
  }
}
