using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class PayTouristStrutturaTipologiaConfiguration : IEntityTypeConfiguration<PayTouristStrutturaTipologia>
{
    public void Configure(EntityTypeBuilder<PayTouristStrutturaTipologia> builder)
    {
        builder.ToTable("paytourist_strutture_tipologie");
        builder.HasKey(t => t.Id);

        builder.HasOne(t => t.Tipologia)
            .WithMany()
            .HasForeignKey(t => t.TipologiaId)
            .OnDelete(DeleteBehavior.Restrict);

        // Una tipologia è instradata su al più una struttura PayTourist per la stessa struttura.
        builder.HasIndex(t => new { t.PayTouristStrutturaId, t.TipologiaId }).IsUnique();
    }
}
