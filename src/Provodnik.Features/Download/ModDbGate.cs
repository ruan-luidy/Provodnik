namespace Provodnik.Features.Download
{
  /// <summary>
  /// A vaga única do ModDB no app inteiro: a instalação dos mods e o Anomaly da instalação do zero dividem a mesma,
  /// do pedido do link até o fim do download. O ModDB não permite dois downloads ao mesmo tempo (os servidores dele
  /// só atendem um tanto de gente em paralelo), então nunca tem mais de um, nem com as duas coisas rodando juntas.
  /// </summary>
  public sealed class ModDbGate
  {
    internal SemaphoreSlim Slot { get; } = new(1);

    public async Task<IDisposable> EnterAsync(CancellationToken cancellationToken)
    {
      await Slot.WaitAsync(cancellationToken).ConfigureAwait(false);
      return new Exit(Slot);
    }

    private sealed class Exit(SemaphoreSlim slot) : IDisposable
    {
      private int _done;

      public void Dispose()
      {
        if (Interlocked.Exchange(ref _done, 1) == 0)
          slot.Release();
      }
    }
  }
}
