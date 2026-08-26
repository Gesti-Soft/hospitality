using GestiSoft.Domain.Entities.Riferimenti;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class ComuneConfiguration : IEntityTypeConfiguration<Comune>
{
    public void Configure(EntityTypeBuilder<Comune> builder)
    {
        builder.ToTable("comuni");
        builder.HasKey(c => c.Id);
        builder.HasIndex(c => c.Codice).IsUnique();
        builder.Property(c => c.Descrizione).IsRequired().HasMaxLength(200);
    }
}
