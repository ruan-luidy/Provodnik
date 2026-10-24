using Provodnik.Core.Layout;
using Provodnik.Core.Modpack;
using Provodnik.Features.Verify;

namespace Provodnik.Tests
{
  public sealed class ActiveModsCheckTests : IDisposable
  {
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
    public void Nome_errado_na_lista_do_gamma_fica_fora_da_conta()
    {
      Mod("312- Mod Bom - Autor");
      Mod("Darkasleif's and iTheon's Parts Condition Color Thresholds Changes");
      ModList("+312- Mod Bom - Autor", "+DarkasleQif's and iTheon's Parts Condition Color Thresholds Changes");

      var result = ActiveModsCheck.Run(_layout, ModpackMetadata.Gamma);

      Assert.Equal(1, result.Expected);
      Assert.Equal(1, result.Found);
      Assert.Empty(result.Missing);
      Assert.Equal(["DarkasleQif's and iTheon's Parts Condition Color Thresholds Changes"], result.Misnamed);
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
      Assert.Empty(result.Misnamed);
    }

    [Theory]
    [InlineData("DarkasleQif's and iTheon's Parts", "Darkasleif's and iTheon's Parts", true)]
    [InlineData("G.A.M.M.A. Main Menu", "g.a.m.m.a. main menu", false)]
    [InlineData("Curto", "Curta", false)]
    [InlineData("100- Mod Qualquer - Autor", "101- Mod Qualquer - Autor", false)]
    [InlineData("Darkasleif's and iTheon's Parts", "Darkasleif's Parts Condition", false)]
    public void Erro_de_digitacao(string listed, string folder, bool expected) =>
      Assert.Equal(expected, ActiveModsCheck.IsTypo(listed, folder));
  }
}
