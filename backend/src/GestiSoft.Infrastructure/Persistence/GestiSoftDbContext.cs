using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Entities.Riferimenti;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Persistence;

public class GestiSoftDbContext(DbContextOptions<GestiSoftDbContext> options) : DbContext(options)
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

    public DbSet<ImpostazioniStruttura> ImpostazioniStruttura => Set<ImpostazioniStruttura>();

    public DbSet<LogEvento> LogEventi => Set<LogEvento>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GestiSoftDbContext).Assembly);
    }
}
