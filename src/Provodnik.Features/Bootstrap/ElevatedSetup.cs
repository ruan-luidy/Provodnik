using System.Text;

namespace Provodnik.Features.Bootstrap
{
  /// <summary>
  /// O que precisa de administrador numa instalação do zero, tudo num PowerShell só (um UAC só): as pastas na exclusão
  /// do Windows Defender, o Visual C++ e o DirectX em modo silencioso (só depois de conferir que a assinatura é da
  /// Microsoft) e os caminhos longos. Cada passo escreve "passo=resultado" num arquivo que o app lê depois.
  /// </summary>
  public sealed record ElevatedPlan(
    IReadOnlyList<string> DefenderExclusions,
    string? VcRedist,
    string? DirectX,
    bool LongPaths,
    string ResultFile)
  {
    public bool IsEmpty => DefenderExclusions.Count == 0 && VcRedist is null && DirectX is null && !LongPaths;
  }

  public static class ElevatedSetup
  {
    public const string Defender = "defender";
    public const string VcRedist = "vcredist";
    public const string DirectX = "directx";
    public const string LongPaths = "longpaths";

    public static string Script(ElevatedPlan plan)
    {
      var s = new StringBuilder();
      s.AppendLine("$ErrorActionPreference = 'Stop'");
      s.AppendLine($"$result = {Quote(plan.ResultFile)}");
      s.AppendLine("function Note($step, $value) { Add-Content -LiteralPath $result -Value \"$step=$value\" -Encoding UTF8 }");
      s.AppendLine("function Install($step, $file, $arguments) {");
      s.AppendLine("  try {");
      s.AppendLine("    $sig = Get-AuthenticodeSignature -LiteralPath $file");
      s.AppendLine("    if ($sig.Status -ne 'Valid' -or $sig.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') { Note $step \"signature:$($sig.Status)\"; return }");
      s.AppendLine("    $p = Start-Process -FilePath $file -ArgumentList $arguments -Wait -PassThru");
      s.AppendLine("    Note $step \"exit:$($p.ExitCode)\"");
      s.AppendLine("  } catch { Note $step \"error:$($_.Exception.Message)\" }");
      s.AppendLine("}");

      if (plan.DefenderExclusions.Count > 0)
      {
        var paths = string.Join(",", plan.DefenderExclusions.Select(Quote));
        s.AppendLine($"try {{ Add-MpPreference -ExclusionPath {paths}; Note {Defender} 'ok' }} catch {{ Note {Defender} \"error:$($_.Exception.Message)\" }}");
      }
      if (plan.VcRedist is { } vc)
        s.AppendLine($"Install {VcRedist} {Quote(vc)} '/install /quiet /norestart'");
      if (plan.DirectX is { } dx)
        s.AppendLine($"Install {DirectX} {Quote(dx)} '/Q'");
      if (plan.LongPaths)
        s.AppendLine($"try {{ Set-ItemProperty -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\FileSystem' -Name 'LongPathsEnabled' -Value 1 -Type DWord; Note {LongPaths} 'ok' }} catch {{ Note {LongPaths} \"error:$($_.Exception.Message)\" }}");
      return s.ToString();
    }

    /// <summary>"passo=resultado" por linha → dicionário (a última linha de cada passo vale).</summary>
    public static IReadOnlyDictionary<string, string> ParseResults(string text)
    {
      var results = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
      foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
      {
        var equals = line.IndexOf('=');
        if (equals > 0)
          results[line[..equals].Trim().TrimStart('﻿')] = line[(equals + 1)..].Trim();
      }
      return results;
    }

    /// <summary>
    /// O resultado de um instalador deu certo: 0, 3010 (pede reiniciar) e 1638 (já tem uma versão mais nova) no Visual
    /// C++; 0 no DirectX.
    /// </summary>
    public static bool Succeeded(string? result) => result is "ok" or "exit:0" or "exit:3010" or "exit:1638";

    /// <summary>Entre aspas simples do PowerShell (a aspa simples dobra).</summary>
    internal static string Quote(string value) => "'" + value.Replace("'", "''") + "'";
  }
}
