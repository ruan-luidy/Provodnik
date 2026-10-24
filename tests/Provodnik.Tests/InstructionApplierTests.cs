using Provodnik.Features.Install;

namespace Provodnik.Tests
{
  public sealed class InstructionApplierTests : IDisposable
  {
    private readonly string _root = Path.Combine(Path.GetTempPath(), "provodnik-tests", Guid.NewGuid().ToString("N"));

    public InstructionApplierTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void Touch(string relative)
    {
      var path = Path.Combine(_root, relative);
      Directory.CreateDirectory(Path.GetDirectoryName(path)!);
      File.WriteAllText(path, relative);
    }

    private bool Has(string relative) => File.Exists(Path.Combine(_root, relative));

    [Fact]
    public void Pasta_com_gamedata_sobe_pra_raiz()
    {
      Touch(@"Mod 1.0\00 MAIN FILES\gamedata\configs\a.ltx");
      Touch(@"Mod 1.0\readme.txt");

      InstructionApplier.Apply(_root, [@"Mod 1.0\00 MAIN FILES"], _ => { });

      Assert.True(Has(@"gamedata\configs\a.ltx"));
      Assert.False(Directory.Exists(Path.Combine(_root, "Mod 1.0")));
    }

    [Fact]
    public void Pasta_sem_gamedata_vai_pra_dentro_de_gamedata()
    {
      Touch(@"[1.1.0] Better Sound\bin\sounds\x.ogg");

      InstructionApplier.Apply(_root, [@"[1.1.0] Better Sound\bin"], _ => { });

      Assert.True(Has(@"gamedata\sounds\x.ogg"));
    }

    [Fact]
    public void Varias_instrucoes_sobrescrevem_na_ordem()
    {
      Touch(@"pack\main\gamedata\w.ltx");
      Touch(@"pack\opt\gamedata\w.ltx");
      File.WriteAllText(Path.Combine(_root, @"pack\opt\gamedata\w.ltx"), "opcional");

      InstructionApplier.Apply(_root, [@"pack\main", @"pack\opt"], _ => { });

      Assert.Equal("opcional", File.ReadAllText(Path.Combine(_root, @"gamedata\w.ltx")));
    }

    [Fact]
    public void Caminho_dentro_de_um_ja_movido_some_como_no_original()
    {
      Touch(@"compilation\ammo maker\gamedata\a.ltx");
      Touch(@"compilation\ammo maker\no recipes mode\gamedata\b.ltx");
      var warnings = new List<string>();

      InstructionApplier.Apply(_root, [@"compilation\ammo maker", @"compilation\ammo maker\no recipes mode"], warnings.Add);

      Assert.True(Has(@"gamedata\a.ltx"));
      // o robocopy /move já levou "no recipes mode" pra raiz, e a limpeza final apaga ela
      Assert.False(Has(@"gamedata\b.ltx"));
      Assert.Single(warnings);
    }

    [Fact]
    public void Barra_no_comeco_e_barra_normal_valem_como_no_original()
    {
      Touch(@"Black_Market-main\gamedata\a.ltx");
      Touch(@"gamma_loading_screens-main\CS Eden\gamedata\b.ltx");
      var warnings = new List<string>();

      InstructionApplier.Apply(_root, [@"\Black_Market-main", "gamma_loading_screens-main/CS Eden"], warnings.Add);

      Assert.Empty(warnings);
      Assert.True(Has(@"gamedata\a.ltx"));
      Assert.True(Has(@"gamedata\b.ltx"));
    }

    [Fact]
    public void Pasta_somente_leitura_do_arquivo_nao_trava_a_limpeza()
    {
      Touch(@"x\gamedata\a.ltx");
      Touch(@"Docs\leia.txt");
      File.SetAttributes(Path.Combine(_root, "Docs"), FileAttributes.Directory | FileAttributes.ReadOnly);
      File.SetAttributes(Path.Combine(_root, @"Docs\leia.txt"), FileAttributes.ReadOnly);

      InstructionApplier.Apply(_root, ["x"], _ => { });

      Assert.False(Directory.Exists(Path.Combine(_root, "Docs")));
    }

    [Fact]
    public void Limpeza_mantem_so_as_pastas_do_jogo()
    {
      Touch(@"x\gamedata\a.ltx");
      Touch(@"fomod\info.xml");
      Touch(@"Docs\leia.txt");

      InstructionApplier.Apply(_root, ["x"], _ => { });

      Assert.True(Has(@"fomod\info.xml"));
      Assert.False(Directory.Exists(Path.Combine(_root, "Docs")));
    }
  }
}
