using GestiSoft.Application.Auth;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Prenotazioni;
using GestiSoft.Contracts.Trattamenti;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Trattamenti;

/// <summary>Prezzi di un trattamento già in euro, come si copiano sulla prenotazione.</summary>
public record PrezziTrattamento(decimal PrezzoAdulto, decimal? PrezzoBambino, int? EtaMassimaBambini);

/// <summary>
/// Trattamenti della struttura (colazione, mezza pensione, pensione completa, all inclusive): listino a persona e
/// a notte, e regola di calcolo dell'importo di una prenotazione. Il prezzo dei trattamenti si somma
/// a quello della camera, che resta il solo pernottamento.
/// </summary>
public class TrattamentiService(
    ITrattamentoStrutturaRepository trattamenti,
    PermessoStrutturaGuard permessoGuard,
    IPrenotazioneRepository prenotazioni,
    IStrutturaRepository strutture,
    IBuoniColazioneGenerator buoniGenerator,
    TenantAccessGuard accessGuard,
    GestioneUtentiGuard gestioneUtentiGuard)
{
    /// <summary>
    /// Ticket del trattamento di una prenotazione, se il suo listino ha "Stampa ticket": uno per ospite
    /// per giorno. La colazione segue la notte (dal giorno dopo l'arrivo a quello della partenza), gli
    /// altri trattamenti vanno con i giorni delle notti (la cena della mezza pensione è la sera
    /// dell'arrivo, non quella della partenza). Per il bar o ristorante convenzionato.
    /// </summary>
    public async Task<byte[]> BuoniColazioneAsync(ICurrentUser currentUser, Guid strutturaId, Guid prenotazioneId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.ReservationRead, cancellationToken);

        var prenotazione = await prenotazioni.GetAsync(prenotazioneId, cancellationToken);
        if (prenotazione is null || prenotazione.StrutturaId != strutturaId)
        {
            throw new NotFoundException("Prenotazione non trovata.");
        }

        if (prenotazione.Trattamento is not { } tipo)
        {
            throw new ConflictException("La prenotazione è solo pernottamento: non ci sono ticket da stampare.");
        }

        var listino = await trattamenti.GetAsync(strutturaId, tipo, cancellationToken);
        if (listino is not { StampaTicket: true })
        {
            throw new ConflictException($"\"{Nome(tipo)}\" non ha i ticket: attiva \"Stampa ticket\" in Impostazioni, scheda Servizi.");
        }

        if (prenotazione is not { CheckIn: { } arrivo, CheckOut: { } partenza } || partenza.Date <= arrivo.Date)
        {
            throw new ConflictException("Mancano le date del soggiorno.");
        }

        var (primo, ultimo) = tipo == TipoTrattamento.Colazione
            ? (arrivo.Date.AddDays(1), partenza.Date)
            : (arrivo.Date, partenza.Date.AddDays(-1));
        var giorni = new List<DateTime>();
        for (var giorno = primo; giorno <= ultimo; giorno = giorno.AddDays(1))
        {
            giorni.Add(giorno);
        }

        var struttura = await strutture.GetByIdAsync(strutturaId, cancellationToken)
            ?? throw new NotFoundException("Struttura non trovata.");

        return buoniGenerator.Genera(new DatiBuoniColazione(
            struttura.Nome,
            listino.EsercizioConvenzionato,
            prenotazione.NumeroPrenotazione,
            prenotazione.Camera?.Nome,
            Math.Max(1, prenotazione.NumeroOspiti ?? 1),
            giorni,
            $"Ticket {Nome(tipo).ToLowerInvariant()}"));
    }

    /// <summary>Lo legge anche chi fa le prenotazioni, per scegliere il trattamento: non solo chi imposta i prezzi.</summary>
    public async Task<IReadOnlyList<TrattamentoStruttura>> ListaAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomRead || p.ReservationRead, cancellationToken);
        return await trattamenti.ListByStrutturaAsync(strutturaId, cancellationToken);
    }

    /// <summary>Stesso permesso delle altre impostazioni generali della struttura (pagina Impostazioni): è una decisione del titolare.</summary>
    public async Task<TrattamentoStruttura> SalvaAsync(ICurrentUser currentUser, Guid strutturaId, TrattamentoStrutturaDto request, CancellationToken cancellationToken)
    {
        await accessGuard.EnsureAccessAsync(currentUser, strutturaId, cancellationToken);
        await gestioneUtentiGuard.EnsureAsync(currentUser, strutturaId, cancellationToken);
        Valida(request);

        var entity = await trattamenti.GetAsync(strutturaId, request.Tipo, cancellationToken)
            ?? new TrattamentoStruttura { StrutturaId = strutturaId, Tipo = request.Tipo };

        entity.Attivo = request.Attivo;
        entity.PrezzoPerPersona = request.PrezzoPerPersona;
        entity.PrezzoBambini = request.PrezzoBambini;
        entity.TipoPrezzoBambini = request.TipoPrezzoBambini;
        entity.EtaMassimaBambini = request.PrezzoBambini is null ? null : request.EtaMassimaBambini;
        entity.EsercizioConvenzionato = string.IsNullOrWhiteSpace(request.EsercizioConvenzionato) ? null : request.EsercizioConvenzionato.Trim();
        entity.StampaTicket = request.StampaTicket;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await trattamenti.UpsertAsync(entity, cancellationToken);
        return entity;
    }

    /// <summary>
    /// Prezzi da copiare sulla prenotazione per il trattamento scelto, dal listino attuale. Rifiuta
    /// un trattamento che la struttura non offre: non si vende ciò che non ha un prezzo.
    /// </summary>
    public async Task<PrezziTrattamento> PrezziDaListinoAsync(Guid strutturaId, TipoTrattamento tipo, CancellationToken cancellationToken)
    {
        var listino = await trattamenti.GetAsync(strutturaId, tipo, cancellationToken);
        if (listino is not { Attivo: true })
        {
            throw new ConflictException($"La struttura non offre il trattamento \"{Nome(tipo)}\": attivalo in Impostazioni, scheda Servizi.");
        }

        return PrezziDa(listino);
    }

    public static PrezziTrattamento PrezziDa(TrattamentoStruttura listino)
    {
        decimal? bambino = listino.PrezzoBambini is not { } prezzoBambini
            ? null
            : listino.TipoPrezzoBambini == TipoVariazionePrezzo.Percentuale
                ? Math.Round(listino.PrezzoPerPersona * prezzoBambini / 100m, 2, MidpointRounding.AwayFromZero)
                : prezzoBambini;

        return new PrezziTrattamento(listino.PrezzoPerPersona, bambino, bambino is null ? null : listino.EtaMassimaBambini);
    }

    /// <summary>
    /// Importo del trattamento per una notte: ogni ospite paga il prezzo adulto, tranne i bambini
    /// fino all'età massima, che pagano il prezzo bambini se c'è. I bambini sono compresi negli
    /// ospiti; se le età sono più degli ospiti si tengono le più piccole, come per il supplemento.
    /// </summary>
    public static decimal ImportoPerNotte(PrezziTrattamento prezzi, int numeroOspiti, IReadOnlyList<int> etaBambini)
    {
        if (numeroOspiti <= 0)
        {
            return 0m;
        }

        var bambini = etaBambini.OrderBy(e => e).Take(numeroOspiti).ToList();
        var adulti = numeroOspiti - bambini.Count;

        return adulti * prezzi.PrezzoAdulto + bambini.Sum(eta =>
            prezzi.PrezzoBambino is { } ridotto && prezzi.EtaMassimaBambini is { } etaMassima && eta <= etaMassima
                ? ridotto
                : prezzi.PrezzoAdulto);
    }

    /// <summary>Prezzi copiati sulla prenotazione, o null se è solo pernottamento.</summary>
    public static PrezziTrattamento? PrezziDellaPrenotazione(Prenotazione prenotazione) =>
        prenotazione is { Trattamento: not null, TrattamentoPrezzoAdulto: { } adulto }
            ? new PrezziTrattamento(adulto, prenotazione.TrattamentoPrezzoBambino, prenotazione.TrattamentoEtaMassimaBambini)
            : null;

    /// <summary>
    /// I quattro trattamenti che ogni struttura ha fin dalla creazione, non offerti e senza prezzo:
    /// si attivano da Impostazioni → Servizi. Non si cancellano (vedi i servizi extra per quelli liberi).
    /// </summary>
    public static IEnumerable<TrattamentoStruttura> BaseDellaStruttura(Guid strutturaId) =>
        Enum.GetValues<TipoTrattamento>().Select(tipo => new TrattamentoStruttura
        {
            StrutturaId = strutturaId,
            Tipo = tipo,
            Attivo = false,
            PrezzoPerPersona = 0m,
        });

    public static string Nome(TipoTrattamento tipo) => tipo switch
    {
        TipoTrattamento.Colazione => "Colazione",
        TipoTrattamento.MezzaPensione => "Mezza pensione",
        TipoTrattamento.PensioneCompleta => "Pensione completa",
        TipoTrattamento.AllInclusive => "All inclusive",
        _ => tipo.ToString(),
    };

    public static void Valida(TrattamentoStrutturaDto request)
    {
        if (!Enum.IsDefined(request.Tipo) || !Enum.IsDefined(request.TipoPrezzoBambini))
        {
            throw new ConflictException("Trattamento non riconosciuto.");
        }

        if (request.PrezzoPerPersona < 0 || request.PrezzoBambini is < 0)
        {
            throw new ConflictException("I prezzi di un trattamento non possono essere negativi.");
        }

        if (request.PrezzoBambini is null)
        {
            return;
        }

        if (request.EtaMassimaBambini is not (>= 0 and <= 17))
        {
            throw new ConflictException("Indica fino a che età (da 0 a 17 anni) si applica il prezzo bambini.");
        }

        if (request.TipoPrezzoBambini == TipoVariazionePrezzo.Percentuale && request.PrezzoBambini > 100m)
        {
            throw new ConflictException("Il prezzo bambini in percentuale va da 0 a 100% del prezzo adulto.");
        }
    }
}
