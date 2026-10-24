namespace Provodnik.Core.IO
{
  /// <summary>
  /// As cópias de pasta que o original fazia com Robocopy e FastCopy, em C#: copiar pulando o que já é igual (mesmo
  /// tamanho e mesma data, a regra do Robocopy), mover juntando com o destino (/s /move) e apagar mesmo com arquivo
  /// somente-leitura (o .git é cheio deles).
  /// </summary>
  public static class FileTree
  {
    /// <summary>
    /// Copia o conteúdo de <paramref name="source"/> pra dentro de <paramref name="destination"/>, sobrescrevendo o
    /// que mudou. Devolve quantos arquivos copiou.
    /// </summary>
    public static int CopyContents(string source, string destination, IReadOnlyCollection<string>? skipFolders = null, CancellationToken cancellationToken = default)
    {
      if (!Directory.Exists(source))
        return 0;

      var copied = 0;
      Directory.CreateDirectory(destination);
      foreach (var file in Directory.EnumerateFiles(source))
      {
        cancellationToken.ThrowIfCancellationRequested();
        var target = Path.Combine(destination, Path.GetFileName(file));
        if (Same(file, target))
          continue;
        ClearReadOnly(target);
        File.Copy(file, target, overwrite: true);
        copied++;
      }

      foreach (var folder in Directory.EnumerateDirectories(source))
      {
        var name = Path.GetFileName(folder);
        if (skipFolders?.Contains(name, StringComparer.OrdinalIgnoreCase) == true)
          continue;
        copied += CopyContents(folder, Path.Combine(destination, name), skipFolders, cancellationToken);
      }

      return copied;
    }

    /// <summary>
    /// O "robocopy origem destino /s /move": os arquivos de <paramref name="source"/> (com as subpastas) vão pra
    /// <paramref name="destination"/>, sobrescrevendo, e a origem some. Mover uma pasta pra dentro dela mesma não faz
    /// nada (devolve false).
    /// </summary>
    public static bool MoveContents(string source, string destination)
    {
      var from = Path.GetFullPath(source).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
      var to = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
      if (!Directory.Exists(source) || to.StartsWith(from, StringComparison.OrdinalIgnoreCase))
        return false;

      foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
      {
        var target = Path.Combine(destination, Path.GetRelativePath(source, file));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        if (File.Exists(target))
        {
          ClearReadOnly(target);
          File.Delete(target);
        }
        Retry(() => File.Move(file, target));
      }

      Delete(source);
      return true;
    }

    /// <summary>
    /// Apaga a pasta inteira. Arquivo e pasta somente-leitura (zip e rar guardam esse atributo) perdem o atributo antes;
    /// e o antivírus que segura um arquivo recém-extraído por um instante ganha mais duas chances.
    /// </summary>
    public static void Delete(string path)
    {
      for (var attempt = 1; ; attempt++)
      {
        if (!Directory.Exists(path))
          return;
        try
        {
          Directory.Delete(path, recursive: true);
          return;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException && attempt < 3)
        {
          ClearAttributes(path);
          Thread.Sleep(300 * attempt);
        }
      }
    }

    /// <summary>O antivírus escaneando o arquivo recém-extraído segura ele por um instante: mais duas chances.</summary>
    private static void Retry(Action action)
    {
      for (var attempt = 1; ; attempt++)
      {
        try
        {
          action();
          return;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException && attempt < 3)
        {
          Thread.Sleep(300 * attempt);
        }
      }
    }

    private static void ClearAttributes(string path)
    {
      try
      {
        foreach (var entry in Directory.EnumerateFileSystemEntries(path, "*", SearchOption.AllDirectories).Append(path))
        {
          if ((File.GetAttributes(entry) & (FileAttributes.ReadOnly | FileAttributes.System | FileAttributes.Hidden)) != 0)
            File.SetAttributes(entry, File.GetAttributes(entry) & ~(FileAttributes.ReadOnly | FileAttributes.System | FileAttributes.Hidden));
        }
      }
      catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
      {
      }
    }

    /// <summary>Tem pelo menos um arquivo em algum lugar dentro (pasta de mod que só ficou com o meta.ini não conta).</summary>
    public static bool HasContent(string path) =>
      Directory.Exists(path)
      && Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
        .Any(f => !Path.GetFileName(f).Equals("meta.ini", StringComparison.OrdinalIgnoreCase));

    private static bool Same(string source, string target)
    {
      var a = new FileInfo(source);
      var b = new FileInfo(target);
      return b.Exists && a.Length == b.Length && a.LastWriteTimeUtc == b.LastWriteTimeUtc;
    }

    private static void ClearReadOnly(string path)
    {
      if (File.Exists(path) && File.GetAttributes(path).HasFlag(FileAttributes.ReadOnly))
        File.SetAttributes(path, FileAttributes.Normal);
    }
  }
}
