using GestiSoft.Application.Auth;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.SuperAdmin;
using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Wubook;

public record AggiornaWubookConfigRequest(bool Attivo);

/// <summary>
/// Credenziali Wubook di una Struttura, gestite solo dal Super Admin — vedi WubookIntegrazione per
/// il significato di ogni campo. Il Token Wubook non è qui: è uguale per tutte le Strutture, vedi
/// ImpostazioniGlobali. NOTA: la scadenza della licenza NON è qui — è la licenza software GestiSoft
/// della Struttura (vedi StrutturaService.AggiornaLicenzaAsync), indipendente da Wubook.
/// </summary>
public record AggiornaWubookLicenzaRequest(string? GestisoftUsername, string? GestisoftToken, string? CodiceStruttura);

/// <summary>
/// Configurazione dell'integrazione Wubook per Struttura. Il toggle self-service (Attivo) resta
/// del Cliente/operatore; il Codice struttura (lcode) e le credenziali gestisoft.it sono gestite
/// solo dal Super Admin (<see cref="AggiornaLicenzaSuperAdminAsync"/>) — inserite a mano, mai più
/// recuperate da gestisoft.it (comportamento precedente, vedi commento su WubookIntegrazione).
/// </summary>
public class WubookLicenzaService(
    IWubookIntegrazioneRepository repository,
    IWubookEventoRicevutoRepository eventiRicevuti,
    IStrutturaRepository strutture,
    ImpostazioniGlobaliService impostazioniGlobali,
    PermessoStrutturaGuard permessoGuard,
    ConcessioneServiziGuard concessioneGuard)
{
    public async Task<WubookIntegrazione> GetOrDefaultAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomRead, cancellationToken);

        return await repository.GetByStrutturaIdAsync(strutturaId, cancellationToken)
            ?? new WubookIntegrazione { StrutturaId = strutturaId };
    }

    public async Task<WubookIntegrazione> AggiornaConfigAsync(ICurrentUser currentUser, Guid strutturaId, AggiornaWubookConfigRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomWrite, cancellationToken);
        if (request.Attivo)
        {
            await concessioneGuard.EnsureWubookAsync(strutturaId, cancellationToken);
        }

        var entity = await repository.GetByStrutturaIdAsync(strutturaId, cancellationToken)
            ?? new WubookIntegrazione { StrutturaId = strutturaId };

        entity.Attivo = request.Attivo;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await repository.UpsertAsync(entity, cancellationToken);
        return entity;
    }

    /// <summary>Lettura completa (inclusi i valori segreti) — solo Super Admin, usata dalla pagina Impostazioni di Super Admin.</summary>
    public async Task<WubookIntegrazione> GetLicenzaSuperAdminAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        RichiediSuperAdmin(currentUser);
        return await repository.GetByStrutturaIdAsync(strutturaId, cancellationToken)
            ?? new WubookIntegrazione { StrutturaId = strutturaId };
    }

    public async Task<WubookIntegrazione> AggiornaLicenzaSuperAdminAsync(ICurrentUser currentUser, Guid strutturaId, AggiornaWubookLicenzaRequest request, CancellationToken cancellationToken)
    {
        RichiediSuperAdmin(currentUser);

        var entity = await repository.GetByStrutturaIdAsync(strutturaId, cancellationToken)
            ?? new WubookIntegrazione { StrutturaId = strutturaId };

        entity.GestisoftUsername = request.GestisoftUsername;
        entity.GestisoftToken = request.GestisoftToken;
        entity.CodiceStruttura = request.CodiceStruttura;
        entity.UltimoErrore = null;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await repository.UpsertAsync(entity, cancellationToken);
        return entity;
    }

    /// <summary>Credenziali pronte all'uso per una Struttura — nessuna chiamata remota, solo lettura/validazione locale (token presente, licenza della Struttura non scaduta).</summary>
    public async Task<(string Token, string Lcode)> GetCredenzialiValideAsync(Guid strutturaId, CancellationToken cancellationToken)
    {
        await concessioneGuard.EnsureWubookAsync(strutturaId, cancellationToken);

        var integrazione = await repository.GetByStrutturaIdAsync(strutturaId, cancellationToken)
            ?? throw new ConflictException("Integrazione OTA non configurata per questa struttura.");

        if (!integrazione.Attivo)
        {
            throw new ConflictException("Integrazione OTA non attiva per questa struttura.");
        }

        // La scadenza qui sotto è la licenza software GestiSoft della Struttura (non un problema di
        // Wubook in sé): niente SegnalaErroreAsync, quel campo (WubookUltimoErrore) è dedicato ai
        // veri errori di sincronizzazione Wubook e va mostrato solo sotto "Wubook" nel pannello
        // Super Admin — questa informazione è già visibile in cima allo stesso pannello, nella
        // sezione "Licenza GestiSoft", che ha sempre la priorità (nessun bisogno di duplicarla qui
        // con un messaggio "contatta l'assistenza" che non ha senso per chi l'assistenza la fa).
        var struttura = await strutture.GetByIdAsync(strutturaId, cancellationToken);
        if (struttura?.ScadenzaLicenza is { } scadenza && scadenza < DateTime.UtcNow)
        {
            throw new ConflictException($"Licenza della struttura scaduta il {scadenza:dd/MM/yyyy}.");
        }

        var tokenWubook = await impostazioniGlobali.GetTokenWubookAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(tokenWubook) || string.IsNullOrWhiteSpace(integrazione.CodiceStruttura))
        {
            var messaggio = "Credenziali OTA non configurate per questa struttura. Contatta l'assistenza GestiSoft.";
            await SegnalaErroreAsync(integrazione, messaggio, cancellationToken);
            throw new ConflictException(messaggio);
        }

        if (integrazione.UltimoErrore is not null)
        {
            integrazione.UltimoErrore = null;
            await repository.UpsertAsync(integrazione, cancellationToken);
        }

        return (tokenWubook, integrazione.CodiceStruttura);
    }

    /// <summary>Per la vista Cliente/operatore (GET config): le credenziali sono pronte se il Codice struttura è impostato, la licenza della Struttura non è scaduta e il Token Wubook globale è configurato.</summary>
    public async Task<bool> CredenzialiProntoAsync(WubookIntegrazione integrazione, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(integrazione.CodiceStruttura))
        {
            return false;
        }

        var struttura = await strutture.GetByIdAsync(integrazione.StrutturaId, cancellationToken);
        if (struttura?.ScadenzaLicenza is { } scadenza && scadenza < DateTime.UtcNow)
        {
            return false;
        }

        var tokenWubook = await impostazioniGlobali.GetTokenWubookAsync(cancellationToken);
        return !string.IsNullOrWhiteSpace(tokenWubook);
    }

    /// <summary>Prenotazioni Wubook intercettate per questa Struttura (Lcode/Rcode/esito), più recenti prima — vedi WubookEventoRicevuto.</summary>
    public async Task<IReadOnlyList<WubookEventoRicevuto>> ListEventiRicevutiAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomRead, cancellationToken);
        return await eventiRicevuti.ListByStrutturaIdAsync(strutturaId, cancellationToken);
    }

    /// <summary>Id Software PayTourist (Fase 8) — un solo valore per tutta l'applicazione (Impostazioni globali del Super Admin), non per Struttura.</summary>
    public async Task<int> GetIdPaytouristAsync(CancellationToken cancellationToken) =>
        await impostazioniGlobali.GetIdSoftwarePaytouristAsync(cancellationToken)
            ?? throw new ConflictException("Id Software PayTourist non configurato. Contatta l'assistenza GestiSoft.");

    private async Task SegnalaErroreAsync(WubookIntegrazione integrazione, string messaggio, CancellationToken cancellationToken)
    {
        if (integrazione.UltimoErrore == messaggio)
        {
            return;
        }

        integrazione.UltimoErrore = messaggio;
        await repository.UpsertAsync(integrazione, cancellationToken);
    }

    /// <summary>
    /// Registra (messaggio non nullo) o cancella (null) un errore di sincronizzazione Wubook diverso
    /// da quelli di credenziali/licenza già gestiti sopra — visibile nel pannello "Stato
    /// sincronizzazione" della pagina Servizi OTA. Usato dal push automatico di disponibilità dopo
    /// ogni prenotazione creata/modificata/annullata (vedi PrenotazioniService): qui un fallimento è
    /// un vero rischio di overbooking (Wubook risultava configurato ma la chiamata non è passata),
    /// non deve sparire in silenzio.
    /// </summary>
    public async Task SegnalaEsitoSincronizzazioneAsync(Guid strutturaId, string? messaggio, CancellationToken cancellationToken)
    {
        var integrazione = await repository.GetByStrutturaIdAsync(strutturaId, cancellationToken);
        if (integrazione is null || integrazione.UltimoErrore == messaggio)
        {
            return;
        }

        integrazione.UltimoErrore = messaggio;
        await repository.UpsertAsync(integrazione, cancellationToken);
    }

    private static void RichiediSuperAdmin(ICurrentUser currentUser)
    {
        if (!currentUser.IsSuperAdmin)
        {
            throw new ForbiddenException("Solo il Super Admin può gestire la licenza OTA.");
        }
    }
}
