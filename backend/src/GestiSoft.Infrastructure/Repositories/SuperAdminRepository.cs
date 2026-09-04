using GestiSoft.Application.SuperAdmin;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

/// <summary>
/// Query di sola lettura per la dashboard Super Admin. Volutamente più query semplici caricate in
/// memoria e ricomposte lì (non una mega-query LINQ con proiezioni annidate su tre livelli) —
/// i volumi in gioco sono quelli di un pannello di amministrazione (decine/centinaia di righe, non
/// milioni), quindi la semplicità/robustezza conta più di risparmiare qualche round-trip.
/// </summary>
public class SuperAdminRepository(GestiSoftDbContext db) : ISuperAdminRepository
{
    public async Task<DashboardSuperAdminInfo> GetDashboardAsync(CancellationToken cancellationToken)
    {
        var clienti = await db.Clienti.AsNoTracking().OrderBy(c => c.RagioneSociale).ToListAsync(cancellationToken);
        // Senza OrderBy esplicito Postgres non garantisce un ordine stabile: un UPDATE (es. i toggle
        // "Servizi concessi") può far tornare le righe in un ordine diverso alla query successiva,
        // facendo "saltare" le strutture di posizione in UI — bug reale osservato e corretto qui.
        var strutture = await db.Strutture.AsNoTracking().OrderBy(s => s.CreatedAtUtc).ToListAsync(cancellationToken);
        var wubook = await db.WubookIntegrazioni.AsNoTracking().ToListAsync(cancellationToken);
        var impostazioni = await db.ImpostazioniStruttura.AsNoTracking().ToListAsync(cancellationToken);
        var utenti = await db.Utenti.AsNoTracking().OrderBy(u => u.Email).ToListAsync(cancellationToken);

        var wubookByStruttura = wubook.ToDictionary(w => w.StrutturaId);
        var impostazioniByStruttura = impostazioni.ToDictionary(i => i.StrutturaId);
        var struttureByCliente = strutture.ToLookup(s => s.ClienteId);
        var utentiByCliente = utenti.Where(u => u.ClienteId != null).ToLookup(u => u.ClienteId!.Value);
        var clientiById = clienti.ToDictionary(c => c.Id);

        var infoClienti = clienti.Select(c =>
        {
            var utentiCliente = utentiByCliente[c.Id];

            var infoStrutture = struttureByCliente[c.Id].Select(s =>
            {
                wubookByStruttura.TryGetValue(s.Id, out var w);
                impostazioniByStruttura.TryGetValue(s.Id, out var imp);

                return new StrutturaAdminInfo(
                    s.Id,
                    s.Nome,
                    s.Attivo,
                    s.DisattivataAtUtc,
                    w?.Attivo ?? false,
                    w?.UltimoErrore,
                    s.ScadenzaLicenza,
                    imp?.PoliziaStatoAttiva ?? false,
                    imp?.OsservatorioAttivo ?? false,
                    imp?.PayTouristAttivo ?? false,
                    s.WubookAbilitato,
                    s.AlloggiatiWebAbilitato,
                    s.OsservatorioAbilitato,
                    s.PayTouristAbilitato);
            }).ToList();

            return new ClienteAdminInfo(
                c.Id,
                c.RagioneSociale,
                c.PartitaIva,
                c.Attivo,
                c.CreatedAtUtc,
                c.QuotaAnnua,
                c.Note,
                utentiCliente.Count(),
                utentiCliente.Count(u => u.Attivo),
                infoStrutture);
        }).ToList();

        var infoUtenti = utenti.Select(u => new UtenteAdminInfo(
            u.Id,
            u.Email,
            u.Nome,
            u.Cognome,
            u.IsSuperAdmin,
            u.Attivo,
            u.ClienteId,
            u.ClienteId != null && clientiById.TryGetValue(u.ClienteId.Value, out var cliente) ? cliente.RagioneSociale : null,
            u.CreatedAtUtc,
            u.IsClienteAccount)).ToList();

        return new DashboardSuperAdminInfo(infoClienti, infoUtenti);
    }

    /// <summary>
    /// Cancellazione fisica riga per riga di ogni tabella tenant-scoped della Struttura, in un unico
    /// ordine che rispetta i vincoli FK "Restrict" del modello (verificato leggendo le FK reali di
    /// ogni entità, non per tentativi): i figli sempre prima dei genitori a cui puntano con Restrict
    /// (es. SettingRoom prima di SettingTipologia, OspiteRiga prima di Ospite, Ospite/Cauzione/
    /// OsservatorioInvio prima di Prenotazione). Tutto in un'unica transazione: se un vincolo FK non
    /// previsto blocca una cancellazione, l'intera operazione va in rollback, nessun dato parziale
    /// viene perso — mai un'eliminazione "a metà".
    /// </summary>
    public async Task EliminaStrutturaAsync(Guid strutturaId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        await db.ChiusureCamera.Where(x => x.StrutturaId == strutturaId).ExecuteDeleteAsync(cancellationToken);
        await db.RestrizioniSoggiornoCamera.Where(x => x.StrutturaId == strutturaId).ExecuteDeleteAsync(cancellationToken);
        await db.PrezziCamera.Where(x => x.StrutturaId == strutturaId).ExecuteDeleteAsync(cancellationToken);
        await db.OspitiRighe.Where(x => x.StrutturaId == strutturaId).ExecuteDeleteAsync(cancellationToken);
        await db.Cauzioni.Where(x => x.StrutturaId == strutturaId).ExecuteDeleteAsync(cancellationToken);
        await db.OsservatorioInvii.Where(x => x.StrutturaId == strutturaId).ExecuteDeleteAsync(cancellationToken);
        await db.Ospiti.Where(x => x.StrutturaId == strutturaId).ExecuteDeleteAsync(cancellationToken);
        await db.DatiFattura.Where(x => x.StrutturaId == strutturaId).ExecuteDeleteAsync(cancellationToken);
        await db.Prenotazioni.Where(x => x.StrutturaId == strutturaId).ExecuteDeleteAsync(cancellationToken);
        await db.Camere.Where(x => x.StrutturaId == strutturaId).ExecuteDeleteAsync(cancellationToken);
        // Tabelle ponte senza StrutturaId proprio: filtrate tramite l'entità Struttura-scoped a cui puntano.
        await db.OsservatorioAppartamentiTipologie
            .Where(x => db.OsservatorioAppartamenti.Any(a => a.Id == x.OsservatorioAppartamentoId && a.StrutturaId == strutturaId))
            .ExecuteDeleteAsync(cancellationToken);
        await db.PayTouristStruttureTipologie
            .Where(x => db.PayTouristStrutture.Any(p => p.Id == x.PayTouristStrutturaId && p.StrutturaId == strutturaId))
            .ExecuteDeleteAsync(cancellationToken);
        await db.TipologieCamera.Where(x => x.StrutturaId == strutturaId).ExecuteDeleteAsync(cancellationToken);
        await db.CanaliVendita.Where(x => x.StrutturaId == strutturaId).ExecuteDeleteAsync(cancellationToken);
        await db.DatiCliente.Where(x => x.StrutturaId == strutturaId).ExecuteDeleteAsync(cancellationToken);
        await db.DatiAziendali.Where(x => x.StrutturaId == strutturaId).ExecuteDeleteAsync(cancellationToken);
        await db.Spese.Where(x => x.StrutturaId == strutturaId).ExecuteDeleteAsync(cancellationToken);
        await db.Entrate.Where(x => x.StrutturaId == strutturaId).ExecuteDeleteAsync(cancellationToken);
        await db.ImpostazioniStruttura.Where(x => x.StrutturaId == strutturaId).ExecuteDeleteAsync(cancellationToken);
        await db.LogEventi.Where(x => x.StrutturaId == strutturaId).ExecuteDeleteAsync(cancellationToken);
        await db.WubookIntegrazioni.Where(x => x.StrutturaId == strutturaId).ExecuteDeleteAsync(cancellationToken);
        await db.AlloggiatiWebIntegrazioni.Where(x => x.StrutturaId == strutturaId).ExecuteDeleteAsync(cancellationToken);
        await db.OsservatorioAppartamenti.Where(x => x.StrutturaId == strutturaId).ExecuteDeleteAsync(cancellationToken);
        await db.PayTouristStrutture.Where(x => x.StrutturaId == strutturaId).ExecuteDeleteAsync(cancellationToken);
        await db.PayTouristIntegrazioni.Where(x => x.StrutturaId == strutturaId).ExecuteDeleteAsync(cancellationToken);
        await db.UtentiStrutture.Where(x => x.StrutturaId == strutturaId).ExecuteDeleteAsync(cancellationToken);
        await db.Strutture.Where(x => x.Id == strutturaId).ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }
}
