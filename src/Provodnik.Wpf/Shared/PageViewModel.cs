using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Provodnik.Wpf.Shared
{
  /// <summary>
  /// Base de toda aba (a PageViewModel do Терминал e do Windfinder): carregando/erro e o "tentar de novo". A aba só
  /// busca quando aparece e está <see cref="Stale"/>: trocar de aba e voltar não busca tudo de novo.
  /// </summary>
  public abstract partial class PageViewModel : ObservableObject
  {
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;

    public bool Stale { get; set; } = true;

    /// <summary>Chamado quando a aba aparece. Só busca de novo se estiver velha.</summary>
    public async Task ShowAsync()
    {
      if (!Stale)
        return;

      Stale = false;
      await RunAsync(LoadAsync);
    }

    /// <summary>O que a aba busca ao aparecer. Aba sem nada pra buscar não precisa sobrescrever.</summary>
    protected virtual Task LoadAsync() => Task.CompletedTask;

    [RelayCommand]
    private Task ReloadAsync() => RunAsync(LoadAsync);

    protected async Task RunAsync(Func<Task> work)
    {
      IsLoading = true;
      Error = null;
      try
      {
        await work();
      }
      catch (Exception ex)
      {
        Error = $"Não deu pra carregar: {ex.Message}";
      }
      finally
      {
        IsLoading = false;
      }
    }
  }
}
