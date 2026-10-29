using Microsoft.Win32;
using Provodnik.Infrastructure.Processes;

namespace Provodnik.Infrastructure.Desktop
{
  /// <summary>
  /// O que a wiki do GAMMA manda ter no Windows antes de instalar: o Visual C++ x64 (o registro que o próprio
  /// instalador da Microsoft escreve), o DirectX End-User Runtime (as DLLs do d3dx que o Windows não traz) e quais
  /// antivírus estão ligados (a Central de Segurança do Windows, a mesma lista da tela de Segurança).
  /// </summary>
  public static class WindowsPrerequisites
  {
    public static bool HasVcRedist()
    {
      try
      {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\X64");
        return key?.GetValue("Installed") is int installed && installed == 1;
      }
      catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
      {
        return false;
      }
    }

    /// <summary>O que o jogo carrega do pacote de junho de 2010 (64 bits).</summary>
    public static bool HasDirectX()
    {
      var system = Environment.GetFolderPath(Environment.SpecialFolder.System);
      return new[] { "d3dx9_43.dll", "d3dx11_43.dll", "d3dcompiler_43.dll", "xinput1_3.dll", "X3DAudio1_7.dll" }
        .All(dll => File.Exists(Path.Combine(system, dll)));
    }

    /// <summary>Os antivírus registrados no Windows, tirando o Defender (esse o Проводник configura sozinho).</summary>
    public static async Task<IReadOnlyList<string>> OtherAntivirusAsync(CancellationToken cancellationToken)
    {
      try
      {
        var result = await ProcessRunner.RunAsync(
          "powershell.exe",
          ["-NoProfile", "-NonInteractive", "-Command", "Get-CimInstance -Namespace root/SecurityCenter2 -ClassName AntiVirusProduct | ForEach-Object { $_.displayName }"],
          new ProcessOptions(Timeout: TimeSpan.FromSeconds(20)),
          cancellationToken).ConfigureAwait(false);
        return result.Output
          .Select(l => l.Trim())
          .Where(l => l.Length > 0 && !l.Contains("Defender", StringComparison.OrdinalIgnoreCase))
          .Distinct(StringComparer.OrdinalIgnoreCase)
          .ToList();
      }
      catch (Exception ex) when (ex is TimeoutException or InvalidOperationException or System.ComponentModel.Win32Exception)
      {
        return [];
      }
    }
  }
}
