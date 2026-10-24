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
        File.Move(file, target);
      }

      Delete(source);
      return true;
    }

    /// <summary>Apaga a pasta inteira; arquivo somente-leitura perde o atributo antes.</summary>
    public static void Delete(string path)
    {
      if (!Directory.Exists(path))
        return;

      try
      {
        Directory.Delete(path, recursive: true);
      }
      catch (UnauthorizedAccessException)
      {
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
          ClearReadOnly(file);
        Directory.Delete(path, recursive: true);
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
