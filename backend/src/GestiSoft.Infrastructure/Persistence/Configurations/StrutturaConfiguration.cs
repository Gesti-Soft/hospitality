using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class StrutturaConfiguration : IEntityTypeConfiguration<Struttura>
{
    public void Configure(EntityTypeBuilder<Struttura> builder)
    {
        builder.ToTable("strutture");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Nome).IsRequired().HasMaxLength(200);
        builder.HasIndex(s => s.ClienteId);
    }
}
