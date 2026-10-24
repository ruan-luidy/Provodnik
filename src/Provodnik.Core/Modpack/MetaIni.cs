namespace Provodnik.Core.Modpack
{
  /// <summary>
  /// O meta.ini que o MO2 lê de cada pasta de mod, montado a partir do resources\meta.ini do GAMMA. O original troca
  /// "installationFile=", "version=", "url=" e "hasCustomURL=" com -replace, que ignora maiúscula: ignoredVersion e
  /// newestVersion também ganham o nome do arquivo. O resultado aqui é o mesmo, sem estragar a caixa das chaves.
  /// </summary>
  public static class MetaIni
  {
    public const string DefaultTemplate =
      "[General]\r\ngameName=stalkeranomaly\r\nmodid=0\r\nignoredVersion=\r\nversion=d2021.10.19.0\r\nnewestVersion=\r\n" +
      "category=\"-1,\"\r\nnexusFileStatus=1\r\ninstallationFile=\r\nrepository=\r\ncomments=\r\nnotes=\r\nnexusDescription=\r\n" +
      "url=\r\nhasCustomURL=true\r\nlastNexusQuery=\r\nlastNexusUpdate=\r\nnexusLastModified=2021-11-09T18:10:18Z\r\n" +
      "converted=false\r\nvalidated=false\r\ncolor=@Variant(\\0\\0\\0\\x43\\0\\xff\\xff\\0\\0\\0\\0\\0\\0\\0\\0)\r\ntracked=0\r\n\r\n" +
      "[installedFiles]\r\n1\\modid=0\r\n1\\fileid=0\r\nsize=1\r\n";

    public const string SeparatorTemplate =
      "[General]\r\nmodid=0\r\nversion=\r\nnewestVersion=\r\ncategory=0\r\ninstallationFile=\r\n\r\n[installedFiles]\r\nsize=0\r\n";

    public static string Build(string template, string archiveName, string? url)
    {
      var lines = template.Replace("\r\n", "\n").Split('\n');
      for (var i = 0; i < lines.Length; i++)
      {
        var equals = lines[i].IndexOf('=');
        if (equals < 0)
          continue;

        var key = lines[i][..equals];
        var value = key switch
        {
          _ when key.Equals("installationFile", StringComparison.OrdinalIgnoreCase) => archiveName,
          _ when key.EndsWith("version", StringComparison.OrdinalIgnoreCase) => archiveName,
          _ when key.Equals("url", StringComparison.OrdinalIgnoreCase) => url ?? string.Empty,
          _ when key.Equals("hasCustomURL", StringComparison.OrdinalIgnoreCase) => "true",
          _ => null,
        };
        if (value is not null)
          lines[i] = $"{key}={value}";
      }

      return string.Join("\r\n", lines);
    }
  }
}
