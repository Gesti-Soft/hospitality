using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class UtenteConfiguration : IEntityTypeConfiguration<Utente>
{
    public void Configure(EntityTypeBuilder<Utente> builder)
    {
        builder.ToTable("utenti");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email).IsRequired().HasMaxLength(320);
        builder.HasIndex(u => u.Email).IsUnique();

        builder.HasOne(u => u.Cliente)
            .WithMany()
            .HasForeignKey(u => u.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(u => u.Strutture)
            .WithOne(us => us.Utente)
            .HasForeignKey(us => us.UtenteId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
