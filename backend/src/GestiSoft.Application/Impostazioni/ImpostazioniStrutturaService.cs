using GestiSoft.Application.Auth;
using GestiSoft.Application.Exceptions;
using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Impostazioni;

public record AggiornaImpostazioniRequest(
    bool PoliziaStatoAttiva,
    bool OsservatorioAttivo,
    bool PayTouristAttivo,
    TimeOnly? OraInvioGiornaliero,
    decimal? TassaSoggiornoPrezzo,
    int? TassaSoggiornoMaxGiorni,
    int? TassaSoggiornoEtaEsenzioneMinori,
    int? TassaSoggiornoEtaEsenzioneAnziani,
    decimal? TassaSoggiornoPercentualeResidenti,
    decimal? TassaSoggiornoPercentualeMinori,
    decimal? TassaSoggiornoPercentualeAnziani,
    string? ComuneAttivita);

public class ImpostazioniStrutturaService(
    IImpostazioniStrutturaRepository repository,
    IStrutturaRepository strutture,
    TenantAccessGuard accessGuard,
    ConcessioneServiziGuard concessioneGuard,
    GestioneUtentiGuard gestioneUtentiGuard)
{
    /// <summary>
    /// PayTourist è offline per manutenzione ogni giorno dalle 14:00 alle 18:00 (comunicato
    /// dall'utente) — <c>OraInvioGiornaliero</c> è condiviso dalle 3 integrazioni "schedine" (Alloggiati
    /// Web/Osservatorio/PayTourist), quindi se PayTourist è concesso a questa Struttura dal Super
    /// Admin (<see cref="Struttura.PayTouristAbilitato"/> — non il toggle self-service
    /// <see cref="ImpostazioniStruttura.PayTouristAttivo"/>, che l'operatore potrebbe riaccendere in
    /// qualunque momento) l'orario configurato non può cadere in quella fascia.
    /// </summary>
    private static readonly TimeOnly InizioManutenzionePayTourist = new(14, 0);
    private static readonly TimeOnly FineManutenzionePayTourist = new(18, 0);
    public async Task<ImpostazioniStruttura> GetOrDefaultAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await accessGuard.EnsureAccessAsync(currentUser, strutturaId, cancellationToken);
        await gestioneUtentiGuard.EnsureAsync(currentUser, strutturaId, cancellationToken);

        return await repository.GetByStrutturaIdAsync(strutturaId, cancellationToken)
            ?? new ImpostazioniStruttura { StrutturaId = strutturaId };
    }

    public async Task<ImpostazioniStruttura> AggiornaAsync(
        ICurrentUser currentUser,
        Guid strutturaId,
        AggiornaImpostazioniRequest request,
        CancellationToken cancellationToken)
    {
        await accessGuard.EnsureAccessAsync(currentUser, strutturaId, cancellationToken);
        await gestioneUtentiGuard.EnsureAsync(currentUser, strutturaId, cancellationToken);

        // La revoca del Super Admin prevale sempre sul toggle self-service: non basta nasconderlo
        // in UI, il salvataggio stesso va rifiutato se il servizio non è concesso al Cliente.
        if (request.PoliziaStatoAttiva)
        {
            await concessioneGuard.EnsureAlloggiatiWebAsync(strutturaId, cancellationToken);
        }
        if (request.OsservatorioAttivo)
        {
            await concessioneGuard.EnsureOsservatorioAsync(strutturaId, cancellationToken);
        }
        if (request.PayTouristAttivo)
        {
            await concessioneGuard.EnsurePayTouristAsync(strutturaId, cancellationToken);
        }

        if (request.OraInvioGiornaliero is { } orario && orario >= InizioManutenzionePayTourist && orario < FineManutenzionePayTourist)
        {
            var struttura = await strutture.GetByIdAsync(strutturaId, cancellationToken);
            if (struttura is { PayTouristAbilitato: true })
            {
                throw new ConflictException(
                    $"L'orario di invio non può essere tra le {InizioManutenzionePayTourist:HH:mm} e le {FineManutenzionePayTourist:HH:mm}: PayTourist è in manutenzione in quella fascia oraria.");
            }
        }

        var impostazioni = await repository.GetByStrutturaIdAsync(strutturaId, cancellationToken)
            ?? new ImpostazioniStruttura { StrutturaId = strutturaId };

        impostazioni.PoliziaStatoAttiva = request.PoliziaStatoAttiva;
        impostazioni.OsservatorioAttivo = request.OsservatorioAttivo;
        impostazioni.PayTouristAttivo = request.PayTouristAttivo;
        impostazioni.OraInvioGiornaliero = request.OraInvioGiornaliero;
        impostazioni.TassaSoggiornoPrezzo = request.TassaSoggiornoPrezzo;
        impostazioni.TassaSoggiornoMaxGiorni = request.TassaSoggiornoMaxGiorni;
        impostazioni.TassaSoggiornoEtaEsenzioneMinori = request.TassaSoggiornoEtaEsenzioneMinori;
        impostazioni.TassaSoggiornoEtaEsenzioneAnziani = request.TassaSoggiornoEtaEsenzioneAnziani;
        impostazioni.TassaSoggiornoPercentualeResidenti = request.TassaSoggiornoPercentualeResidenti;
        impostazioni.TassaSoggiornoPercentualeMinori = request.TassaSoggiornoPercentualeMinori;
        impostazioni.TassaSoggiornoPercentualeAnziani = request.TassaSoggiornoPercentualeAnziani;
        impostazioni.ComuneAttivita = request.ComuneAttivita;
        impostazioni.UpdatedAtUtc = DateTime.UtcNow;

        await repository.UpsertAsync(impostazioni, cancellationToken);
        return impostazioni;
    }
}
