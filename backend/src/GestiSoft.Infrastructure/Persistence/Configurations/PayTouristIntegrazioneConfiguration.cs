using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class PayTouristIntegrazioneConfiguration : IEntityTypeConfiguration<PayTouristIntegrazione>
{
    public void Configure(EntityTypeBuilder<PayTouristIntegrazione> builder)
    {
        builder.ToTable("paytourist_integrazioni");
        builder.ConfigureTenant();

        // Una sola configurazione PayTourist per Struttura.
        builder.HasIndex(p => p.StrutturaId).IsUnique();

        builder.HasMany(p => p.PortaliAttivi)
            .WithOne(x => x.Integrazione)
            .HasForeignKey(x => x.PayTouristIntegrazioneId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
