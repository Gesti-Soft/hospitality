using GestiSoft.Infrastructure.Persistence;
using GestiSoft.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;

namespace GestiSoft.Infrastructure.Seed;

/// <summary>
/// Cifra le credenziali rimaste in chiaro da prima che la cifratura esistesse. Senza questo passo
/// resterebbero leggibili finché qualcuno non risalva quella configurazione — cioè, in pratica, per
/// sempre: sono valori che si impostano una volta e non si toccano più, e nel frattempo finiscono
/// in ogni backup e in ogni dump scaricato per assistenza.
/// <para>
/// Idempotente: riconosce dal prefisso <see cref="CredenzialiProtector.Prefisso"/> i valori già
/// cifrati e li lascia stare, quindi dall'avvio successivo non scrive più niente. Un valore in
/// chiaro si rilegge normalmente (il protector lo restituisce com'è), quindi basta marcare la
/// proprietà come modificata perché EF lo riscriva passando dalla cifratura.
/// </para>
/// </summary>
public class CredenzialiCifraturaSeeder(GestiSoftDbContext db, ILogger<CredenzialiCifraturaSeeder> logger)
{
    /// <summary>Filtro "non ancora cifrato": passato come parametro SQL, non concatenato nella query.</summary>
    private static readonly string PrefissoLike = CredenzialiProtector.Prefisso + "%";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var cifrate = 0;

        cifrate += await CifraAsync(
            db.Database.SqlQuery<Guid>($"select \"Id\" from paytourist_integrazioni where \"Token\" is not null and \"Token\" <> '' and \"Token\" not like {PrefissoLike}"),
            ids => db.PayTouristIntegrazioni.Where(x => ids.Contains(x.Id)),
            entry => entry.Property(x => x.Token).IsModified = true,
            cancellationToken);

        cifrate += await CifraAsync(
            db.Database.SqlQuery<Guid>($"select \"Id\" from alloggiati_web_integrazioni where \"Password\" is not null and \"Password\" <> '' and \"Password\" not like {PrefissoLike}"),
            ids => db.AlloggiatiWebIntegrazioni.Where(x => ids.Contains(x.Id)),
            entry => entry.Property(x => x.Password).IsModified = true,
            cancellationToken);

        cifrate += await CifraAsync(
            db.Database.SqlQuery<Guid>($"select \"Id\" from alloggiati_web_integrazioni where \"WsKey\" is not null and \"WsKey\" <> '' and \"WsKey\" not like {PrefissoLike}"),
            ids => db.AlloggiatiWebIntegrazioni.Where(x => ids.Contains(x.Id)),
            entry => entry.Property(x => x.WsKey).IsModified = true,
            cancellationToken);

        cifrate += await CifraAsync(
            db.Database.SqlQuery<Guid>($"select \"Id\" from osservatorio_appartamenti where \"Password\" is not null and \"Password\" <> '' and \"Password\" not like {PrefissoLike}"),
            ids => db.OsservatorioAppartamenti.Where(x => ids.Contains(x.Id)),
            entry => entry.Property(x => x.Password).IsModified = true,
            cancellationToken);

        cifrate += await CifraAsync(
            db.Database.SqlQuery<Guid>($"select \"Id\" from impostazioni_globali where \"TokenWubook\" is not null and \"TokenWubook\" <> '' and \"TokenWubook\" not like {PrefissoLike}"),
            ids => db.ImpostazioniGlobali.Where(x => ids.Contains(x.Id)),
            entry => entry.Property(x => x.TokenWubook).IsModified = true,
            cancellationToken);

        cifrate += await CifraAsync(
            db.Database.SqlQuery<Guid>($"select \"Id\" from wubook_integrazioni where \"GestisoftToken\" is not null and \"GestisoftToken\" <> '' and \"GestisoftToken\" not like {PrefissoLike}"),
            ids => db.WubookIntegrazioni.Where(x => ids.Contains(x.Id)),
            entry => entry.Property(x => x.GestisoftToken).IsModified = true,
            cancellationToken);

        if (cifrate > 0)
        {
            logger.LogInformation("Cifratura credenziali: {Quantita} valori riscritti cifrati (erano in chiaro).", cifrate);
        }
    }

    /// <param name="idsInChiaro">Query grezza: è l'unico modo per sapere com'è scritto il valore sul database, perché passando da EF il convertitore lo avrebbe già decifrato.</param>
    private async Task<int> CifraAsync<T>(
        IQueryable<Guid> idsInChiaro,
        Func<IReadOnlyCollection<Guid>, IQueryable<T>> righeDaId,
        Action<EntityEntry<T>> marcaModificata,
        CancellationToken cancellationToken)
        where T : class
    {
        var ids = await idsInChiaro.ToListAsync(cancellationToken);
        if (ids.Count == 0)
        {
            return 0;
        }

        var righe = await righeDaId(ids).ToListAsync(cancellationToken);
        foreach (var riga in righe)
        {
            marcaModificata(db.Entry(riga));
        }

        await db.SaveChangesAsync(cancellationToken);
        return righe.Count;
    }
}
