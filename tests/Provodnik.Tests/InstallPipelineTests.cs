using Provodnik.Core.Archives;
using Provodnik.Core.Desktop;
using Provodnik.Core.Downloads;
using Provodnik.Core.Install;
using Provodnik.Core.Layout;
using Provodnik.Core.Remote;
using Provodnik.Core.Repos;
using Provodnik.Features.Install;

namespace Provodnik.Tests
{
  /// <summary>
  /// O pipeline inteiro numa pasta GAMMA de mentira, com ferramentas falsas: o "zip" é um texto e extrair é escrever
  /// esse texto em pastas. Serve pra provar o continuar-de-onde-parou e a atualização da lista.
  /// </summary>
  public sealed class InstallPipelineTests : IDisposable
  {
    private readonly string _root = Path.Combine(Path.GetTempPath(), "provodnik-pipeline", Guid.NewGuid().ToString("N"));
    private readonly GammaLayout _layout;
    private readonly FakeApi _api = new();
    private readonly FakeExtractor _extractor = new();
    private readonly FakeDownloader _downloader = new();

    public InstallPipelineTests()
    {
      _layout = new GammaLayout(_root);
      Directory.CreateDirectory(_layout.Installer);
      File.WriteAllText(_layout.ModOrganizerExe, string.Empty);
      foreach (var tool in new[] { _layout.SevenZip, _layout.Curl, _layout.CurlConfig, _layout.ModDbCli })
      {
        Directory.CreateDirectory(Path.GetDirectoryName(tool)!);
        File.WriteAllText(tool, string.Empty);
      }
      var anomaly = Path.Combine(_root, "Anomaly");
      Directory.CreateDirectory(Path.Combine(anomaly, "bin"));
      File.WriteAllText(_layout.ModOrganizerIni, $"[General]\ngamePath=@ByteArray({anomaly.Replace(@"\", @"\\")})\n");
    }

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

    private InstallPipeline Pipeline() => new(_api, new FakeGit(), _downloader, new FakeResolver(), _extractor, new NoShortcuts());

    private Task<InstallReport> RunAsync(InstallOptions? options = null) =>
      Pipeline().RunAsync(_layout, options ?? new InstallOptions { CreateShortcuts = false }, new NullReporter(), CancellationToken.None);

    private static string Line(string url, string instructions, string author, string name, string archive) =>
      $"{url}\t{instructions}\t - {author}\t {name}\thttps://www.moddb.com/mods/x\t{archive}\t";

    private bool Has(string relative) => File.Exists(Path.Combine(_layout.Mods, relative));

    [Fact]
    public async Task Segunda_rodada_nao_reextrai_nada()
    {
      _api.List = string.Join('\n', " Audio", Line("https://www.moddb.com/addons/start/1", "0", "Ana", "Som", "som.zip"), Line("https://www.moddb.com/addons/start/2", "pack", "Bia", "Luz", "luz.7z"));

      var first = await RunAsync();
      var second = await RunAsync();

      Assert.True(first.Succeeded, string.Join("; ", first.Failures.Select(f => f.Reason)));
      Assert.Equal(2, first.Installed);
      Assert.True(Has(@"2- Som - Ana\gamedata\file.txt"));
      Assert.True(Has(@"3- Luz - Bia\gamedata\file.txt"));
      Assert.True(Has(@"1- Audio_separator\meta.ini"));
      Assert.Equal(0, second.Installed);
      Assert.Equal(2, second.UpToDate);
      Assert.Equal(2, _extractor.Calls);
    }

    [Fact]
    public async Task Instrucao_nova_com_o_mesmo_arquivo_reextrai()
    {
      _api.List = Line("https://www.moddb.com/addons/start/2", "pack", "Bia", "Luz", "luz.7z");
      await RunAsync();

      _api.List = Line("https://www.moddb.com/addons/start/2", "pack\\opt", "Bia", "Luz", "luz.7z");
      var update = await RunAsync();

      Assert.Equal(1, update.Installed);
      Assert.True(Has(@"1- Luz - Bia\gamedata\optional.txt"));
    }

    [Fact]
    public async Task Mod_que_mudou_de_numero_so_troca_o_nome_da_pasta()
    {
      _api.List = string.Join('\n', Line("https://www.moddb.com/addons/start/1", "0", "Ana", "Som", "som.zip"), Line("https://www.moddb.com/addons/start/9", "0", "Caio", "Velho", "velho.zip"));
      await RunAsync();

      // a atualização põe um separador na frente (todo mundo anda um número) e tira o "Velho"
      _api.List = string.Join('\n', " Audio", Line("https://www.moddb.com/addons/start/1", "0", "Ana", "Som", "som.zip"));
      var update = await RunAsync();

      Assert.Equal(0, update.Installed);
      Assert.Equal(1, update.UpToDate);
      Assert.True(Has(@"2- Som - Ana\gamedata\file.txt"));
      Assert.False(Directory.Exists(Path.Combine(_layout.Mods, "1- Som - Ana")));
      Assert.False(Directory.Exists(Path.Combine(_layout.Mods, "2- Velho - Caio")));
      Assert.Equal(2, _extractor.Calls);
    }

    [Fact]
    public async Task Download_que_falha_nao_estraga_os_outros_e_entra_no_relatorio()
    {
      _api.List = string.Join('\n', Line("https://www.moddb.com/addons/start/1", "0", "Ana", "Som", "som.zip"), Line("https://www.moddb.com/addons/start/666", "0", "Dan", "Quebrado", "quebrado.zip"));
      _downloader.Broken.Add("666");

      var report = await RunAsync(new InstallOptions { CreateShortcuts = false, Attempts = 1 });

      Assert.False(report.Succeeded);
      var failure = Assert.Single(report.Failures);
      Assert.Equal(2, failure.Index);
      Assert.True(Has(@"1- Som - Ana\gamedata\file.txt"));
      // sem pasta pela metade nem meta.ini mentindo que instalou
      Assert.False(Directory.Exists(Path.Combine(_layout.Mods, "2- Quebrado - Dan")));

      // a internet voltou: tentar de novo só a falha
      _downloader.Broken.Clear();
      var retry = await RunAsync(new InstallOptions { CreateShortcuts = false, OnlyMods = new HashSet<int> { 2 }, SkipPreparation = true });

      Assert.True(retry.Succeeded);
      Assert.Equal(1, retry.Installed);
      Assert.True(Has(@"2- Quebrado - Dan\gamedata\file.txt"));
    }

    [Fact]
    public async Task Arquivo_usado_por_varios_mods_e_baixado_uma_vez()
    {
      const string url = "https://github.com/ahuyn/anomaly-compilation/archive/refs/tags/latest.zip";
      _api.List = string.Join('\n', $"{url}\tpack\t - arti\t Ammo", $"{url}\tpack\\opt\t - arti\t Ammo Patch");

      var report = await RunAsync();

      Assert.Equal(2, report.Installed);
      Assert.Equal(1, _downloader.Calls);
    }

    // ===== Falsos =====

    private sealed class FakeApi : IGammaApi
    {
      public string List { get; set; } = string.Empty;

      public Task<string> GetModListAsync(string key, CancellationToken cancellationToken) => Task.FromResult(List);

      public Task<string> GetDefinitionVersionAsync(CancellationToken cancellationToken) => Task.FromResult("920");
    }

    /// <summary>"Clona" escrevendo a definição mínima do GAMMA na pasta do repositório.</summary>
    private sealed class FakeGit : IGitClient
    {
      public Task SyncAsync(GitRepository repository, string directory, IProgress<string>? progress, CancellationToken cancellationToken)
      {
        Directory.CreateDirectory(Path.Combine(directory, ".git"));
        if (repository == GitRepository.Definition)
        {
          var data = Path.Combine(directory, "G.A.M.M.A", "modpack_data");
          Directory.CreateDirectory(data);
          File.WriteAllText(Path.Combine(data, "modlist.txt"), "# modlist\n");
          File.WriteAllText(Path.Combine(directory, "G.A.M.M.A_definition_version.txt"), "920");
        }
        return Task.CompletedTask;
      }
    }

    private sealed class FakeResolver : IModDbResolver
    {
      public Task<string> ResolveAsync(string addonUrl, CancellationToken cancellationToken) => Task.FromResult(addonUrl + "?mirror");
    }

    /// <summary>O "arquivo" baixado é o próprio link (o md5 muda quando o link muda).</summary>
    private sealed class FakeDownloader : IFileDownloader
    {
      private int _calls;

      public HashSet<string> Broken { get; } = [];

      public int Calls => _calls;

      public Task<DownloadResult> DownloadAsync(DownloadRequest request, IProgress<TransferProgress>? progress, CancellationToken cancellationToken)
      {
        Interlocked.Increment(ref _calls);
        if (Broken.Any(request.Url.Contains))
          throw new DownloadException("conexão caiu");
        Directory.CreateDirectory(Path.GetDirectoryName(request.Destination)!);
        File.WriteAllText(request.Destination, request.Url);
        return Task.FromResult(new DownloadResult(false, null));
      }
    }

    /// <summary>Todo "arquivo" vira gamedata\file.txt na raiz e pack\gamedata, pack\opt\optional.txt.</summary>
    private sealed class FakeExtractor : IArchiveExtractor
    {
      private int _calls;

      public int Calls => _calls;

      public Task ExtractAsync(string archive, string destination, IProgress<double>? progress, CancellationToken cancellationToken)
      {
        Interlocked.Increment(ref _calls);
        void Write(string relative)
        {
          var path = Path.Combine(destination, relative);
          Directory.CreateDirectory(Path.GetDirectoryName(path)!);
          File.WriteAllText(path, archive);
        }
        Write(@"gamedata\file.txt");
        Write(@"pack\gamedata\file.txt");
        Write(@"pack\opt\optional.txt");
        return Task.CompletedTask;
      }
    }

    private sealed class NoShortcuts : IShortcutWriter
    {
      public void Create(string shortcutPath, string target, string? icon = null, string? workingDirectory = null)
      {
      }
    }

    private sealed class NullReporter : IInstallReporter
    {
      public void Stage(InstallStage stage, StageState state, string? detail = null)
      {
      }

      public void Mod(ModProgress progress)
      {
      }

      public void Log(LogLevel level, string message)
      {
      }
    }
  }
}
