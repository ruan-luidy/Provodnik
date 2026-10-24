namespace Provodnik.Core.Desktop
{
  /// <summary>O que a checagem antes de instalar precisa saber do Windows.</summary>
  public interface ISystemInfo
  {
    /// <summary>O Проводник está rodando como administrador.</summary>
    bool IsElevated { get; }

    /// <summary>LongPathsEnabled no registro (nulo: não deu pra ler).</summary>
    bool? LongPathsEnabled { get; }

    /// <summary>A versão do WebView2 Runtime (o navegador que passa pelo Cloudflare do ModDB); nulo: não instalado.</summary>
    string? WebView2Version { get; }
  }
}
