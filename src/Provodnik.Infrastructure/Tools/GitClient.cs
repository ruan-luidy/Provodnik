using Provodnik.Core.Diagnostics;
using Provodnik.Core.IO;
using Provodnik.Core.Repos;
using Provodnik.Core.Text;
using Provodnik.Infrastructure.Processes;

namespace Provodnik.Infrastructure.Tools
{
  /// <summary>
  /// O Git portátil do GAMMA (resources\cmd\git.exe; sem ele, o do PATH). O original rodava cada comando duas vezes
  /// pelo git-cmd e não olhava se deu certo. Aqui: clone raso (--depth 1, a primeira vez do gamma_large_files_v2
  /// baixa só a versão atual em vez do histórico inteiro), fetch + reset --hard + clean pra ficar igual ao remoto, nova
  /// tentativa quando a rede falha e erro de verdade quando não dá.
  /// </summary>
  public sealed class GitClient(Func<string> gitPath, IToolConsole console) : IGitClient
  {
    private static readonly IReadOnlyDictionary<string, string> Environment = new Dictionary<string, string>
    {
      ["GIT_TERMINAL_PROMPT"] = "0",
      ["GIT_ASKPASS"] = string.Empty,
    };

    public async Task SyncAsync(GitRepository repository, string directory, IProgress<string>? progress, CancellationToken cancellationToken)
    {
      if (!Directory.Exists(Path.Combine(directory, ".git")))
      {
        // pasta sem .git (cópia manual, clone que morreu no meio): não tem como atualizar, clona de novo
        FileTree.Delete(directory);
        Directory.CreateDirectory(Path.GetDirectoryName(directory)!);
        await RunAsync(Path.GetDirectoryName(directory)!, ["clone", "--progress", "--depth", "1", "--branch", repository.Branch, repository.Url, directory], progress, retries: 3, cancellationToken).ConfigureAwait(false);
        return;
      }

      await RunAsync(directory, ["remote", "set-url", "origin", repository.Url], progress, retries: 1, cancellationToken).ConfigureAwait(false);
      await RunAsync(directory, ["fetch", "--progress", "--depth", "1", "origin", repository.Branch], progress, retries: 3, cancellationToken).ConfigureAwait(false);
      await RunAsync(directory, ["checkout", "-f", "-B", repository.Branch, $"origin/{repository.Branch}"], progress, retries: 1, cancellationToken).ConfigureAwait(false);
      await RunAsync(directory, ["reset", "--hard", $"origin/{repository.Branch}"], progress, retries: 1, cancellationToken).ConfigureAwait(false);
      await RunAsync(directory, ["clean", "-fd"], progress, retries: 1, cancellationToken).ConfigureAwait(false);
    }

    private async Task RunAsync(string workingDirectory, string[] command, IProgress<string>? progress, int retries, CancellationToken cancellationToken)
    {
      var git = gitPath();
      var exe = File.Exists(git) ? git : "git";
      string[] arguments = ["-c", "safe.directory=*", "-c", "core.longpaths=true", "-c", "http.postBuffer=524288000", .. command];

      console.Write("git", $"$ git {string.Join(' ', command)}");
      for (var attempt = 1; ; attempt++)
      {
        var result = await ProcessRunner.RunAsync(
          exe,
          arguments,
          new ProcessOptions(WorkingDirectory: workingDirectory, OnOutput: Report, OnError: Report, Environment: Environment),
          cancellationToken).ConfigureAwait(false);
        if (result.ExitCode == 0)
          return;
        if (attempt >= retries)
          throw new GitException($"git {command[0]} falhou ({result.ExitCode}): {result.Tail()}");

        progress?.Report($"git {command[0]} falhou, tentando de novo ({attempt + 1}/{retries})...");
        await Task.Delay(TimeSpan.FromSeconds(3 * attempt), cancellationToken).ConfigureAwait(false);
      }

      void Report(string line)
      {
        progress?.Report(line);
        if (!ProgressLines.TryParseGit(line, out _, out _))
          console.Write("git", line);
      }
    }
  }
}
