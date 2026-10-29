using System.Security.Cryptography;
using System.Text;
using Provodnik.Core.Archives;
using Provodnik.Core.Bootstrap;
using Provodnik.Core.Downloads;
using Provodnik.Core.Install;
using Provodnik.Core.Layout;
using Provodnik.Features.Bootstrap;

namespace Provodnik.Tests
{
  /// <summary>A instalação do zero: a checagem da pasta, o ModOrganizer.ini, o script de administrador e o caminho todo.</summary>
  public sealed class BootstrapTests : IDisposable
  {
    private const long GB = 1L << 30;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "provodnik-bootstrap", Guid.NewGuid().ToString("N"));

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

    private static BootstrapFacts Facts(long free = 300 * GB, DriveType drive = DriveType.Fixed, params string[] forbidden) => new(drive, free, forbidden);

    private static bool Blocked(BootstrapTarget target, BootstrapFacts facts) =>
      target.Check(facts).Any(i => i.Level == BootstrapIssueLevel.Blocker);

    [Fact]
    public void Gamma_e_anomaly_ficam_lado_a_lado_na_pasta_escolhida()
    {
      var target = BootstrapTarget.Under(@"D:\Jogos");

      Assert.Equal(@"D:\Jogos\GAMMA", target.Gamma);
      Assert.Equal(@"D:\Jogos\Anomaly", target.Anomaly);
      Assert.False(Blocked(target, Facts()));
    }

    [Fact]
    public void Checagem_barra_o_que_a_wiki_proibe()
    {
      Assert.True(Blocked(new BootstrapTarget(@"D:\GAMMA", @"E:\Anomaly"), Facts()));
      Assert.True(Blocked(new BootstrapTarget(@"D:\GAMMA", @"D:\GAMMA\Anomaly"), Facts()));
      Assert.True(Blocked(BootstrapTarget.Under(@"C:\Users\ana\Desktop"), Facts(forbidden: @"C:\Users\ana")));
      Assert.True(Blocked(BootstrapTarget.Under(@"E:\"), Facts(drive: DriveType.Removable)));
      // numa pasta vazia (no D:\ desta máquina pode já ter GAMMA e Anomaly, que descontam do espaço)
      Assert.True(Blocked(BootstrapTarget.Under(_root), Facts(free: 100 * GB)));
      // pasta que só começa igual à proibida (C:\Users2) não é dentro dela
      Assert.False(Blocked(BootstrapTarget.Under(@"C:\Users2"), Facts(forbidden: @"C:\Users")));
    }

    [Fact]
    public void Pouco_espaco_so_avisa_e_acento_no_caminho_avisa()
    {
      var issues = BootstrapTarget.Under(@"D:\Jogos Ação").Check(Facts(free: 170 * GB));

      Assert.DoesNotContain(issues, i => i.Level == BootstrapIssueLevel.Blocker);
      Assert.Equal(2, issues.Count(i => i.Level == BootstrapIssueLevel.Warning));
    }

    [Fact]
    public void Ini_novo_do_mo2_aponta_pro_anomaly_com_os_executaveis()
    {
      var ini = Mo2Instance.Configure(null, @"D:\GAMMA", @"D:\Anomaly", "G.A.M.M.A", "2.5.2");
      var config = Mo2Config.Parse(ini);

      Assert.Contains("gameName=STALKER Anomaly", ini);
      Assert.Contains(@"gamePath=@ByteArray(D:\\Anomaly)", ini);
      Assert.Contains("first_start=false", ini);
      Assert.Contains(@"10\arguments=\""D:\\Anomaly\""", ini);
      Assert.Equal(@"D:\Anomaly", config.GamePath);
      Assert.Equal("G.A.M.M.A", config.SelectedProfile);
      Assert.Equal(8, config.GameExecutables.Count);
      Assert.Equal(@"D:\Anomaly\bin\AnomalyDX11AVX.exe", config.GameExecutables[0].Binary);
      Assert.Contains("size=10", ini);
    }

    [Fact]
    public void Ini_que_ja_existe_guarda_o_resto_e_troca_os_executaveis()
    {
      const string existing = """
        [General]
        gameName=
        selected_profile=@ByteArray(Meu)
        version=2.5.0

        [customExecutables]
        size=1
        1\binary=C:/Velho/AnomalyLauncher.exe
        1\title=Anomaly Launcher

        [Geometry]
        MainWindow_monitor=0
        """;

      var ini = Mo2Instance.Configure(existing, @"E:\Jogos\GAMMA", @"E:\Jogos\Anomaly", "G.A.M.M.A", "2.5.2");
      var config = Mo2Config.Parse(ini);

      Assert.Equal(@"E:\Jogos\Anomaly", config.GamePath);
      Assert.Equal("Meu", config.SelectedProfile);
      Assert.Contains("version=2.5.0", ini);
      Assert.Contains("MainWindow_monitor=0", ini);
      Assert.DoesNotContain("C:/Velho", ini);
      Assert.StartsWith("[General]", ini);
    }

    [Fact]
    public void Script_de_administrador_junta_tudo_e_escapa_as_aspas()
    {
      var script = ElevatedSetup.Script(new ElevatedPlan([@"D:\O'Neil\GAMMA", @"D:\O'Neil\Anomaly"], @"C:\t\vc.exe", null, LongPaths: true, @"C:\t\r.txt"));

      Assert.Contains(@"Add-MpPreference -ExclusionPath 'D:\O''Neil\GAMMA','D:\O''Neil\Anomaly'", script);
      Assert.Contains(@"Install vcredist 'C:\t\vc.exe' '/install /quiet /norestart'", script);
      Assert.DoesNotContain("Install directx", script);
      Assert.Contains("LongPathsEnabled", script);
      Assert.Contains("O=Microsoft Corporation", script);
    }

    [Fact]
    public void Resultado_do_administrador_vira_dicionario()
    {
      var results = ElevatedSetup.ParseResults("\uFEFFdefender=ok\r\nvcredist=exit:3010\r\ndirectx=signature:NotSigned\r\n");

      Assert.True(ElevatedSetup.Succeeded(results["defender"]));
      Assert.True(ElevatedSetup.Succeeded(results["vcredist"]));
      Assert.False(ElevatedSetup.Succeeded(results["directx"]));
      Assert.True(ElevatedSetup.Succeeded("exit:1638"));
    }

    [Fact]
    public void Raiz_extraida_e_a_pasta_mais_rasa_com_o_marcador()
    {
      var marker = Path.Combine(_root, "x", "Anomaly-1.5.3", "AnomalyLauncher.exe");
      Directory.CreateDirectory(Path.GetDirectoryName(marker)!);
      File.WriteAllText(marker, "");

      Assert.Equal(Path.GetDirectoryName(marker), ExtractedRoot.Find(Path.Combine(_root, "x"), "AnomalyLauncher.exe"));
      Assert.Null(ExtractedRoot.Find(Path.Combine(_root, "x"), "ModOrganizer.exe"));
    }

    [Fact]
    public async Task Do_zero_baixa_confere_extrai_e_configura_o_mo2()
    {
      var target = BootstrapTarget.Under(_root);
      var services = new FakeServices();
      var pipeline = new BootstrapPipeline(services) { SevenZipSha256 = FakeServices.Hash("7zr"), SevenZipExtraSha256 = FakeServices.Hash("7zextra"), AnomalySha256 = FakeServices.Hash("anomaly") };
      var events = new List<BootstrapProgress>();

      var result = await pipeline.RunAsync(target, new BootstrapOptions(), new Sync<BootstrapProgress>(events.Add), CancellationToken.None);

      Assert.True(result.Succeeded, result.Error);
      Assert.True(target.GammaReady);
      Assert.True(target.AnomalyReady);
      Assert.Equal(target.Anomaly, Mo2Config.Load(target.Layout.ModOrganizerIni).GamePath);
      Assert.True(File.Exists(Path.Combine(target.Gamma, "portable.txt")));
      Assert.False(Directory.Exists(target.WorkDir));
      // o perfil que vem no RC3 é velho: a primeira instalação monta o perfil com a lista atual do GAMMA
      Assert.False(File.Exists(Path.Combine(target.Gamma, "profiles", "G.A.M.M.A", "modlist.txt")));
      // o RC3 pelo Mediafire (link da página do arquivo), o Anomaly pelo ModDB com o curl-impersonate
      Assert.Contains(services.Downloads, d => d.Url.Contains("mediafire") && !d.Impersonate);
      Assert.Contains(services.Downloads, d => d.Url.Contains("dbolical") && d.Impersonate);
      // a exclusão no Defender vem ligada: um UAC mesmo sem nada faltando
      Assert.True(services.Elevated);
      Assert.Contains(events, e => e.Step == BootstrapStep.ModOrganizer && e.State == StageState.Done);

      // rodando de novo, nada é baixado: tudo já está no lugar
      services.Downloads.Clear();
      var again = await pipeline.RunAsync(target, new BootstrapOptions(), new Sync<BootstrapProgress>(_ => { }), CancellationToken.None);
      Assert.True(again.Succeeded, again.Error);
      Assert.Empty(services.Downloads);
    }

    [Fact]
    public async Task Rc3_corrompido_e_apagado_e_a_instalacao_para()
    {
      var target = BootstrapTarget.Under(_root);
      var services = new FakeServices { CorruptGamma = true };
      var pipeline = new BootstrapPipeline(services) { SevenZipSha256 = FakeServices.Hash("7zr"), SevenZipExtraSha256 = FakeServices.Hash("7zextra"), AnomalySha256 = FakeServices.Hash("anomaly") };

      var result = await pipeline.RunAsync(target, new BootstrapOptions(), new Sync<BootstrapProgress>(_ => { }), CancellationToken.None);

      Assert.False(result.Succeeded);
      Assert.False(File.Exists(Path.Combine(target.Downloads, BootstrapSources.GammaArchive)));
      Assert.False(target.GammaReady);
    }

    private sealed class Sync<T>(Action<T> action) : IProgress<T>
    {
      public void Report(T value) => action(value);
    }

    /// <summary>O mundo de fora de mentira: o conteúdo de cada "arquivo" é um texto, e extrair cria a árvore certa.</summary>
    private sealed class FakeServices : IBootstrapServices, IFileDownloader, IArchiveExtractor
    {
      public List<DownloadRequest> Downloads { get; } = [];

      public bool Elevated { get; private set; }

      public bool CorruptGamma { get; init; }

      public static string Hash(string content) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

      public IFileDownloader Downloader => this;

      public IArchiveExtractor Extractor(string sevenZipExe) => this;

      public IReadOnlyList<string> ExistingDownloadFolders => [];

      public Task<IReadOnlyList<HostedFile>> ListMediafireFolderAsync(string folderKey, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<HostedFile>>([new HostedFile("GAMMA RC3.7z", Encoding.UTF8.GetByteCount("rc3"), "abc", Hash("rc3"))]);

      public Task<string> ResolveLinkAsync(string pageUrl, CancellationToken cancellationToken) =>
        Task.FromResult(pageUrl.Contains("mediafire") ? "https://download1.mediafire.com/abc/GAMMA RC3.7z" : "https://fmt1.dl.dbolical.com/Anomaly-1.5.3-Full.2.7z");

      public Task<ElevatedOutcome> RunElevatedAsync(ElevatedPlan plan, CancellationToken cancellationToken)
      {
        Elevated = true;
        return Task.FromResult(new ElevatedOutcome(false, new Dictionary<string, string>()));
      }

      public Task<Prerequisites> DetectAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new Prerequisites(VcRedist: true, DirectX: true, LongPaths: true, []));

      public Task<DownloadResult> DownloadAsync(DownloadRequest request, IProgress<TransferProgress>? progress, CancellationToken cancellationToken)
      {
        Downloads.Add(request);
        var content = request.Url switch
        {
          var u when u.Contains("7zr") => "7zr",
          var u when u.Contains("extra") => "7zextra",
          var u when u.Contains("mediafire") => CorruptGamma ? "rc!" : "rc3",
          _ => "anomaly",
        };
        File.WriteAllText(request.Destination, content);
        progress?.Report(new TransferProgress(content.Length, content.Length, null));
        return Task.FromResult(new DownloadResult(false, null));
      }

      public Task ExtractAsync(string archive, string destination, IProgress<double>? progress, CancellationToken cancellationToken)
      {
        var content = File.ReadAllText(archive);
        if (content == "7zextra")
        {
          Touch(destination, Path.Combine("x64", "7za.exe"));
        }
        else if (content == "rc3")
        {
          Touch(destination, @"GAMMA\ModOrganizer.exe");
          Touch(destination, @"GAMMA\.Grok's Modpack Installer\resources\7zip\7z.exe");
          Touch(destination, @"GAMMA\profiles\G.A.M.M.A\modlist.txt");
        }
        else
        {
          Touch(destination, @"Anomaly-1.5.3\AnomalyLauncher.exe");
          Touch(destination, @"Anomaly-1.5.3\bin\AnomalyDX11AVX.exe");
          Touch(destination, @"Anomaly-1.5.3\db\0.db0");
        }
        progress?.Report(1);
        return Task.CompletedTask;
      }

      private static void Touch(string root, string relative)
      {
        var path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "");
      }
    }
  }
}
