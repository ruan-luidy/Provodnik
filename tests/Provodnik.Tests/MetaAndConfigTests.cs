using Provodnik.Core.Layout;
using Provodnik.Core.Modpack;

namespace Provodnik.Tests
{
  public class MetaAndConfigTests
  {
    [Fact]
    public void Meta_ini_recebe_arquivo_versao_e_pagina()
    {
      var meta = MetaIni.Build(MetaIni.DefaultTemplate, "PDAradioExtendedV1.3.7z", "https://www.moddb.com/addon");

      Assert.Contains("installationFile=PDAradioExtendedV1.3.7z", meta);
      Assert.Contains("\r\nversion=PDAradioExtendedV1.3.7z", meta);
      Assert.Contains("newestVersion=PDAradioExtendedV1.3.7z", meta);
      Assert.Contains("url=https://www.moddb.com/addon", meta);
      Assert.Contains("hasCustomURL=true", meta);
      Assert.Contains("gameName=stalkeranomaly", meta);
    }

    [Fact]
    public void Ini_do_mo2_da_o_anomaly_e_os_executaveis()
    {
      const string ini = "[General]\ngameName=STALKER Anomaly\ngamePath=@ByteArray(D:\\\\Anomaly)\nselected_profile=@ByteArray(G.A.M.M.A)\n" +
        "[customExecutables]\n1\\binary=D:/Anomaly/AnomalyLauncher.exe\n1\\title=Anomaly Launcher\n" +
        "2\\binary=D:/Anomaly/bin/AnomalyDX11AVX.exe\n2\\title=Anomaly (DX11-AVX)\n";

      var config = Mo2Config.Parse(ini);

      Assert.Equal(@"D:\Anomaly", config.GamePath);
      Assert.Equal("G.A.M.M.A", config.SelectedProfile);
      Assert.Equal(2, config.Executables.Count);
      Assert.Equal("Anomaly (DX11-AVX)", Assert.Single(config.GameExecutables).Title);
    }

    [Fact]
    public void Sem_gamePath_usa_a_pasta_do_launcher_do_anomaly()
    {
      var config = Mo2Config.Parse("[customExecutables]\n1\\binary=E:/Jogos/Anomaly/AnomalyLauncher.exe\n1\\title=Anomaly Launcher\n");

      Assert.Equal(@"E:\Jogos\Anomaly", config.GamePath);
    }
  }
}
