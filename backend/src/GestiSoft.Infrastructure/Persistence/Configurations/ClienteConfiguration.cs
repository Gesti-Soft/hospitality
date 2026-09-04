using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class ClienteConfiguration : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> builder)
    {
        builder.ToTable("clienti");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.RagioneSociale).IsRequired().HasMaxLength(200);
        builder.Property(c => c.PartitaIva).HasMaxLength(20);
        builder.Property(c => c.QuotaMensile).HasColumnType("numeric(10,2)");
        builder.Property(c => c.Note).HasColumnType("text");

        builder.HasMany(c => c.Strutture)
            .WithOne(s => s.Cliente)
            .HasForeignKey(s => s.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
