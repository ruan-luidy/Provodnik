using Provodnik.Core.Modpack;
using Provodnik.Core.Text;
using Provodnik.Features.Mods;
using Provodnik.Wpf.Shared;

namespace Provodnik.Wpf.Features.Mods
{
  /// <summary>Uma linha da tela de Mods: o addon, o arquivo em downloads\ e a pasta em mods\.</summary>
  public sealed record ModItemRow(ModInventoryItem Item, string FolderPath)
  {
    public int Index => Item.Entry.Index;

    public string Title => Item.Entry.Title;

    public string Author => Item.Entry.Author;

    public string Source => Item.Entry.Kind switch
    {
      ModKind.ModDb => "ModDB",
      ModKind.GoFile => "gofile",
      _ => "GitHub",
    };

    public string ArchiveName => Item.Entry.ArchiveName ?? "—";

    public string ArchiveText => Item.ArchiveSize is { } size ? ByteSize.Format(size) : Item.HasPartial ? "pela metade" : "faltando";

    public string FolderText => Item.FolderHasContent ? "Instalado" : Item.DisabledInGamma ? "Desligado" : "Vazio";

    public string? FolderHint => !Item.FolderHasContent && Item.DisabledInGamma
      ? "Pasta vazia, mas o GAMMA deixa este mod desligado no MO2: não faz falta no jogo."
      : null;

    public string? WebPage => Item.Entry.WebPage;

    /// <summary>Instalado pelo Проводник (sabe de qual arquivo veio) ou só "tem coisa na pasta" (instalado pelo original).</summary>
    public string Origin => Item.Installed is { } installed ? $"Проводник, {installed.InstalledAt:dd/MM HH:mm}" : Item.FolderHasContent ? "launcher original" : "—";

    public RowTone Tone => Item.NeedsInstall ? RowTone.Error : !Item.FolderHasContent ? RowTone.Muted : !Item.ArchivePresent ? RowTone.Warning : RowTone.Success;

    public bool IsMissing => Item.NeedsInstall;
  }
}
