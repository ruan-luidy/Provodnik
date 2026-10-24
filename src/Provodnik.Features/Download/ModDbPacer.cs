using Provodnik.Core.Downloads;
using Provodnik.Core.Text;

namespace Provodnik.Features.Download
{
  /// <summary>
  /// O ritmo com o ModDB. O que o ModDB limita é a página "addons/start/N" (onde o stalker-gamma-dl-cli pega o link),
  /// não o download no CDN (cdn.dbolical.com). Então:
  ///   - os pedidos de link vão um de cada vez, com um respiro entre eles (<see cref="Spacing"/>);
  ///   - quando o ModDB reclama (403, 429, Cloudflare, sem link), todo mundo esfria junto, em vez de cada mod
  ///     insistir por conta própria e piorar o bloqueio: 30 s, depois 60, 120... até 5 min;
  ///   - cada link que vem certo diminui a espera de novo.
  /// O download em si fica livre pra rodar em paralelo, no limite das Configurações.
  /// </summary>
  public sealed class ModDbPacer(IModDbResolver resolver, TimeSpan? spacing = null, TimeSpan? firstCooldown = null)
  {
    private static readonly TimeSpan MaxCooldown = TimeSpan.FromMinutes(5);
    private readonly SemaphoreSlim _gate = new(1);
    private DateTime _nextAllowed = DateTime.MinValue;
    private TimeSpan _cooldown = TimeSpan.Zero;

    public TimeSpan Spacing { get; } = spacing ?? TimeSpan.FromSeconds(1.5);

    /// <summary>A primeira espera depois de uma reclamação; dobra a cada nova, até <see cref="MaxCooldown"/>.</summary>
    public TimeSpan FirstCooldown { get; } = firstCooldown ?? TimeSpan.FromSeconds(30);

    /// <summary>Quantas vezes o ModDB mandou esperar nesta rodada.</summary>
    public int Cooldowns { get; private set; }

    public async Task<string> ResolveAsync(string addonUrl, Action<string> waiting, CancellationToken cancellationToken)
    {
      await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
      try
      {
        var wait = _nextAllowed - DateTime.UtcNow;
        if (wait > TimeSpan.Zero)
        {
          if (wait > TimeSpan.FromSeconds(5))
            waiting(L.T("features.pacer.waiting", wait.TotalSeconds));
          await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
        }

        try
        {
          waiting(L.T("features.pacer.requesting"));
          var link = await resolver.ResolveAsync(addonUrl, cancellationToken).ConfigureAwait(false);
          // deu certo: a espera volta a encolher
          _cooldown = _cooldown > TimeSpan.Zero ? TimeSpan.FromTicks(_cooldown.Ticks / 2) : TimeSpan.Zero;
          _nextAllowed = DateTime.UtcNow + Spacing;
          return link;
        }
        catch (Exception ex) when (IsThrottle(ex) && !cancellationToken.IsCancellationRequested)
        {
          _cooldown = _cooldown == TimeSpan.Zero ? FirstCooldown : TimeSpan.FromTicks(Math.Min(_cooldown.Ticks * 2, MaxCooldown.Ticks));
          _nextAllowed = DateTime.UtcNow + _cooldown;
          Cooldowns++;
          throw;
        }
      }
      finally
      {
        _gate.Release();
      }
    }

    /// <summary>O CDN também pode bloquear: o download avisa, e os próximos pedidos esfriam do mesmo jeito.</summary>
    public void ReportThrottle(Exception ex)
    {
      if (!IsThrottle(ex))
        return;
      _cooldown = _cooldown == TimeSpan.Zero ? FirstCooldown : TimeSpan.FromTicks(Math.Min(_cooldown.Ticks * 2, MaxCooldown.Ticks));
      _nextAllowed = DateTime.UtcNow + _cooldown;
      Cooldowns++;
    }

    internal static bool IsThrottle(Exception ex)
    {
      // as frases do Проводник vêm nos três idiomas (infra.modDb.noLink, infra.modDb.timeout e o bloqueio do navegador)
      var text = ex.Message.ToLowerInvariant();
      return text.Contains("403") || text.Contains("429") || text.Contains("cloudflare") || text.Contains("challenge")
        || text.Contains("too many") || text.Contains("temporarily") || text.Contains("bloqueou") || text.Contains("заблокировал")
        || text.Contains("não devolveu o link") || text.Contains("didn't return the download link") || text.Contains("не вернул ссылку")
        || text.Contains("não respondeu a tempo") || text.Contains("didn't respond in time") || text.Contains("не ответил вовремя");
    }
  }
}
