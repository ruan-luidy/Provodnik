using System.Diagnostics;
using Provodnik.Core.Desktop;
using Provodnik.Core.Layout;
using Provodnik.Core.Text;

namespace Provodnik.Features.Launch
{
  /// <summary>
  /// Abrir o jogo: pelo MO2, que monta a pasta virtual com os mods. O botão Play do original só abria o MO2; aqui dá
  /// pra ir direto num executável dele ("moshortcut://:Anomaly (DX11-AVX)") e o MO2 abre o jogo sozinho, no perfil
  /// escolhido ("-p", o "use the given profile" da linha de comando do MO2).
  /// </summary>
  public static class GameLauncher
  {
    /// <summary>Os perfis do MO2 (as pastas em profiles\), o do modpack primeiro.</summary>
    public static IReadOnlyList<string> Profiles(GammaLayout layout, string modpackProfile)
    {
      if (!Directory.Exists(layout.Profiles))
        return [];
      return Directory.GetDirectories(layout.Profiles)
        .Select(Path.GetFileName)
        .OfType<string>()
        .OrderBy(name => !name.Equals(modpackProfile, StringComparison.OrdinalIgnoreCase))
        .ThenBy(name => name, StringComparer.CurrentCultureIgnoreCase)
        .ToList();
    }

    /// <param name="unelevated">
    /// Com o Проводник rodando como administrador: abre por um atalho pelo Explorer, que não herda o administrador.
    /// MO2 e jogo elevados dão dor de cabeça (arrastar arquivo, overlay do Steam/Discord, saves).
    /// </param>
    public static void OpenModOrganizer(GammaLayout layout, string? profile = null, IShortcutWriter? unelevated = null) =>
      Start(layout, Arguments(Existing(layout, profile), null), unelevated);

    public static void Play(GammaLayout layout, string? executableTitle, string? profile = null, IShortcutWriter? unelevated = null) =>
      Start(layout, Arguments(Existing(layout, profile), string.IsNullOrWhiteSpace(executableTitle) ? null : $"\"moshortcut://:{executableTitle}\""), unelevated);

    // perfil apagado ou renomeado no MO2: abre no último que ele usou, em vez de parar no aviso de perfil inexistente
    private static string? Existing(GammaLayout layout, string? profile) =>
      !string.IsNullOrWhiteSpace(profile) && profile == Path.GetFileName(profile) && Directory.Exists(Path.Combine(layout.Profiles, profile)) ? profile : null;

    public static string? Arguments(string? profile, string? shortcut)
    {
      var parts = new List<string>(2);
      if (!string.IsNullOrWhiteSpace(profile))
        parts.Add($"-p \"{profile}\"");
      if (shortcut is not null)
        parts.Add(shortcut);
      return parts.Count == 0 ? null : string.Join(' ', parts);
    }

    private static void Start(GammaLayout layout, string? arguments, IShortcutWriter? unelevated)
    {
      if (!File.Exists(layout.ModOrganizerExe))
        throw new FileNotFoundException(L.T("features.launch.noModOrganizer", layout.Root));

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
