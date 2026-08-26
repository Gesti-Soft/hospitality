using GestiSoft.Domain.Entities.Riferimenti;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class TipoAlloggiatoConfiguration : IEntityTypeConfiguration<TipoAlloggiato>
{
    public void Configure(EntityTypeBuilder<TipoAlloggiato> builder)
    {
        builder.ToTable("tipi_alloggiato");
        builder.HasKey(t => t.Id);
        builder.HasIndex(t => t.Codice).IsUnique();
        builder.Property(t => t.Codice).IsRequired().HasMaxLength(10);
        builder.Property(t => t.Descrizione).IsRequired().HasMaxLength(200);
    }
}
