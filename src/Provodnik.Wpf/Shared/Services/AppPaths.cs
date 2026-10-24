using System.IO;

namespace Provodnik.Wpf.Shared.Services
{
  /// <summary>
  /// Onde o Проводник guarda o que é de quem usa: %LocalAppData%\Provodnik (configurações e errors.log). O que é da
  /// instalação do GAMMA (estado, cache de md5, logs de instalação) fica dentro da própria pasta do GAMMA, em
  /// .Grok's Modpack Installer\.provodnik, pra ir junto se a pasta mudar de lugar.
  /// </summary>
  public static class AppPaths
  {
    public static string Root { get; } = CreateRoot();

    public static string Settings => Path.Combine(Root, "settings.json");

    /// <summary>Erros inesperados, com a exceção inteira (a caixa de erro mostra só o resumo).</summary>
    public static string ErrorLog => Path.Combine(Root, "errors.log");

    private static string CreateRoot()
    {
      var root = Environment.GetEnvironmentVariable("PROVODNIK_DATA_DIR") is { Length: > 0 } custom
        ? custom
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Provodnik");
      Directory.CreateDirectory(root);
      return root;
    }
  }
}
