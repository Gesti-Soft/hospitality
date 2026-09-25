using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("ticket_assistenza");
        builder.ConfigureTenant();

        builder.Property(t => t.Numero).UseIdentityByDefaultColumn();
        builder.HasIndex(t => t.Numero).IsUnique();

        builder.Property(t => t.Oggetto).IsRequired().HasMaxLength(150);

        // Elenco del pannello Super Admin: aperti prima, poi per ultimo messaggio.
        builder.HasIndex(t => new { t.Stato, t.UltimoMessaggioClienteAtUtc });

        builder.HasOne(t => t.AutoreUtente)
            .WithMany()
            .HasForeignKey(t => t.AutoreUtenteId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(t => t.Messaggi)
            .WithOne(m => m.Ticket)
            .HasForeignKey(m => m.TicketId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
