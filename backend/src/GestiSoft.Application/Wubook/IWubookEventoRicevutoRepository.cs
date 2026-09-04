using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Wubook;

public interface IWubookEventoRicevutoRepository
{
    Task<WubookEventoRicevuto?> GetByRcodeAsync(Guid strutturaId, int rcode, CancellationToken cancellationToken);

    /// <summary>Più recenti prima — usata dalla pagina Wubook per mostrare le prenotazioni intercettate.</summary>
    Task<IReadOnlyList<WubookEventoRicevuto>> ListByStrutturaIdAsync(Guid strutturaId, CancellationToken cancellationToken);

    /// <summary>Inserisce l'evento, o aggiorna quello già presente per lo stesso Rcode/Struttura se è un retry.</summary>
    Task UpsertAsync(WubookEventoRicevuto evento, CancellationToken cancellationToken);
}
