using System.Text.Json;
using Provodnik.Core.Install;

namespace Provodnik.Features.Install
{
  /// <summary>O .provodnik\state.json: gravado num .tmp e trocado pelo arquivo final, então nunca fica pela metade.</summary>
  public sealed class InstallStateStore(string path)
  {
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public InstallState Load()
    {
      try
      {
        if (File.Exists(path) && JsonSerializer.Deserialize<InstallState>(File.ReadAllText(path), Options) is { } state)
        {
          // o JSON volta com dicionário que diferencia maiúscula; pasta de mod no Windows não diferencia
          return state with
          {
            Mods = new Dictionary<string, InstalledMod>(state.Mods, StringComparer.OrdinalIgnoreCase),
            ETags = new Dictionary<string, string>(state.ETags, StringComparer.OrdinalIgnoreCase),
          };
        }
      }
      catch (Exception ex) when (ex is JsonException or IOException)
      {
      }
      return new InstallState();
    }

    public void Save(InstallState state)
    {
      Directory.CreateDirectory(Path.GetDirectoryName(path)!);
      var temp = path + ".tmp";
      File.WriteAllText(temp, JsonSerializer.Serialize(state, Options));
      File.Move(temp, path, overwrite: true);
    }
  }
}
