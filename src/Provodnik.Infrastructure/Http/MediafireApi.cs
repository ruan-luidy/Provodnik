using System.Net.Http;
using System.Text.Json;

namespace Provodnik.Infrastructure.Http
{
  /// <summary>Um arquivo de uma pasta pública do Mediafire. Sha256 é o campo hash da API (o SHA-256 do arquivo).</summary>
  public sealed record MediafireFile(string Name, long Size, string QuickKey, string? Sha256);

  /// <summary>
  /// A API pública do Mediafire (folder/get_content), que responde sem o Cloudflare da página: o nome, o tamanho, a
  /// chave da página e o hash de cada arquivo da pasta. O link de download em si só sai pela página (navegador).
  /// </summary>
  public sealed class MediafireApi(HttpClient http)
  {
    public async Task<IReadOnlyList<MediafireFile>> ListFolderAsync(string folderKey, CancellationToken cancellationToken)
    {
      var url = $"https://www.mediafire.com/api/1.5/folder/get_content.php?folder_key={Uri.EscapeDataString(folderKey)}&content_type=files&response_format=json";
      await using var stream = await http.GetStreamAsync(url, cancellationToken).ConfigureAwait(false);
      using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

      var response = json.RootElement.GetProperty("response");
      if (response.TryGetProperty("result", out var result) && result.GetString() != "Success")
        throw new InvalidDataException($"Mediafire: {response.GetProperty("message").GetString()}");

      var files = new List<MediafireFile>();
      foreach (var file in response.GetProperty("folder_content").GetProperty("files").EnumerateArray())
      {
        var hash = file.TryGetProperty("hash", out var h) ? h.GetString() : null;
        files.Add(new MediafireFile(
          file.GetProperty("filename").GetString() ?? string.Empty,
          Number(file.GetProperty("size")),
          file.GetProperty("quickkey").GetString() ?? string.Empty,
          hash is { Length: 64 } ? hash.ToLowerInvariant() : null));
      }
      return files;
    }

    // o Mediafire manda número às vezes como texto
    private static long Number(JsonElement value) =>
      value.ValueKind == JsonValueKind.Number ? value.GetInt64() : long.Parse(value.GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture);
  }
}
