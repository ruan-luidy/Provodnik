using System.Collections.Concurrent;
using System.Windows.Markup;
using System.Windows.Media;

namespace Provodnik.Wpf.Shared.Icons
{
  public enum BrandKind
  {
    ModDb,
  }

  /// <summary>
  /// Logo de site como Geometry, no molde do <see cref="PhosphorExtension"/>:
  ///
  ///   <Button hc:IconElement.Geometry="{icons:Brand ModDb}" />
  ///
  /// O do ModDB sai do SVG oficial (tools/brand/moddb.svg, domínio público no Wikimedia Commons): só as letras e a
  /// chave, com o miolo das letras vazado e sem o bloco branco de fundo, pra pintar com a cor do texto. É duas vezes
  /// mais largo que alto: o botão dá a largura (Icon.Brand.Width).
  /// </summary>
  [MarkupExtensionReturnType(typeof(Geometry))]
  public sealed class BrandExtension : MarkupExtension
  {
    private static readonly ConcurrentDictionary<BrandKind, Geometry> Cache = new();

    // os paths de classe s3 (letras e chave) e os miolos de classe s1 dentro delas, na ordem B, D, chave, D, O, M
    private const string ModDb =
      "F0 M182.92 21.77l-1.18 77.82 36.21 3.43 0.09-12.16 9.23 0.59-0.3-38.17-9.02 0.78v-8.73l9.02-0.99 0.1-24.04-8.93 1.77v-10.01z " +
      "M192.43 58.77l23.16-2.26 0.1 31.89-23.55-1.27z M192.72 30.31l-0.19 16.49 23.16-3.34 0.1-18.54z " +
      "M170.35 25.6l-0.19 8.15 7.16-1.96-1.08 57.2-7.17-0.19-0.39 9.81-29.04-2.16 2.25-62.02z M150.04 41.5l17.76-4.81-1.18 49.85-17.95-0.49z " +
      "M125.8 8.43l-0.49 23.16c0 0-11.77 2.55-13.15 1.67-0.88-0.69-0.39-7.75-0.19-9.23l1.27-11.09c-17.76 8.54-11.58 32.78-3.53 36.01l-1.57 43.28" +
      "c0 2.26 1.27 6.28 4.02 6.68l12.95 0.49c2.26 0.09 5.79-1.48 5.99-6.77l1.57-46.03c9.32-6.47 11.97-37.38-6.87-38.17z " +
      "M101.66 50.53l-1.86 39.64h-5.11l-0.29 6.77-20.51-0.88 2.06-43.28 20.9-6.47-0.19 5.69z M94.2 54.94l-1.56 32.97h-11.78l1.47-29.73z " +
      "M72.03 58.28l-1.47 32.09h-4.03l-0.2 5.49-13.24-0.49 0.19-4.81h-3.33l1.17-26.88 3.44-0.89 0.3-3.92 13.24-4.02-0.1 4.51z " +
      "M65.65 62.3l-1.28 25.81-8.73 0.1 1.08-23.55z " +
      "M47.59 60.73l-1.47 34.54-5.3-0.19 0.88-23.36-0.97 0.2-0.89 3.83-3.04 0.49-0.69-3.34-0.88 0.2-0.89 21.78-4.9-0.2 0.98-29.04 5-1.48" +
      "-0.09 3.25 1.76-0.4 0.79 3.14 1.18-0.19 0.88-3.54 2.06-0.59 0.1-3.62z";

    public BrandExtension()
    {
    }

    public BrandExtension(BrandKind kind)
    {
      Kind = kind;
    }

    [ConstructorArgument("kind")]
    public BrandKind Kind { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider) => Get(Kind);

    public static Geometry Get(BrandKind kind) => Cache.GetOrAdd(kind, k =>
    {
      var geometry = Geometry.Parse(k switch
      {
        BrandKind.ModDb => ModDb,
        _ => "",
      });
      geometry.Freeze();
      return geometry;
    });
  }
}
