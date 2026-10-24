using Velopack;

namespace Provodnik.Wpf
{
  /// <summary>
  /// Entrada própria, como no Otmetka: o Velopack roda antes de tudo (instalar, atualizar e desinstalar chamam o exe
  /// com argumentos dele e saem ali mesmo), antes até da instância única. Fora de uma instalação do Velopack (o exe
  /// solto na pasta do GAMMA, o Debug) não faz nada e o app segue normal.
  /// </summary>
  public static class Program
  {
    [STAThread]
    public static void Main()
    {
      VelopackApp.Build().Run();

      var app = new App();
      app.InitializeComponent();
      app.Run();
    }
  }
}
