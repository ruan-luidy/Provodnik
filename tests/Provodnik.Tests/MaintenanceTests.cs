using System.IO.Compression;
using Provodnik.Core.Layout;
using Provodnik.Core.Modpack;
using Provodnik.Features.Install;
using Provodnik.Features.ModOrganizer;
using Provodnik.Features.Mods;

namespace Provodnik.Tests
{
  /// <summary>A limpeza de downloads\, a volta do perfil de antes e o backup dos saves.</summary>
  public sealed class MaintenanceTests : IDisposable
  {
    private readonly string _root = Path.Combine(Path.GetTempPath(), "provodnik-maintenance", Guid.NewGuid().ToString("N"));
    private readonly GammaLayout _layout;

    public MaintenanceTests() => _layout = new GammaLayout(_root);

    public void Dispose()
    {
      try
      {
        Directory.Delete(_root, recursive: true);
      }
      catch (IOException)
      {
      }
    }

    private static string Line(string url, string author, string name, string archive) =>
      $"{url}\t0\t - {author}\t {name}\thttps://www.moddb.com/mods/x\t{archive}\t";

    private void Download(string name, int size = 10)
    {
      Directory.CreateDirectory(_layout.Downloads);
      File.WriteAllBytes(Path.Combine(_layout.Downloads, name), new byte[size]);
    }

    private void ModFolder(string name, string meta)
    {
      var folder = Path.Combine(_layout.Mods, name);
      Directory.CreateDirectory(folder);
      File.WriteAllText(Path.Combine(folder, "meta.ini"), meta);
    }

    // ===== downloads\ =====

    [Fact]
    public void Limpeza_so_pega_o_que_nada_mais_usa()
    {
      var list = ModList.Parse(Line("https://www.moddb.com/addons/start/1", "Ana", "Som", "som-2.0.zip"));
      Download("som-2.0.zip");
      Download("som-1.0.zip", 100);
      Download("velho.7z.part", 5);
      Download("som-2.0.zip.part");
      // a sobra do GAMMA que ainda tem pasta e o mod da pessoa instalado pelo MO2 ficam
      Download("sobra.rar");
      ModFolder("9- Sobra - Caio", "[General]\r\nversion=sobra.rar\r\ninstallationFile=sobra.rar\r\n");
      Download("Coop-1.4.zip");
      ModFolder("Coop", "[General]\r\nversion=1.4\r\ninstallationFile=C:/GAMMA/downloads/Coop-1.4.zip\r\n");
      // o que o MO2 baixou pra pessoa (tem .meta) e ainda não foi instalado também fica
      Download("Novo-mod.7z");
      Download("Novo-mod.7z.meta");
      Download("anotacoes.txt");

      var old = DownloadsCleaner.Find(_layout, list);

      Assert.Equal(["som-1.0.zip", "velho.7z.part"], old.Select(o => o.Name));
      Assert.Equal(105, old.Sum(o => o.Size));

      var result = DownloadsCleaner.Delete(old);
      Assert.Equal(2, result.Deleted);
      Assert.Equal(105, result.Freed);
      Assert.Empty(result.Failed);
      Assert.False(File.Exists(Path.Combine(_layout.Downloads, "som-1.0.zip")));
      Assert.True(File.Exists(Path.Combine(_layout.Downloads, "som-2.0.zip")));
      Assert.True(File.Exists(Path.Combine(_layout.Downloads, "Novo-mod.7z")));
    }

    [Fact]
    public void Limpeza_sem_downloads_nao_acha_nada()
    {
      Assert.Empty(DownloadsCleaner.Find(_layout, ModList.Parse(string.Empty)));
    }

    // ===== perfil de antes =====

    private string Profile => Path.Combine(_layout.Profiles, ModpackMetadata.Gamma.FolderName, "modlist.txt");

    private void SetUp(string gamma, string current)
    {
      Directory.CreateDirectory(_layout.ProfileTemplate);
      var file = Path.Combine(_layout.Definition(ModpackMetadata.Gamma.FolderName), "modpack_data", "modlist.txt");
      Directory.CreateDirectory(Path.GetDirectoryName(file)!);
      File.WriteAllText(file, gamma);
      Directory.CreateDirectory(Path.GetDirectoryName(Profile)!);
      File.WriteAllText(Profile, current);
    }

    [Fact]
    public void Voltar_o_perfil_traz_a_lista_de_antes_e_voltar_de_novo_desfaz()
    {
      var folder = Path.Combine(_layout.Mods, "Shuler");
      Directory.CreateDirectory(folder);
      File.WriteAllText(Path.Combine(folder, "shuler.script"), "x");
      SetUp("+2- Som - Ana", "-2- Som - Ana");
      ProfileSetup.Apply(_layout, ModpackMetadata.Gamma);
      var merged = File.ReadAllText(Profile);
      Assert.Equal("+2- Som - Ana", merged);

      var restored = ProfileSetup.RestoreLatest(_layout, ModpackMetadata.Gamma);

      Assert.NotNull(restored);
      Assert.Equal("-2- Som - Ana", File.ReadAllText(Profile));

      // a de agora virou cópia: voltar de novo desfaz a volta
      ProfileSetup.RestoreLatest(_layout, ModpackMetadata.Gamma);
      Assert.Equal(merged, File.ReadAllText(Profile));
    }

    [Fact]
    public void Sem_copia_nao_volta_nada()
    {
      SetUp("+2- Som - Ana", "+2- Som - Ana");

      Assert.Null(ProfileSetup.LatestBackup(_layout));
      Assert.Null(ProfileSetup.RestoreLatest(_layout, ModpackMetadata.Gamma));
      Assert.Equal("+2- Som - Ana", File.ReadAllText(Profile));
    }

    // ===== saves =====

    [Fact]
    public void Saves_ficam_no_Anomaly_ou_no_perfil_quando_ele_tem_os_dele()
    {
      var anomaly = Path.Combine(_root, "Anomaly");
      var profile = Path.Combine(_layout.Profiles, ModpackMetadata.Gamma.FolderName);
      Directory.CreateDirectory(profile);
      File.WriteAllText(Path.Combine(profile, "settings.ini"), "[General]\r\nLocalSaves=false\r\n");
      Assert.Equal(Path.Combine(anomaly, "appdata", "savedgames"), SavesBackup.SavesFolder(_layout, ModpackMetadata.Gamma, anomaly));

      File.WriteAllText(Path.Combine(profile, "settings.ini"), "[General]\r\nLocalSaves=true\r\n");
      Assert.Equal(Path.Combine(profile, "saves"), SavesBackup.SavesFolder(_layout, ModpackMetadata.Gamma, anomaly));
    }

    [Fact]
    public void Backup_dos_saves_vira_zip_e_guarda_os_cinco_ultimos()
    {
      var saves = Path.Combine(_root, "Anomaly", "appdata", "savedgames");
      Directory.CreateDirectory(saves);
      File.WriteAllText(Path.Combine(saves, "l01_escape_16.scop"), "save");
      File.WriteAllText(Path.Combine(saves, "l01_escape_16.dds"), "foto");

      string? last = null;
      for (var i = 0; i < 7; i++)
        last = SavesBackup.Create(_layout, saves);

      Assert.NotNull(last);
      using (var zip = ZipFile.OpenRead(last!))
        Assert.Equal(["l01_escape_16.dds", "l01_escape_16.scop"], zip.Entries.Select(e => e.FullName).Order());
      var kept = Directory.GetFiles(SavesBackup.Dir(_layout));
      Assert.Equal(5, kept.Length);
      Assert.Contains(last, kept);
      Assert.DoesNotContain(kept, k => k.EndsWith(".tmp", StringComparison.Ordinal));
    }

    [Fact]
    public void Sem_save_nao_faz_zip()
    {
      Assert.Null(SavesBackup.Create(_layout, Path.Combine(_root, "nada")));
      var empty = Path.Combine(_root, "vazio");
      Directory.CreateDirectory(empty);
      Assert.Null(SavesBackup.Create(_layout, empty));
      Assert.False(Directory.Exists(SavesBackup.Dir(_layout)));
    }
  }
}
