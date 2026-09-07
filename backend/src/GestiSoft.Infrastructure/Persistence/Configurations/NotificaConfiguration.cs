using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class NotificaConfiguration : IEntityTypeConfiguration<Notifica>
{
    public void Configure(EntityTypeBuilder<Notifica> builder)
    {
        builder.ToTable("notifiche");
        builder.ConfigureTenant();

        builder.Property(n => n.Titolo).IsRequired().HasMaxLength(200);
        builder.Property(n => n.Messaggio).IsRequired().HasMaxLength(1000);
        builder.Property(n => n.Canale).HasMaxLength(100);
        builder.Property(n => n.ChiaveDedup).HasMaxLength(200);

        // Interrogata ad ogni caricamento del pannello notifiche (lista + badge non lette).
        builder.HasIndex(n => new { n.StrutturaId, n.Stato, n.LettaAtUtc });

        // Deduplica delle notifiche periodiche (licenza/schedine) — filtrato perché le notifiche
        // puntuali (nuova prenotazione/cancellazione) hanno ChiaveDedup null.
        builder.HasIndex(n => new { n.StrutturaId, n.ChiaveDedup })
            .HasFilter("\"ChiaveDedup\" IS NOT NULL");

        builder.HasOne(n => n.Prenotazione)
            .WithMany()
            .HasForeignKey(n => n.PrenotazioneId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
