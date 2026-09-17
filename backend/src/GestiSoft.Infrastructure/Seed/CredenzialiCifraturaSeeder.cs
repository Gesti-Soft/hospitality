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
/// <para>
/// Fa anche da motore della <b>rotazione della chiave</b>: quando è configurata una chiave
/// precedente (<c>Credenziali:ChiaveCifraturaPrecedente</c>) non basta cifrare ciò che è in chiaro,
/// vanno riscritte tutte le credenziali, perché quelle cifrate con la chiave vecchia sono
/// indistinguibili dalle altre finché non si prova a leggerle. La riscrittura passa sempre dalla
/// chiave corrente, quindi ripetere l'avvio non fa danni e completa un'eventuale rotazione
/// interrotta a metà.
/// </para>
/// </summary>
public class CredenzialiCifraturaSeeder(GestiSoftDbContext db, CredenzialiProtector protector, ILogger<CredenzialiCifraturaSeeder> logger)
{
    /// <summary>Filtro "non ancora cifrato": passato come parametro SQL, non concatenato nella query.</summary>
    private static readonly string PrefissoLike = CredenzialiProtector.Prefisso + "%";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var rotazione = protector.RotazioneInCorso;
        var cifrate = 0;

        cifrate += await CifraAsync(
            db.Database.SqlQuery<Guid>($"select \"Id\" from paytourist_integrazioni where \"Token\" is not null and \"Token\" <> '' and ({rotazione} or \"Token\" not like {PrefissoLike})"),
            ids => db.PayTouristIntegrazioni.Where(x => ids.Contains(x.Id)),
            entry => entry.Property(x => x.Token).IsModified = true,
            cancellationToken);

        cifrate += await CifraAsync(
            db.Database.SqlQuery<Guid>($"select \"Id\" from alloggiati_web_integrazioni where \"Password\" is not null and \"Password\" <> '' and ({rotazione} or \"Password\" not like {PrefissoLike})"),
            ids => db.AlloggiatiWebIntegrazioni.Where(x => ids.Contains(x.Id)),
            entry => entry.Property(x => x.Password).IsModified = true,
            cancellationToken);

        cifrate += await CifraAsync(
            db.Database.SqlQuery<Guid>($"select \"Id\" from alloggiati_web_integrazioni where \"WsKey\" is not null and \"WsKey\" <> '' and ({rotazione} or \"WsKey\" not like {PrefissoLike})"),
            ids => db.AlloggiatiWebIntegrazioni.Where(x => ids.Contains(x.Id)),
            entry => entry.Property(x => x.WsKey).IsModified = true,
            cancellationToken);

        cifrate += await CifraAsync(
            db.Database.SqlQuery<Guid>($"select \"Id\" from osservatorio_appartamenti where \"Password\" is not null and \"Password\" <> '' and ({rotazione} or \"Password\" not like {PrefissoLike})"),
            ids => db.OsservatorioAppartamenti.Where(x => ids.Contains(x.Id)),
            entry => entry.Property(x => x.Password).IsModified = true,
            cancellationToken);

        cifrate += await CifraAsync(
            db.Database.SqlQuery<Guid>($"select \"Id\" from impostazioni_globali where \"TokenWubook\" is not null and \"TokenWubook\" <> '' and ({rotazione} or \"TokenWubook\" not like {PrefissoLike})"),
            ids => db.ImpostazioniGlobali.Where(x => ids.Contains(x.Id)),
            entry => entry.Property(x => x.TokenWubook).IsModified = true,
            cancellationToken);

        cifrate += await CifraAsync(
            db.Database.SqlQuery<Guid>($"select \"Id\" from wubook_integrazioni where \"GestisoftToken\" is not null and \"GestisoftToken\" <> '' and ({rotazione} or \"GestisoftToken\" not like {PrefissoLike})"),
            ids => db.WubookIntegrazioni.Where(x => ids.Contains(x.Id)),
            entry => entry.Property(x => x.GestisoftToken).IsModified = true,
            cancellationToken);

        if (cifrate > 0 && rotazione)
        {
            logger.LogInformation(
                "Rotazione chiave credenziali: {Quantita} valori riscritti con la chiave corrente. Completata la verifica, togliere Credenziali__ChiaveCifraturaPrecedente dalla configurazione.",
                cifrate);
        }
        else if (cifrate > 0)
        {
            logger.LogInformation("Cifratura credenziali: {Quantita} valori riscritti cifrati (erano in chiaro).", cifrate);
        }
        else if (rotazione)
        {
            logger.LogInformation("Rotazione chiave credenziali: nessuna credenziale da riscrivere. Si può togliere Credenziali__ChiaveCifraturaPrecedente dalla configurazione.");
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
