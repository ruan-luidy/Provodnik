namespace Provodnik.Core.Diagnostics
{
  /// <summary>
  /// O console das ferramentas (o cmd que o launcher original abria junto): cada linha que o git, o curl, o 7-Zip e o
  /// stalker-gamma-dl-cli escrevem, menos o redesenho de progresso, que vira barra na tela.
  /// </summary>
  public interface IToolConsole
  {
    void Write(string tool, string line);
  }

  public sealed class NullToolConsole : IToolConsole
  {
    public static readonly NullToolConsole Instance = new();

    public void Write(string tool, string line)
    {
    }
  }
}
