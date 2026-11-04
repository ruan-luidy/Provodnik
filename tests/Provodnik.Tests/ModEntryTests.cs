using Provodnik.Core.Modpack;

namespace Provodnik.Tests
{
  /// <summary>A página que o botão de globo abre: a do addon, o repositório do GitHub, nunca um link de download.</summary>
  public sealed class ModEntryTests
  {
    private static ModEntry Entry(ModKind kind, string? url, string? page) =>
      new(1, kind, url, ["0"], " - Ana", "Som", page, "som.zip", null);

    [Fact]
    public void ModDb_abre_a_pagina_do_addon()
    {
      var entry = Entry(ModKind.ModDb, "https://www.moddb.com/addons/start/1", "https://www.moddb.com/mods/stalker-anomaly/addons/som");

      Assert.Equal("https://www.moddb.com/mods/stalker-anomaly/addons/som", entry.WebPage);
    }

    [Fact]
    public void ModDb_sem_pagina_nao_cai_pro_link_de_download()
    {
      Assert.Null(Entry(ModKind.ModDb, "https://www.moddb.com/addons/start/1", null).WebPage);
      Assert.Null(Entry(ModKind.ModDb, "https://www.moddb.com/addons/start/1", "").WebPage);
    }

    [Fact]
    public void Pagina_que_e_link_de_download_nao_conta()
    {
      Assert.Null(Entry(ModKind.ModDb, "https://www.moddb.com/addons/start/1", "https://www.moddb.com/addons/start/1").WebPage);
      Assert.Null(Entry(ModKind.ModDb, "https://www.moddb.com/addons/start/1", "https://www.moddb.com/downloads/start/277404").WebPage);
    }

    [Fact]
    public void GitHub_sem_pagina_abre_o_repositorio()
    {
      var entry = Entry(ModKind.GitHub, "https://github.com/ana/som/archive/main.zip", null);

      Assert.Equal("https://github.com/ana/som/archive/main.zip", entry.WebPage);
    }

    [Fact]
    public void Gofile_sem_pagina_fica_sem_botao()
    {
      Assert.Null(Entry(ModKind.GoFile, "https://gofile.io/d/abc", "0").WebPage);
    }
  }
}
