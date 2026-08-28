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
        var strutture = await db.Strutture.AsNoTracking().ToListAsync(cancellationToken);
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
                    w?.Attivo ?? false,
                    w?.UltimoErrore,
                    w?.CacheAggiornataAtUtc,
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
            u.CreatedAtUtc)).ToList();

        return new DashboardSuperAdminInfo(infoClienti, infoUtenti);
    }
}
