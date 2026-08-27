using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class PrenotazioneConfiguration : IEntityTypeConfiguration<Prenotazione>
{
    public void Configure(EntityTypeBuilder<Prenotazione> builder)
    {
        builder.ToTable("prenotazioni");
        builder.ConfigureTenant();

        builder.Property(p => p.ImportoPrenotazione).HasPrecision(18, 2);
        builder.Property(p => p.ImportoPagato).HasPrecision(18, 2);
        builder.Property(p => p.ImportoTotale).HasPrecision(18, 2);
        builder.Property(p => p.TotalTax).HasPrecision(18, 2);

        builder.HasIndex(p => new { p.StrutturaId, p.CheckIn, p.CheckOut });

        // Chiave di deduplica per il pull da Wubook — filtrato perché le prenotazioni non-OTA hanno IdPrenotazioneWubook null.
        builder.HasIndex(p => new { p.StrutturaId, p.IdPrenotazioneWubook })
            .IsUnique()
            .HasFilter("\"IdPrenotazioneWubook\" IS NOT NULL");

        builder.HasOne(p => p.Camera)
            .WithMany()
            .HasForeignKey(p => p.CameraId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
