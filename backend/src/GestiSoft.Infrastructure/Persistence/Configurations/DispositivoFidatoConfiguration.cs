using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class DispositivoFidatoConfiguration : IEntityTypeConfiguration<DispositivoFidato>
{
    public void Configure(EntityTypeBuilder<DispositivoFidato> builder)
    {
        builder.ToTable("dispositivi_fidati");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.TokenHash).IsRequired().HasMaxLength(200);
        // Il token arriva dal browser a ogni login: si cerca per hash, non per utente.
        builder.HasIndex(d => d.TokenHash).IsUnique();
        builder.HasIndex(d => d.UtenteId);

        builder.HasOne(d => d.Utente)
            .WithMany(u => u.DispositiviFidati)
            .HasForeignKey(d => d.UtenteId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
