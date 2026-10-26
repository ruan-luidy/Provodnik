using Provodnik.Core.Layout;
using Provodnik.Core.Modpack;
using Provodnik.Features.ModOrganizer;
using Provodnik.Features.Verify;

namespace Provodnik.Tests
{
  public sealed class ActiveModsCheckTests : IDisposable
  {
    private const string Typo = "DarkasleQif's and iTheon's Parts Condition Color Thresholds Changes";
    private const string Real = "Darkasleif's and iTheon's Parts Condition Color Thresholds Changes";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "provodnik-active", Guid.NewGuid().ToString("N"));
    private readonly GammaLayout _layout;

    public ActiveModsCheckTests() => _layout = new GammaLayout(_root);

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

    private void Mod(string name)
    {
      var folder = Path.Combine(_layout.Mods, name);
      Directory.CreateDirectory(folder);
      File.WriteAllText(Path.Combine(folder, "script.txt"), "x");
    }

    private void ModList(params string[] lines)
    {
      var file = Path.Combine(_layout.Definition(ModpackMetadata.Gamma.FolderName), "modpack_data", "modlist.txt");
      Directory.CreateDirectory(Path.GetDirectoryName(file)!);
      File.WriteAllText(file, string.Join("\r\n", lines));
    }

    [Fact]
    public void Nome_errado_na_lista_do_gamma_conta_pela_pasta_de_verdade()
    {
      Mod("312- Mod Bom - Autor");
      Mod(Real);
      ModList("+312- Mod Bom - Autor", "+" + Typo);

      var result = ActiveModsCheck.Run(_layout, ModpackMetadata.Gamma);

      Assert.Equal(2, result.Expected);
      Assert.Equal(2, result.Found);
      Assert.Empty(result.Missing);
      Assert.Equal([Typo], result.Corrected);
      Assert.True(result.Complete);
    }

    [Fact]
    public void Mod_sem_pasta_e_sem_parecido_continua_faltando()
    {
      Mod("312- Mod Bom - Autor");
      Mod("313- Mod Bom - Autor 2");
      ModList("+312- Mod Bom - Autor", "+313- Mod Bom - Autor", "+G.A.M.M.A. New Main Menu");

      var result = ActiveModsCheck.Run(_layout, ModpackMetadata.Gamma);

      Assert.Equal(3, result.Expected);
      Assert.Equal(1, result.Found);
      Assert.Equal(["313- Mod Bom - Autor", "G.A.M.M.A. New Main Menu"], result.Missing);
      Assert.Empty(result.Corrected);
    }

    [Fact]
    public void Perfil_sai_com_o_nome_corrigido_no_mesmo_lugar()
    {
      Mod("312- Mod Bom - Autor");
      Mod("Darkasleif's Stop Selling Documents");
      Mod(Real);
      Mod("Darkasleif's Nimble Upgrades Guns");
      ModList("# comentário", "+312- Mod Bom - Autor", "+Darkasleif's Stop Selling Documents", "+" + Typo, "-Darkasleif's Nimble Upgrades Guns");
      Directory.CreateDirectory(_layout.ProfileTemplate);
      var fixes = new List<(string From, string To)>();

      var profile = ProfileSetup.Apply(_layout, ModpackMetadata.Gamma, fixes).Profile;

      var lines = File.ReadAllText(Path.Combine(profile, "modlist.txt")).Split("\r\n");
      Assert.Equal(["# comentário", "+312- Mod Bom - Autor", "+Darkasleif's Stop Selling Documents", "+" + Real, "-Darkasleif's Nimble Upgrades Guns"], lines);
      Assert.Equal([(Typo, Real)], fixes);
    }

    [Fact]
    public void Nao_troca_pra_pasta_que_ja_tem_linha_propria()
    {
      var fixes = new List<(string From, string To)>();

      var text = ModListNames.Repair("+" + Typo + "\n-" + Real, [Real], fixes);

      Assert.Equal("+" + Typo + "\n-" + Real, text);
      Assert.Empty(fixes);
    }

    [Theory]
    [InlineData("DarkasleQif's and iTheon's Parts", "Darkasleif's and iTheon's Parts", true)]
    [InlineData("G.A.M.M.A. Main Menu", "g.a.m.m.a. main menu", false)]
    [InlineData("Curto", "Curta", false)]
    [InlineData("100- Mod Qualquer - Autor", "101- Mod Qualquer - Autor", false)]
    [InlineData("Darkasleif's and iTheon's Parts", "Darkasleif's Parts Condition", false)]
    public void Erro_de_digitacao(string listed, string folder, bool expected) =>
      Assert.Equal(expected, ModListNames.IsTypo(listed, folder));
  }
}
