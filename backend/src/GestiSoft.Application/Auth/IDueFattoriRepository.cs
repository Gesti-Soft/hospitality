using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Auth;

/// <summary>Codici di recupero e dispositivi ricordati del 2FA — sempre cercati per hash, mai per valore in chiaro.</summary>
public interface IDueFattoriRepository
{
    Task<IReadOnlyList<CodiceRecuperoUtente>> ListCodiciNonUsatiAsync(Guid utenteId, CancellationToken cancellationToken);

    /// <summary>Sostituisce in blocco i codici dell'utente: rigenerarli invalida sempre i precedenti.</summary>
    Task SostituisciCodiciAsync(Guid utenteId, IReadOnlyList<CodiceRecuperoUtente> nuovi, CancellationToken cancellationToken);

    Task SegnaCodiceUsatoAsync(CodiceRecuperoUtente codice, CancellationToken cancellationToken);

    Task<DispositivoFidato?> GetDispositivoAsync(string tokenHash, CancellationToken cancellationToken);

    Task AddDispositivoAsync(DispositivoFidato dispositivo, CancellationToken cancellationToken);

    /// <summary>Usata quando il 2FA viene disattivato o i codici rigenerati: tutti i browser ricordati tornano a dover inserire un codice.</summary>
    Task RimuoviDispositiviAsync(Guid utenteId, CancellationToken cancellationToken);

    /// <summary>Pulizia dei dispositivi scaduti — righe morte che non servono più a nessuno.</summary>
    Task RimuoviDispositiviScadutiAsync(CancellationToken cancellationToken);
}
