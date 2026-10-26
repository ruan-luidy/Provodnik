using Provodnik.Core.Layout;
using Provodnik.Features.Launch;

namespace Provodnik.Tests
{
  public sealed class GameLauncherTests : IDisposable
  {
    private readonly string _root = Path.Combine(Path.GetTempPath(), "provodnik-launcher", Guid.NewGuid().ToString("N"));

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

    [Fact]
    public void Perfil_vai_antes_do_atalho_do_executavel()
    {
      Assert.Equal("-p \"G.A.M.M.A\" \"moshortcut://:Anomaly (DX11-AVX)\"", GameLauncher.Arguments("G.A.M.M.A", "\"moshortcut://:Anomaly (DX11-AVX)\""));
      Assert.Equal("-p \"CLIKRΔD - ROMEO\"", GameLauncher.Arguments("CLIKRΔD - ROMEO", null));
      Assert.Equal("\"moshortcut://:Anomaly (DX11)\"", GameLauncher.Arguments(null, "\"moshortcut://:Anomaly (DX11)\""));
      Assert.Null(GameLauncher.Arguments(" ", null));
    }

    [Fact]
    public void Perfis_com_o_do_GAMMA_primeiro()
    {
      var layout = new GammaLayout(_root);
      foreach (var name in new[] { "Zeta", "G.A.M.M.A", "Alfa" })
        Directory.CreateDirectory(Path.Combine(layout.Profiles, name));

      Assert.Equal(["G.A.M.M.A", "Alfa", "Zeta"], GameLauncher.Profiles(layout, "G.A.M.M.A"));
      Assert.Empty(GameLauncher.Profiles(new GammaLayout(Path.Combine(_root, "nada")), "G.A.M.M.A"));
    }
  }
}
