using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class DatiClienteConfiguration : IEntityTypeConfiguration<DatiCliente>
{
    public void Configure(EntityTypeBuilder<DatiCliente> builder)
    {
        builder.ToTable("dati_cliente");
        builder.ConfigureTenant();

        builder.HasMany(c => c.Fatture)
            .WithOne(f => f.Cliente)
            .HasForeignKey(f => f.DatiClienteId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
