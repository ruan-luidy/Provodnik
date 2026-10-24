using System.Text.Json;
using Provodnik.Core.IO;

namespace Provodnik.Features.Verify
{
  /// <summary>
  /// O md5 de cada arquivo em downloads\, guardado com o tamanho e a data dele (.provodnik\hashes.json). O original
  /// recalculava o md5 dos ~100 GB a cada atualização; aqui só o que mudou de tamanho ou de data é lido de novo.
  /// </summary>
  public sealed class HashCache
  {
    private sealed record Entry(long Size, long Ticks, string Md5);

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    private readonly string _path;
    private readonly Dictionary<string, Entry> _entries;
    private readonly Lock _gate = new();
    private bool _dirty;

    private HashCache(string path, Dictionary<string, Entry> entries)
    {
      _path = path;
      _entries = entries;
    }

    public static HashCache Load(string path)
    {
      try
      {
        if (File.Exists(path) && JsonSerializer.Deserialize<Dictionary<string, Entry>>(File.ReadAllText(path), Options) is { } entries)
          return new HashCache(path, new Dictionary<string, Entry>(entries, StringComparer.OrdinalIgnoreCase));
      }
      catch (Exception ex) when (ex is JsonException or IOException)
      {
      }
      return new HashCache(path, new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase));
    }

    public async Task<string> GetAsync(string file, CancellationToken cancellationToken)
    {
      var info = new FileInfo(file);
      lock (_gate)
      {
        if (_entries.TryGetValue(info.FullName, out var entry) && entry.Size == info.Length && entry.Ticks == info.LastWriteTimeUtc.Ticks)
          return entry.Md5;
      }

      var md5 = await Md5.HashAsync(file, cancellationToken).ConfigureAwait(false);
      Remember(file, md5);
      return md5;
    }

    public void Remember(string file, string md5)
    {
      var info = new FileInfo(file);
      lock (_gate)
      {
        _entries[info.FullName] = new Entry(info.Length, info.LastWriteTimeUtc.Ticks, md5);
        _dirty = true;
      }
    }

    public void Save()
    {
      lock (_gate)
      {
        if (!_dirty)
          return;
        // arquivo apagado do downloads\ sai do cache
        foreach (var key in _entries.Keys.Where(k => !File.Exists(k)).ToList())
          _entries.Remove(key);

        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(_entries, Options));
        File.Move(temp, _path, overwrite: true);
        _dirty = false;
      }
    }
  }
}
