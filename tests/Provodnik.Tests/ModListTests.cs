using Provodnik.Core.Modpack;

namespace Provodnik.Tests
{
  public class ModListTests
  {
    private static ModList Real() => ModList.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "mods.txt")));

    [Fact]
    public void Lista_real_tem_todas_as_linhas_e_os_separadores()
    {
      var list = Real();

      Assert.Equal(492, list.Entries.Count);
      Assert.Equal(19, list.Entries.Count(e => e.IsSeparator));
      Assert.Equal(473, list.Mods.Count);
    }

    [Fact]
    public void Pasta_do_separador_sai_igual_a_do_original()
    {
      var audio = Real().Entries[0];

      Assert.True(audio.IsSeparator);
      Assert.Equal("1- Audio_separator", audio.FolderName);
    }

    [Fact]
    public void Pasta_do_mod_junta_numero_nome_e_autor()
    {
      var list = Real();

      Assert.Equal("10- Better sound - Grokitach", list.Entries[9].FolderName);
      Assert.Equal("12- PDA Radio Extended - Starcry_", list.Entries[11].FolderName);
    }

    [Fact]
    public void Linha_do_moddb_traz_arquivo_md5_e_instrucoes()
    {
      var sco = Real().Entries[2];

      Assert.Equal(ModKind.ModDb, sco.Kind);
      Assert.Equal("Soundscape_Overhaul_3.0.zip", sco.ArchiveName);
      Assert.Equal("0fd082a51a59f4d35a18a2431cd7f344", sco.Md5);
      Assert.Equal(4, sco.Instructions.Count);
      Assert.Equal("Solarint", sco.Author);
    }

    [Fact]
    public void Instrucao_zero_vira_lista_vazia()
    {
      var theme = Real().Entries[1];

      Assert.Empty(theme.Instructions);
    }

    [Fact]
    public void Zip_do_github_tem_o_nome_do_repositorio()
    {
      var entry = ModList.ParseLine(7, "https://github.com/ahuyn/anomaly-exo/archive/refs/tags/latest.zip\tanomaly-exo-latest\t - arti\t Powered Exos");

      Assert.Equal(ModKind.GitHub, entry.Kind);
      Assert.Equal("anomaly-exo.zip", entry.ArchiveName);
      Assert.Null(entry.Md5);
      Assert.Equal("7- Powered Exos - arti", entry.FolderName);
    }

    [Fact]
    public void Mods_que_dividem_o_mesmo_arquivo_apontam_pro_mesmo_nome()
    {
      var compilation = Real().Mods.Where(m => m.Url == "https://github.com/ahuyn/anomaly-compilation/archive/refs/tags/latest.zip").ToList();

      Assert.Equal(4, compilation.Count);
      Assert.All(compilation, m => Assert.Equal("anomaly-compilation.zip", m.ArchiveName));
    }

    [Fact]
    public void Linha_sem_http_e_separador_mesmo_com_espaco()
    {
      Assert.Equal(ModKind.Separator, ModList.KindOf(" Gunplay"));
      Assert.Equal(ModKind.LargeFiles, ModList.KindOf("https://github.com/Grokitach/gamma_large_files_v2/x"));
    }

    [Fact]
    public void Metadados_do_gamma_dao_a_pasta_sem_o_ponto_final()
    {
      var metadata = ModpackMetadata.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "modpack_maker_metadata.txt")));

      Assert.Equal("G.A.M.M.A.", metadata.Name);
      Assert.Equal("G.A.M.M.A", metadata.FolderName);
      Assert.Equal("modpack_data/modlist.txt", metadata.ModOrganizerModList);
      Assert.Equal("modpack_addons", metadata.AdditionalFiles);
    }
  }
}
