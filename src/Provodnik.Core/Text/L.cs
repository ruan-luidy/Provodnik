using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace Provodnik.Core.Text
{
  /// <summary>
  /// Os textos do app em português, inglês e russo. Cada projeto embute os seus catálogos (Localization\*.pt.json,
  /// *.en.json, *.ru.json: chave → texto, com {0} pros valores) e se registra aqui; quem mostra texto pede pela chave.
  /// O português é a base: chave que falta no idioma escolhido sai em português, e chave que não existe sai ela mesma
  /// (dá pra ver na tela o que esqueceu de traduzir). O idioma é escolhido uma vez, na partida: trocar reabre o app.
  /// </summary>
  public static class L
  {
    public const string Portuguese = "pt";
    public const string English = "en";
    public const string Russian = "ru";

    public static IReadOnlyList<string> Languages { get; } = [Portuguese, English, Russian];

    private static readonly List<Assembly> Sources = [];
    private static readonly Lock Gate = new();
    private static Dictionary<string, string> _base = [];
    private static Dictionary<string, string> _current = [];

    // os Provodnik.* se registram sozinhos ao carregar (e os que já estão carregados agora): ninguém esquece um projeto
    static L()
    {
      AppDomain.CurrentDomain.AssemblyLoad += (_, e) => RegisterIfOurs(e.LoadedAssembly);
      foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        RegisterIfOurs(assembly);
    }

    public static string Language { get; private set; } = Portuguese;

    /// <summary>
    /// O idioma do Windows, lido uma vez antes de o app trocar a cultura da thread pela do idioma escolhido (depois
    /// disso a CurrentUICulture já é a do app). É o que vale no "Automático".
    /// </summary>
    public static string SystemLanguage { get; } = FromSystem(CultureInfo.CurrentUICulture);

    private static void RegisterIfOurs(Assembly assembly)
    {
      if (assembly.GetName().Name?.StartsWith("Provodnik", StringComparison.Ordinal) == true && !assembly.IsDynamic)
        Register(assembly);
    }

    /// <summary>A cultura do idioma (números, datas): pt-BR, en-US, ru-RU.</summary>
    public static CultureInfo Culture => CultureOf(Language);

    /// <summary>Junta os catálogos embutidos neste assembly (os que terminam em .pt.json, .en.json, .ru.json).</summary>
    public static void Register(Assembly assembly)
    {
      lock (Gate)
      {
        if (Sources.Contains(assembly))
          return;
        Sources.Add(assembly);
        Load();
      }
    }

    /// <summary>Escolhe o idioma (um de <see cref="Languages"/>; outro vira inglês).</summary>
    public static void Use(string language)
    {
      lock (Gate)
      {
        Language = Languages.Contains(language) ? language : English;
        Load();
      }
    }

    /// <summary>O idioma do Windows: português e russo/ucraniano/bielorrusso pelo nome, o resto em inglês.</summary>
    public static string FromSystem(CultureInfo culture) => culture.TwoLetterISOLanguageName switch
    {
      "pt" => Portuguese,
      "ru" or "uk" or "be" => Russian,
      _ => English,
    };

    public static string T(string key) =>
      _current.TryGetValue(key, out var text) || _base.TryGetValue(key, out text) ? text : key;

    public static string T(string key, params object?[] args) => string.Format(Culture, T(key), args);

    /// <summary>Tem texto pra essa chave em algum catálogo.</summary>
    public static bool Has(string key) => _current.ContainsKey(key) || _base.ContainsKey(key);

    /// <summary>
    /// O texto da chave em todos os idiomas: pra reconhecer, num log gravado em outro idioma, uma frase que o app
    /// escreve (o "Pronto em ..." do fim da instalação, que o Registro lê).
    /// </summary>
    public static IReadOnlyList<string> Variants(string key)
    {
      lock (Gate)
        return Languages.Select(Read).Select(c => c.GetValueOrDefault(key)).OfType<string>().Distinct().ToList();
    }

    private static CultureInfo CultureOf(string language) => language switch
    {
      Portuguese => CultureInfo.GetCultureInfo("pt-BR"),
      Russian => CultureInfo.GetCultureInfo("ru-RU"),
      _ => CultureInfo.GetCultureInfo("en-US"),
    };

    private static void Load()
    {
      _base = Read(Portuguese);
      _current = Language == Portuguese ? _base : Read(Language);
    }

    private static Dictionary<string, string> Read(string language)
    {
      var texts = new Dictionary<string, string>(StringComparer.Ordinal);
      var suffix = $".{language}.json";
      foreach (var assembly in Sources)
      {
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)))
        {
          using var stream = assembly.GetManifestResourceStream(name);
          if (stream is null)
            continue;
          var catalog = JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? [];
          foreach (var (key, text) in catalog)
            texts[key] = text;
        }
      }
      return texts;
    }
  }
}
