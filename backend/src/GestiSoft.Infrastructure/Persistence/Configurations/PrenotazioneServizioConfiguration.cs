using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class PrenotazioneServizioConfiguration : IEntityTypeConfiguration<PrenotazioneServizio>
{
    public void Configure(EntityTypeBuilder<PrenotazioneServizio> builder)
    {
        builder.ToTable("prenotazioni_servizi");
        builder.ConfigureTenant();

        builder.Property(s => s.Nome).HasMaxLength(100).IsRequired();
        builder.Property(s => s.PrezzoUnitario).HasPrecision(18, 2);

        builder.HasOne<Prenotazione>().WithMany().HasForeignKey(s => s.PrenotazioneId).OnDelete(DeleteBehavior.Cascade);
        // Il servizio non si cancella mai davvero (vedi ServizioStruttura.Eliminato).
        builder.HasOne<ServizioStruttura>().WithMany().HasForeignKey(s => s.ServizioId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(s => s.AggiuntoDa).HasMaxLength(256);

        // Lo stesso servizio può ripetersi in giorni diversi: nessun vincolo di unicità.
        builder.HasIndex(s => s.PrenotazioneId);
    }
}
