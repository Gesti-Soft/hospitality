using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class TicketMessaggioConfiguration : IEntityTypeConfiguration<TicketMessaggio>
{
    public void Configure(EntityTypeBuilder<TicketMessaggio> builder)
    {
        builder.ToTable("ticket_assistenza_messaggi");
        builder.ConfigureTenant();

        builder.Property(m => m.Testo).IsRequired().HasMaxLength(5000);

        builder.HasIndex(m => new { m.TicketId, m.CreatedAtUtc });

        builder.HasOne(m => m.AutoreUtente)
            .WithMany()
            .HasForeignKey(m => m.AutoreUtenteId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(m => m.Allegati)
            .WithOne(a => a.Messaggio)
            .HasForeignKey(a => a.MessaggioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
