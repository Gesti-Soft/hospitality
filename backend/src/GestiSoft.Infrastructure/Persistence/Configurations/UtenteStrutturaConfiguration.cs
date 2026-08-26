using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class UtenteStrutturaConfiguration : IEntityTypeConfiguration<UtenteStruttura>
{
    public void Configure(EntityTypeBuilder<UtenteStruttura> builder)
    {
        builder.ToTable("utenti_strutture");
        builder.HasKey(us => us.Id);

        // Un solo ruolo/set di permessi per coppia Utente/Struttura.
        builder.HasIndex(us => new { us.UtenteId, us.StrutturaId }).IsUnique();

        builder.HasOne(us => us.Struttura)
            .WithMany()
            .HasForeignKey(us => us.StrutturaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
