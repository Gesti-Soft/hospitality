using GestiSoft.Domain.Entities.Riferimenti;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class StatoConfiguration : IEntityTypeConfiguration<Stato>
{
    public void Configure(EntityTypeBuilder<Stato> builder)
    {
        builder.ToTable("stati");
        builder.HasKey(s => s.Id);
        builder.HasIndex(s => s.Codice).IsUnique();
        builder.Property(s => s.Descrizione).IsRequired().HasMaxLength(200);
    }
}
