using System.Collections.Concurrent;
using System.IO;
using Provodnik.Core.Archives;
using Provodnik.Core.Desktop;
using Provodnik.Core.Diagnostics;
using Provodnik.Core.Downloads;
using Provodnik.Core.Remote;
using Provodnik.Core.Repos;
using Provodnik.Core.Text;

namespace Provodnik.Wpf.Features.Demo
{
  /// <summary>
  /// As ferramentas de mentira da simulação. O tempo corre ~10x mais rápido que o de verdade, mas com o mesmo
  /// jeitão: links do ModDB que às vezes levam bloqueio, downloads que caem e voltam, uns poucos que falham de vez,
  /// arquivo corrompido na extração. Quem falha é sempre o mesmo (sorteio pelo nome), pra dar pra testar o "tentar de
  /// novo" e ver a mesma tela duas vezes.
  /// </summary>
  public static class DemoTools
  {
    public sealed class Api(DemoWorld world) : IGammaApi
    {
      public async Task<string> GetModListAsync(string key, CancellationToken cancellationToken)
      {
        await Task.Delay(600, cancellationToken);
        return world.ListText;
      }

      public async Task<string> GetDefinitionVersionAsync(CancellationToken cancellationToken)
      {
        await Task.Delay(300, cancellationToken);
        return "920";
      }
    }

    public sealed class Git(DemoWorld world, IToolConsole console) : IGitClient
    {
      public async Task SyncAsync(GitRepository repository, string directory, IProgress<string>? progress, CancellationToken cancellationToken)
      {
        var first = !Directory.Exists(Path.Combine(directory, ".git"));
        console.Write("git", first ? $"$ git clone --progress --depth 1 --branch main {repository.Url}" : "$ git fetch --progress --depth 1 origin main");
        foreach (var phase in new[] { "Counting objects", "Receiving objects", "Resolving deltas", "Updating files" })
        {
          for (var percent = 0; percent <= 100; percent += 20)
          {
            progress?.Report($"{phase}: {percent,3}% ({percent * 12}/1200)");
            await Task.Delay(first ? 90 : 25, cancellationToken);
          }
        }
        console.Write("git", $"HEAD is now at {DemoWorld.Dice(repository.Name):x7} demo");

        Directory.CreateDirectory(Path.Combine(directory, ".git"));
        if (repository == GitRepository.Definition)
        {
          var definition = Path.Combine(directory, "G.A.M.M.A");
          Write(Path.Combine(definition, "modpack_data", "modlist.txt"), world.ModOrganizerList());
          Write(Path.Combine(definition, "modpack_data", "modpack_icon.ico"), string.Empty);
          Write(Path.Combine(definition, "modpack_patches", "bin", "demo.txt"), "patch");
          Write(Path.Combine(directory, "G.A.M.M.A_definition_version.txt"), "920");
        }
        else if (repository == GitRepository.LargeFiles)
        {
          Write(Path.Combine(directory, "G.A.M.M.A. Large Files", "gamedata", "demo.txt"), "large");
        }
        else
        {
          Write(Path.Combine(directory, "AEK", "gamedata", "demo.txt"), "aek");
        }
      }
    }

    public sealed class Resolver(IToolConsole console) : IModDbResolver
    {
      private readonly ConcurrentDictionary<string, int> _calls = new();

      public async Task<string> ResolveAsync(string addonUrl, CancellationToken cancellationToken)
      {
        var call = _calls.AddOrUpdate(addonUrl, 1, (_, n) => n + 1);
        console.Write("moddb", L.T("install.moddb.requesting", addonUrl));
        await Task.Delay(150 + DemoWorld.Dice(addonUrl) % 450, cancellationToken);

        // um em ~45 leva o bloqueio do ModDB na primeira vez (a fila inteira esfria)
        if (call == 1 && DemoWorld.Dice(addonUrl, 1) % 45 == 0)
          throw new DownloadException(L.T("install.moddb.blockedIp"));

        var link = $"https://fmt{1 + DemoWorld.Dice(addonUrl, 2) % 6}.dl.dbolical.com/dl/demo/{addonUrl.Split('/')[^1]}?st=demo";
        console.Write("moddb", $"link: {link}");
        return link;
      }
    }

    public sealed class Downloader(IToolConsole console) : IFileDownloader
    {
      private readonly ConcurrentDictionary<string, int> _calls = new();

      public async Task<DownloadResult> DownloadAsync(DownloadRequest request, IProgress<TransferProgress>? progress, CancellationToken cancellationToken)
      {
        var archive = Path.GetFileName(request.Destination).Replace(".part", string.Empty);
        var call = _calls.AddOrUpdate(archive, 1, (_, n) => n + 1);
        var dice = DemoWorld.Dice(archive);
        var size = (long)(2 + dice % 900) * 1024 * 1024 / (dice % 7 == 0 ? 1 : 3);
        var speed = (6 + DemoWorld.Dice(archive, 3) % 22) * 1024d * 1024;
        var tool = request.Impersonate ? "curl" : "http";
        console.Write(tool, L.T("install.demo.downloading", request.Url));

        // um em ~31 cai no meio da primeira vez e volta na segunda; um em ~110 falha sempre (404)
        var dropsAt = call == 1 && DemoWorld.Dice(archive, 4) % 31 == 0 ? size / 2 : long.MaxValue;
        var broken = DemoWorld.Dice(archive, 5) % 110 == 0;

        long received = 0;
        while (received < size)
        {
          await Task.Delay(200, cancellationToken);
          received = Math.Min(size, received + (long)(speed * 0.2 * 10));
          progress?.Report(new TransferProgress(received, size, speed * (0.85 + DemoWorld.Dice(archive, (int)(received % 97)) % 30 / 100d)));
          if (broken && received > size / 5)
          {
            console.Write(tool, "curl: (22) The requested URL returned error: 404");
            throw new DownloadException(L.T("install.demo.http404"));
          }
          if (received >= dropsAt)
          {
            console.Write(tool, "curl: (56) Recv failure: Connection was reset");
            throw new DownloadException(L.T("install.demo.connectionReset"));
          }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(request.Destination)!);
        await File.WriteAllTextAsync(request.Destination, DemoWorld.ContentOf(archive), cancellationToken);
        return new DownloadResult(false, request.Impersonate ? null : $"\"demo-{dice:x}\"");
      }
    }

    public sealed class Extractor(DemoWorld world, IToolConsole console) : IArchiveExtractor
    {
      public async Task ExtractAsync(string archive, string destination, IProgress<double>? progress, CancellationToken cancellationToken)
      {
        var name = Path.GetFileName(archive);
        console.Write("7z", L.T("install.demo.extracting", name));
        var steps = 3 + DemoWorld.Dice(name, 6) % 10;
        for (var step = 1; step <= steps; step++)
        {
          await Task.Delay(180, cancellationToken);
          progress?.Report((double)step / steps);
          // um em ~90 está corrompido
          if (step == steps / 2 && DemoWorld.Dice(name, 7) % 90 == 0)
          {
            console.Write("7z", $"ERROR: Headers Error : {archive}");
            throw new ArchiveException(L.T("install.demo.headersError"));
          }
        }

        Write(Path.Combine(destination, "gamedata", $"{Path.GetFileNameWithoutExtension(name)}.ltx"), name);
        foreach (var folder in world.Folders.GetValueOrDefault(name) ?? [])
          Write(Path.Combine(destination, folder.Replace('/', '\\').Trim('\\'), "gamedata", "demo.ltx"), name);
      }
    }

    public sealed class Shortcuts : IShortcutWriter
    {
      public void Create(string shortcutPath, string target, string? icon = null, string? workingDirectory = null, string? arguments = null)
      {
      }
    }

    private static void Write(string path, string content)
    {
      Directory.CreateDirectory(Path.GetDirectoryName(path)!);
      File.WriteAllText(path, content);
    }
  }
}
