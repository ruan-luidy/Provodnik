using System.Diagnostics;
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

    public static void OpenModOrganizer(GammaLayout layout) => Start(layout, []);

    public static void Play(GammaLayout layout, string? executableTitle)
    {
      if (string.IsNullOrWhiteSpace(executableTitle))
      {
        OpenModOrganizer(layout);
        return;
      }
      Start(layout, [$"moshortcut://:{executableTitle}"]);
    }

    private static void Start(GammaLayout layout, string[] arguments)
    {
      if (!File.Exists(layout.ModOrganizerExe))
        throw new FileNotFoundException($"ModOrganizer.exe não encontrado em {layout.Root}");

      var info = new ProcessStartInfo(layout.ModOrganizerExe) { UseShellExecute = false, WorkingDirectory = layout.Root };
      foreach (var argument in arguments)
        info.ArgumentList.Add(argument);
      Process.Start(info)?.Dispose();
    }
  }
}
