using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Win32;
using Provodnik.Core.Archives;
using Provodnik.Core.Bootstrap;
using Provodnik.Core.Desktop;
using Provodnik.Core.Diagnostics;
using Provodnik.Core.Downloads;
using Provodnik.Features.Bootstrap;
using Provodnik.Infrastructure.Desktop;
using Provodnik.Infrastructure.Http;
using Provodnik.Infrastructure.Tools;

namespace Provodnik.Wpf.Features.Bootstrap
{
  /// <summary>
  /// A instalação do zero ligada nas ferramentas de verdade. O curl-impersonate e o 7z.exe são os que o RC3 traz, lidos
  /// da pasta do GAMMA escolhida (<see cref="Target"/>) na hora de usar; antes disso, só o HttpClient e o 7zr.exe.
  /// </summary>
  public sealed class BootstrapServices(
    HttpDownloader http,
    MediafireApi mediafire,
    IModDbResolver browser,
    ISystemInfo system,
    IToolConsole console) : IBootstrapServices
  {
    public BootstrapTarget? Target { get; set; }

    public IFileDownloader Downloader => new FileDownloader(
      new CurlDownloader(() =>
      {
        var layout = (Target ?? throw new InvalidOperationException("sem pasta")).Layout;
        return new CurlPaths(layout.Curl, layout.CurlConfig, layout.CurlHeader);
      }, console),
      http);

    public IArchiveExtractor Extractor(string sevenZipExe) => new SevenZipExtractor(() => sevenZipExe, console);

    public async Task<IReadOnlyList<HostedFile>> ListMediafireFolderAsync(string folderKey, CancellationToken cancellationToken) =>
      (await mediafire.ListFolderAsync(folderKey, cancellationToken).ConfigureAwait(false))
        .Select(f => new HostedFile(f.Name, f.Size, f.QuickKey, f.Sha256))
        .ToList();

    public Task<string> ResolveLinkAsync(string pageUrl, CancellationToken cancellationToken) => browser.ResolveAsync(pageUrl, cancellationToken);

    public async Task<Prerequisites> DetectAsync(CancellationToken cancellationToken)
    {
      var antivirus = await WindowsPrerequisites.OtherAntivirusAsync(cancellationToken).ConfigureAwait(false);
      return new Prerequisites(WindowsPrerequisites.HasVcRedist(), WindowsPrerequisites.HasDirectX(), system.LongPathsEnabled, antivirus);
    }

    /// <summary>
    /// O script vai pro lado do arquivo de resultado e roda no PowerShell do Windows como administrador (o UAC
    /// pergunta uma vez). UTF-8 com BOM: sem ele o PowerShell 5 lê o arquivo como ANSI e estraga caminho com acento.
    /// </summary>
    public async Task<ElevatedOutcome> RunElevatedAsync(ElevatedPlan plan, CancellationToken cancellationToken)
    {
      var script = Path.ChangeExtension(plan.ResultFile, ".ps1");
      await File.WriteAllTextAsync(script, ElevatedSetup.Script(plan), new UTF8Encoding(true), cancellationToken).ConfigureAwait(false);
      console.Write("provodnik", $"powershell (administrador): {script}");
      try
      {
        using var process = Process.Start(new ProcessStartInfo(
          "powershell.exe",
          $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{script}\"")
        {
          Verb = "runas",
          UseShellExecute = true,
          WindowStyle = ProcessWindowStyle.Hidden,
        });
        if (process is null)
          return new ElevatedOutcome(true, new Dictionary<string, string>());
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
      }
      catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
      {
        // cancelou o UAC
        return new ElevatedOutcome(true, new Dictionary<string, string>());
      }

      var results = File.Exists(plan.ResultFile)
        ? ElevatedSetup.ParseResults(await File.ReadAllTextAsync(plan.ResultFile, cancellationToken).ConfigureAwait(false))
        : new Dictionary<string, string>();
      foreach (var (step, result) in results)
        console.Write("provodnik", $"{step}: {result}");
      return new ElevatedOutcome(false, results);
    }

    public IReadOnlyList<string> ExistingDownloadFolders { get; } = [DownloadsFolder()];

    /// <summary>A pasta Downloads do Windows, mesmo quando a pessoa mudou ela de lugar.</summary>
    private static string DownloadsFolder()
    {
      try
      {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders");
        if (key?.GetValue("{374DE290-123F-4565-9164-39C4925E467B}") is string path && path.Length > 0)
          return Environment.ExpandEnvironmentVariables(path);
      }
      catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
      {
      }
      return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }
  }
}
