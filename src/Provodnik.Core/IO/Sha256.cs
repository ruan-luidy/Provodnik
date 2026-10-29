using System.Security.Cryptography;

namespace Provodnik.Core.IO
{
  /// <summary>O SHA-256 do arquivo em hexa minúsculo, com progresso (o RC3 tem 12 GB: leva um minuto).</summary>
  public static class Sha256
  {
    public static async Task<string> HashAsync(string path, IProgress<double>? progress, CancellationToken cancellationToken)
    {
      await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, FileOptions.Asynchronous | FileOptions.SequentialScan);
      using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
      var buffer = new byte[1 << 20];
      var length = Math.Max(1, stream.Length);
      long read = 0;
      var lastReport = 0L;
      int count;
      while ((count = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
      {
        hash.AppendData(buffer, 0, count);
        read += count;
        if (read - lastReport >= 64 << 20)
        {
          lastReport = read;
          progress?.Report((double)read / length);
        }
      }
      progress?.Report(1);
      return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
  }
}
