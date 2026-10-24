using System.Collections.Concurrent;
using System.Windows.Markup;
using System.Windows.Media;
using MahApps.Metro.IconPacks;

namespace Provodnik.Wpf.Shared.Icons
{
  /// <summary>
  /// Ícone do Phosphor como Geometry, pra usar direto no XAML - todo ícone do terminal sai daqui:
  ///
  ///   <Path Data="{icons:Phosphor PlusBold}" />
  ///   <Button hc:IconElement.Geometry="{icons:Phosphor XBold}" />
  ///
  /// O pacote (MahApps.Metro.IconPacks.PhosphorIcons) entrega o desenho por um controle; aqui ele é criado uma vez
  /// por ícone, o path vira Geometry congelada e fica em cache - a mesma instância serve todas as telas.
  /// Os nomes seguem o Phosphor com o peso no fim (PlusBold, TrashFill, CursorLight...); o terminal usa Bold, que
  /// é o que fica legível nos 12-14 px dos botões.
  /// </summary>
  [MarkupExtensionReturnType(typeof(Geometry))]
  public sealed class PhosphorExtension : MarkupExtension
  {
    private static readonly ConcurrentDictionary<PackIconPhosphorIconsKind, Geometry> Cache = new();

    public PhosphorExtension()
    {
    }

    public PhosphorExtension(PackIconPhosphorIconsKind kind)
    {
      Kind = kind;
    }

    [ConstructorArgument("kind")]
    public PackIconPhosphorIconsKind Kind { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider) => Get(Kind);

    public static Geometry Get(PackIconPhosphorIconsKind kind) => Cache.GetOrAdd(kind, k =>
    {
      // O controle preenche Data no construtor a partir do dicionário do pacote; não precisa entrar na tela.
      var data = new PackIconPhosphorIcons { Kind = k }.Data;
      if (string.IsNullOrEmpty(data))
        return Geometry.Empty;

      var geometry = Geometry.Parse(data);
      geometry.Freeze();
      return geometry;
    });
  }
}
