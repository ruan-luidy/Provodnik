using System.Diagnostics;
using Provodnik.Core.Desktop;
using Provodnik.Core.Layout;

namespace Provodnik.Features.Launch
{
  /// <summary>
  /// Abrir o jogo: pelo MO2, que monta a pasta virtual com os mods. O botão Play do original só abria o MO2; aqui dá
  /// pra ir direto num executável dele ("moshortcut://:Anomaly (DX11-AVX)") e o MO2 abre o jogo sozinho.
  /// </summary>
  public static class GameLauncher
  {
    public static bool IsModOrganizerRunning() => Process.GetProcessesByName("ModOrganizer").Length > 0;

    public static bool IsGameRunning() =>
      Process.GetProcesses().Any(p => p.ProcessName.StartsWith("AnomalyDX", StringComparison.OrdinalIgnoreCase));

    /// <param name="unelevated">
    /// Com o Проводник rodando como administrador: abre por um atalho pelo Explorer, que não herda o administrador.
    /// MO2 e jogo elevados dão dor de cabeça (arrastar arquivo, overlay do Steam/Discord, saves).
    /// </param>
    public static void OpenModOrganizer(GammaLayout layout, IShortcutWriter? unelevated = null) => Start(layout, null, unelevated);

    public static void Play(GammaLayout layout, string? executableTitle, IShortcutWriter? unelevated = null) =>
      Start(layout, string.IsNullOrWhiteSpace(executableTitle) ? null : $"\"moshortcut://:{executableTitle}\"", unelevated);

    private static void Start(GammaLayout layout, string? arguments, IShortcutWriter? unelevated)
    {
      if (!File.Exists(layout.ModOrganizerExe))
        throw new FileNotFoundException($"ModOrganizer.exe não encontrado em {layout.Root}");

      if (unelevated is not null)
      {
        Directory.CreateDirectory(layout.DataDir);
        var shortcut = Path.Combine(layout.DataDir, "abrir-mo2.lnk");
        unelevated.Create(shortcut, layout.ModOrganizerExe, workingDirectory: layout.Root, arguments: arguments);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{shortcut}\"") { UseShellExecute = false })?.Dispose();
        return;
      }

      var info = new ProcessStartInfo(layout.ModOrganizerExe, arguments ?? string.Empty) { UseShellExecute = false, WorkingDirectory = layout.Root };
      Process.Start(info)?.Dispose();
    }
  }
}
