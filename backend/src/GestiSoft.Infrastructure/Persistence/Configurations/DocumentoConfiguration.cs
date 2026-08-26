using GestiSoft.Domain.Entities.Riferimenti;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class DocumentoConfiguration : IEntityTypeConfiguration<Documento>
{
    public void Configure(EntityTypeBuilder<Documento> builder)
    {
        builder.ToTable("documenti_identita");
        builder.HasKey(d => d.Id);
        builder.HasIndex(d => d.Codice).IsUnique();
        builder.Property(d => d.Codice).IsRequired().HasMaxLength(10);
        builder.Property(d => d.Descrizione).IsRequired().HasMaxLength(200);
    }
}
