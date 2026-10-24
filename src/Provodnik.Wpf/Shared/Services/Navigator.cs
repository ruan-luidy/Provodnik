namespace Provodnik.Wpf.Shared.Services
{
  /// <summary>As telas do painel lateral, na ordem em que aparecem.</summary>
  public enum Tab
  {
    Home,
    Install,
    Mods,
    Logs,
  }

  /// <summary>Uma tela pedindo pra abrir outra (o "Instalar" do Início abre a tela de instalação). O shell escuta e troca de aba.</summary>
  public sealed class Navigator
  {
    public event Action<Tab>? PageRequested;

    public void Open(Tab tab) => PageRequested?.Invoke(tab);
  }
}
