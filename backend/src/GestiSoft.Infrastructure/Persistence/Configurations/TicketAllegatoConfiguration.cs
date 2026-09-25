using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class TicketAllegatoConfiguration : IEntityTypeConfiguration<TicketAllegato>
{
    public void Configure(EntityTypeBuilder<TicketAllegato> builder)
    {
        builder.ToTable("ticket_assistenza_allegati");
        builder.ConfigureTenant();

        builder.Property(a => a.NomeFile).IsRequired().HasMaxLength(120);
        builder.Property(a => a.ContentType).IsRequired().HasMaxLength(50);
        builder.Property(a => a.Percorso).IsRequired().HasMaxLength(200);

        builder.HasIndex(a => a.MessaggioId);
    }
}
