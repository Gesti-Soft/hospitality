using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class CodiceRecuperoUtenteConfiguration : IEntityTypeConfiguration<CodiceRecuperoUtente>
{
    public void Configure(EntityTypeBuilder<CodiceRecuperoUtente> builder)
    {
        builder.ToTable("codici_recupero_utente");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.CodiceHash).IsRequired().HasMaxLength(400);
        builder.HasIndex(c => c.UtenteId);

        builder.HasOne(c => c.Utente)
            .WithMany(u => u.CodiciRecupero)
            .HasForeignKey(c => c.UtenteId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
