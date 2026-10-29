namespace Provodnik.Core.Bootstrap
{
  /// <summary>
  /// De onde vem cada peça da instalação do zero, com o hash pra conferir. Os links e os SHA-256 do Anomaly e do RC3
  /// são os da página "Installing GAMMA" da wiki do GAMMA; o RC3 já foi trocado no Mediafire depois da wiki, então o
  /// hash dele vem da própria API do Mediafire (o campo hash é o SHA-256 do arquivo).
  /// </summary>
  public static class BootstrapSources
  {
    public const string AnomalyArchive = "Anomaly-1.5.3-Full.2.7z";

    /// <summary>A página de start do ModDB (o mesmo caminho dos addons: Cloudflare, espelho, CDN).</summary>
    public const string AnomalyModDbStart = "https://www.moddb.com/downloads/start/277404";

    public const string AnomalyPage = "https://www.moddb.com/mods/stalker-anomaly/downloads/stalker-anomaly-153";

    public const string AnomalySha256 = "e564d9110e673eacfa5604b0837f6e6208eae4cd46cd5682326ed59f931527ae";

    public const string GammaArchive = "GAMMA RC3.7z";

    /// <summary>A pasta pública do GAMMA no Mediafire (a API lista o arquivo, o tamanho e o hash sem Cloudflare).</summary>
    public const string GammaMediafireFolder = "agyul0e6fbban";

    public static string MediafireFilePage(string quickKey) => $"https://www.mediafire.com/file/{quickKey}/file";

    /// <summary>Os outros espelhos do RC3, pra quem quiser baixar na mão (o Gofile e o MEGA não deixam automatizar).</summary>
    public static IReadOnlyList<string> GammaManualMirrors { get; } =
    [
      "https://www.mediafire.com/folder/agyul0e6fbban/STALKER_GAMMA",
      "https://gofile.io/d/Y8NnGS",
      "https://mega.nz/folder/xeUC3TJa#D2tWBrPrKi64Kq_S4CCYrg",
    ];

    /// <summary>
    /// O 7zr.exe (o 7-Zip de linha de comando que só abre .7z), que abre o pacote extra do 7-Zip, que traz o 7za.exe
    /// de 64 bits que extrai o RC3 (é o RC3 que traz o 7z.exe do GAMMA). Versão fixa, conferida pelo hash.
    /// </summary>
    public const string SevenZipUrl = "https://github.com/ip7z/7zip/releases/download/24.09/7zr.exe";

    public const string SevenZipSha256 = "d2c0045523cf053a6b43f9315e9672fc2535f06aeadd4ffa53c729cd8b2b6dfe";

    /// <summary>O pacote extra da mesma versão, com o 7za.exe de 64 bits (o 7zr é de 32).</summary>
    public const string SevenZipExtraUrl = "https://github.com/ip7z/7zip/releases/download/24.09/7z2409-extra.7z";

    public const string SevenZipExtraSha256 = "43ae97658d0fc5b4eec4d409d85f7bed74a80945fd5704333a3599e0bd79b5fc";

    /// <summary>O Visual C++ mais recente (x64) e o DirectX End-User Runtime, os links da wiki. Assinatura da Microsoft conferida antes de rodar.</summary>
    public const string VcRedistUrl = "https://aka.ms/vs/17/release/vc_redist.x64.exe";

    public const string DirectXUrl = "https://download.microsoft.com/download/1/7/1/1718CCC4-6315-4D8E-9543-8E28A4E18C4C/dxwebsetup.exe";
  }
}
