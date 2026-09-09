using System.Text.RegularExpressions;
using GestiSoft.Application.Auth;
using GestiSoft.Application.Exceptions;
using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Camere;

public record CreaCanaleVenditaRequest(string Descrizione, string? Colore = null);

/// <summary>
/// CRUD dei canali/agenzie di vendita (es. "Booking.com", "Diretto") — porta
/// OrderManagement.Model.BusinesObject.SettingAgenzie del legacy, usato come elenco suggerimenti
/// per il campo Agenzia (testo libero) di Prenotazione.
/// </summary>
public partial class CanaliVenditaService(ICanaleVenditaRepository canali, PermessoStrutturaGuard permessoGuard)
{
    // Stessa palette usata dal Calendario (frontend/src/pages/operativo/CalendarioPage.tsx,
    // PALETTE_CANALI) — un colore fisso e persistito sul canale, non più ricalcolato in base
    // all'ordine delle prenotazioni visibili in un dato periodo (era la causa del bug per cui i
    // colori "saltavano" cambiando pagina/periodo).
    private static readonly string[] PaletteColori =
        ["#1C7EA8", "#DD7A2C", "#1F8A70", "#3A4453", "#4FB4DE", "#F0A868", "#C2921C", "#C24444"];

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex RegexColoreHex();

    /// <summary>Colore valido esplicito → normalizzato in maiuscolo; altrimenti prossimo della palette in base al numero di canali già esistenti.</summary>
    private static string ScegliColore(string? richiesto, int numeroCanaliEsistenti)
    {
        if (!string.IsNullOrWhiteSpace(richiesto) && RegexColoreHex().IsMatch(richiesto.Trim()))
        {
            return richiesto.Trim().ToUpperInvariant();
        }
        return PaletteColori[numeroCanaliEsistenti % PaletteColori.Length];
    }

    public async Task<IReadOnlyList<SettingAgenzia>> ListaAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingAgency, cancellationToken);
        return await canali.ListByStrutturaAsync(strutturaId, cancellationToken);
    }

    public async Task<SettingAgenzia> CreaAsync(ICurrentUser currentUser, Guid strutturaId, CreaCanaleVenditaRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingAgency, cancellationToken);

        var descrizione = request.Descrizione.Trim();
        if (await canali.ExistsByDescrizioneAsync(strutturaId, descrizione, escludiId: null, cancellationToken))
        {
            throw new ConflictException("Esiste già un canale di vendita con questo nome.");
        }

        var esistenti = await canali.ListByStrutturaAsync(strutturaId, cancellationToken);
        var entity = new SettingAgenzia { StrutturaId = strutturaId, Descrizione = descrizione, Colore = ScegliColore(request.Colore, esistenti.Count) };
        await canali.AddAsync(entity, cancellationToken);
        return entity;
    }

    public async Task<SettingAgenzia> AggiornaAsync(ICurrentUser currentUser, Guid strutturaId, Guid canaleId, CreaCanaleVenditaRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingAgency, cancellationToken);

        var entity = await canali.GetAsync(canaleId, cancellationToken)
            ?? throw new NotFoundException("Canale di vendita non trovato.");
        if (entity.StrutturaId != strutturaId)
        {
            throw new NotFoundException("Canale di vendita non trovato.");
        }

        var descrizione = request.Descrizione.Trim();
        if (await canali.ExistsByDescrizioneAsync(strutturaId, descrizione, escludiId: canaleId, cancellationToken))
        {
            throw new ConflictException("Esiste già un canale di vendita con questo nome.");
        }

        entity.Descrizione = descrizione;
        // In modifica un colore mancante/non valido lascia invariato quello già assegnato — solo
        // in creazione un colore assente fa scattare l'assegnazione automatica dalla palette.
        if (!string.IsNullOrWhiteSpace(request.Colore) && RegexColoreHex().IsMatch(request.Colore.Trim()))
        {
            entity.Colore = request.Colore.Trim().ToUpperInvariant();
        }
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await canali.UpdateAsync(entity, cancellationToken);
        return entity;
    }

    /// <summary>
    /// Crea un canale vendita per ogni valore del campo Agenzia già presente sulle prenotazioni
    /// della struttura (es. arrivate da una sincronizzazione Wubook) che non corrisponde ancora a
    /// nessun canale configurato — evita di doverli ritrovare/digitare a mano uno per uno.
    /// </summary>
    public async Task<IReadOnlyList<SettingAgenzia>> ImportaDaPrenotazioniAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingAgency, cancellationToken);

        var esistenti = await canali.ListByStrutturaAsync(strutturaId, cancellationToken);
        // "Diretta" è già un'opzione implicita del form prenotazione (non un canale configurabile):
        // non va reimportata come se fosse un canale mancante.
        var nomiEsistenti = new HashSet<string>(esistenti.Select(c => c.Descrizione), StringComparer.OrdinalIgnoreCase) { "Diretta" };

        var agenzieDaPrenotazioni = await canali.ListaAgenzieDistinteDaPrenotazioniAsync(strutturaId, cancellationToken);
        var daImportare = agenzieDaPrenotazioni
            .Select(a => a.Trim())
            .Where(a => a.Length > 0)
            // Stessa agenzia scritta con maiuscole diverse su prenotazioni diverse (es. "booking.com"
            // e "Booking.com") deve diventare un solo canale, non uno per variante.
            .GroupBy(a => a, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Where(a => !nomiEsistenti.Contains(a))
            .ToList();

        var creati = new List<SettingAgenzia>();
        foreach (var descrizione in daImportare)
        {
            var entity = new SettingAgenzia { StrutturaId = strutturaId, Descrizione = descrizione, Colore = ScegliColore(null, esistenti.Count + creati.Count) };
            await canali.AddAsync(entity, cancellationToken);
            creati.Add(entity);
        }

        return creati;
    }

    public async Task EliminaAsync(ICurrentUser currentUser, Guid strutturaId, Guid canaleId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingAgency, cancellationToken);

        var entity = await canali.GetAsync(canaleId, cancellationToken)
            ?? throw new NotFoundException("Canale di vendita non trovato.");
        if (entity.StrutturaId != strutturaId)
        {
            throw new NotFoundException("Canale di vendita non trovato.");
        }

        await canali.DeleteAsync(entity, cancellationToken);
    }
}
