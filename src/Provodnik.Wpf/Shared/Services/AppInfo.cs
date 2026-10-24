using System.IO;
using System.Reflection;

namespace Provodnik.Wpf.Shared.Services
{
  /// <summary>Versão, data do build e novidades, tudo gravado no próprio exe.</summary>
  public static class AppInfo
  {
    private static readonly Assembly Assembly = typeof(AppInfo).Assembly;

    public static string Version { get; } =
      (Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0").Split('+')[0];

    /// <summary>Versão de teste ("0.2.0-teste.1"): a barra de título mostra TESTE.</summary>
    public static bool IsTest => Version.Contains('-');

    public static DateOnly? BuildDate { get; } =
      Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "BuildDate")?.Value is { } text
      && DateOnly.TryParse(text, out var date)
        ? date
        : null;

    /// <summary>O docs/CHANGELOG.md inteiro (vai embutido no exe pelo csproj).</summary>
    public static string Changelog { get; } = LoadChangelog();

    private static string LoadChangelog()
    {
      using var stream = Assembly.GetManifestResourceStream("Provodnik.CHANGELOG.md");
      if (stream is null)
        return string.Empty;

      using var reader = new StreamReader(stream);
      return reader.ReadToEnd();
    }
  }
}
