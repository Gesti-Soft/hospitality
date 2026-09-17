using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Entities.Riferimenti;
using GestiSoft.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GestiSoft.Infrastructure.Persistence;

public class GestiSoftDbContext(DbContextOptions<GestiSoftDbContext> options, CredenzialiProtector credenziali) : DbContext(options)
{
    public DbSet<Cliente> Clienti => Set<Cliente>();

    public DbSet<Struttura> Strutture => Set<Struttura>();

    public DbSet<SettingTipologia> TipologieCamera => Set<SettingTipologia>();

    public DbSet<SettingRoom> Camere => Set<SettingRoom>();

    public DbSet<GestionePrezzo> PrezziCamera => Set<GestionePrezzo>();

    public DbSet<SettingAgenzia> CanaliVendita => Set<SettingAgenzia>();

    public DbSet<Prenotazione> Prenotazioni => Set<Prenotazione>();

    public DbSet<Ospite> Ospiti => Set<Ospite>();

    public DbSet<OspiteRiga> OspitiRighe => Set<OspiteRiga>();

    public DbSet<Spesa> Spese => Set<Spesa>();

    public DbSet<Entrata> Entrate => Set<Entrata>();

    public DbSet<Cauzione> Cauzioni => Set<Cauzione>();

    public DbSet<DatiAziendali> DatiAziendali => Set<DatiAziendali>();

    public DbSet<DatiCliente> DatiCliente => Set<DatiCliente>();

    public DbSet<DatiFattura> DatiFattura => Set<DatiFattura>();

    public DbSet<Comune> Comuni => Set<Comune>();

    public DbSet<Stato> Stati => Set<Stato>();

    public DbSet<Documento> DocumentiIdentita => Set<Documento>();

    public DbSet<TipoAlloggiato> TipiAlloggiato => Set<TipoAlloggiato>();

    public DbSet<Utente> Utenti => Set<Utente>();

    public DbSet<UtenteStruttura> UtentiStrutture => Set<UtenteStruttura>();

    public DbSet<CodiceRecuperoUtente> CodiciRecuperoUtente => Set<CodiceRecuperoUtente>();

    public DbSet<DispositivoFidato> DispositiviFidati => Set<DispositivoFidato>();

    public DbSet<ImpostazioniStruttura> ImpostazioniStruttura => Set<ImpostazioniStruttura>();

    public DbSet<LogEvento> LogEventi => Set<LogEvento>();

    public DbSet<WubookIntegrazione> WubookIntegrazioni => Set<WubookIntegrazione>();

    public DbSet<WubookEventoRicevuto> WubookEventiRicevuti => Set<WubookEventoRicevuto>();

    public DbSet<RinnovoLicenza> RinnoviLicenza => Set<RinnovoLicenza>();

    public DbSet<ImpostazioniGlobali> ImpostazioniGlobali => Set<ImpostazioniGlobali>();

    public DbSet<AlloggiatiWebIntegrazione> AlloggiatiWebIntegrazioni => Set<AlloggiatiWebIntegrazione>();

    public DbSet<OsservatorioAppartamento> OsservatorioAppartamenti => Set<OsservatorioAppartamento>();

    public DbSet<OsservatorioAppartamentoTipologia> OsservatorioAppartamentiTipologie => Set<OsservatorioAppartamentoTipologia>();

    public DbSet<OsservatorioInvio> OsservatorioInvii => Set<OsservatorioInvio>();

    public DbSet<PayTouristIntegrazione> PayTouristIntegrazioni => Set<PayTouristIntegrazione>();

    public DbSet<PayTouristPortaleAttivo> PayTouristPortaliAttivi => Set<PayTouristPortaleAttivo>();

    public DbSet<PayTouristStruttura> PayTouristStrutture => Set<PayTouristStruttura>();

    public DbSet<PayTouristStrutturaTipologia> PayTouristStruttureTipologie => Set<PayTouristStrutturaTipologia>();

    public DbSet<ChiusuraCamera> ChiusureCamera => Set<ChiusuraCamera>();

    public DbSet<RestrizioneSoggiornoCamera> RestrizioniSoggiornoCamera => Set<RestrizioneSoggiornoCamera>();

    public DbSet<Notifica> Notifiche => Set<Notifica>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GestiSoftDbContext).Assembly);

        // Npgsql accetta solo DateTime con Kind=Utc per le colonne "timestamp with time zone"
        // (il default per DateTime in EF Core/Npgsql). I DateTime che arrivano dal JSON delle
        // request HTTP hanno Kind=Unspecified (nessun offset nel payload) e altrimenti farebbero
        // fallire ogni insert/update con ArgumentException. Applicato una sola volta qui invece
        // che con SpecifyKind sparso in ogni service: i valori sono comunque sempre date/orari
        // "locali alla struttura", non convertiamo il valore, lo ritagghiamo solo come UTC.
        var utcConverter = new ValueConverter<DateTime, DateTime>(
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        var nullableUtcConverter = new ValueConverter<DateTime?, DateTime?>(
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                {
                    property.SetValueConverter(utcConverter);
                }
                else if (property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(nullableUtcConverter);
                }
            }
        }

        // Credenziali dei servizi esterni cifrate a riposo: sono le chiavi d'accesso a portali della
        // PA, e in chiaro finivano in ogni backup e in ogni dump scaricato per assistenza. La
        // conversione sta qui e non nelle singole Configuration perché quelle vengono create per
        // riflessione, senza accesso al protector (vedi CredenzialiProtector).
        var credenzialeConverter = new ValueConverter<string?, string?>(
            v => credenziali.Proteggi(v),
            v => credenziali.Leggi(v));

        modelBuilder.Entity<PayTouristIntegrazione>().Property(p => p.Token).HasConversion(credenzialeConverter);
        modelBuilder.Entity<AlloggiatiWebIntegrazione>().Property(p => p.Password).HasConversion(credenzialeConverter);
        modelBuilder.Entity<AlloggiatiWebIntegrazione>().Property(p => p.WsKey).HasConversion(credenzialeConverter);
        modelBuilder.Entity<OsservatorioAppartamento>().Property(p => p.Password).HasConversion(credenzialeConverter);
        modelBuilder.Entity<ImpostazioniGlobali>().Property(p => p.TokenWubook).HasConversion(credenzialeConverter);
        modelBuilder.Entity<WubookIntegrazione>().Property(p => p.GestisoftToken).HasConversion(credenzialeConverter);
    }
}
