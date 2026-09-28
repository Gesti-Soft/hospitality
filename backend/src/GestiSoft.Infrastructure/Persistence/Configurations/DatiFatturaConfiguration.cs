using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class DatiFatturaConfiguration : IEntityTypeConfiguration<DatiFattura>
{
    public void Configure(EntityTypeBuilder<DatiFattura> builder)
    {
        builder.ToTable("dati_fattura");
        builder.ConfigureTenant();

        builder.Property(f => f.PrezzoTotale).HasPrecision(18, 2);
        builder.Property(f => f.ImportoTotale).HasPrecision(18, 2);
        builder.Property(f => f.Arrotondamento).HasPrecision(18, 2);
        builder.Property(f => f.ImpostaSoggiorno).HasPrecision(18, 2);
        builder.Property(f => f.ImportoBollo).HasPrecision(18, 2);

        // Numero progressivo univoco per anno, Struttura e serie: fatture e ricevute sono due
        // numerazioni distinte, e dentro ciascuna un numero non si ripete.
        builder.HasIndex(f => new { f.StrutturaId, f.Anno, f.TipoEmissione, f.Progressivo }).IsUnique();

        // Un documento non si cancella (la funzione non esiste): se un giorno esistesse, righe e
        // legami con le prenotazioni vanno via con lui.
        builder.HasMany(f => f.Righe).WithOne().HasForeignKey(r => r.DatiFatturaId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(f => f.Prenotazioni).WithOne().HasForeignKey(p => p.DatiFatturaId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class RigaFatturaConfiguration : IEntityTypeConfiguration<RigaFattura>
{
    public void Configure(EntityTypeBuilder<RigaFattura> builder)
    {
        builder.ToTable("righe_fattura");
        builder.ConfigureTenant();

        builder.Property(r => r.Descrizione).HasMaxLength(1000).IsRequired();
        builder.Property(r => r.Quantita).HasPrecision(18, 2);
        builder.Property(r => r.PrezzoUnitario).HasPrecision(18, 2);
        builder.Property(r => r.PrezzoTotale).HasPrecision(18, 2);

        builder.HasIndex(r => r.DatiFatturaId);
        // "Già fatturato?" si chiede per prenotazione e per servizio extra.
        builder.HasIndex(r => r.PrenotazioneId);
        builder.HasIndex(r => r.PrenotazioneServizioId);
    }
}

public class FatturaPrenotazioneConfiguration : IEntityTypeConfiguration<FatturaPrenotazione>
{
    public void Configure(EntityTypeBuilder<FatturaPrenotazione> builder)
    {
        builder.ToTable("fatture_prenotazioni");
        builder.ConfigureTenant();

        builder.HasIndex(p => new { p.DatiFatturaId, p.PrenotazioneId }).IsUnique();
        builder.HasIndex(p => p.PrenotazioneId);
        // Una prenotazione non si cancella mai davvero (si annulla).
        builder.HasOne<Prenotazione>().WithMany().HasForeignKey(p => p.PrenotazioneId).OnDelete(DeleteBehavior.Restrict);
    }
}
