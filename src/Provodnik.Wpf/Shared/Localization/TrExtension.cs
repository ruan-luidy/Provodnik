using System.Windows.Markup;
using Provodnik.Core.Text;

namespace Provodnik.Wpf.Shared.Localization
{
  /// <summary>
  /// O texto da chave no idioma do app, direto no XAML: Text="{loc:Tr install.title}". O idioma não muda com o app
  /// aberto (trocar reabre), então é o texto pronto e não uma ligação.
  /// </summary>
  [MarkupExtensionReturnType(typeof(string))]
  public sealed class TrExtension(string key) : MarkupExtension
  {
    [ConstructorArgument("key")]
    public string Key { get; set; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider) => L.T(Key);
  }
}
