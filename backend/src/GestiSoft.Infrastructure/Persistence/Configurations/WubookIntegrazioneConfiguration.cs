using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class WubookIntegrazioneConfiguration : IEntityTypeConfiguration<WubookIntegrazione>
{
    public void Configure(EntityTypeBuilder<WubookIntegrazione> builder)
    {
        builder.ToTable("wubook_integrazioni");
        builder.ConfigureTenant();
        builder.Property(w => w.CodiceStruttura).HasMaxLength(50);
        builder.Property(w => w.UrlAvvisiPrecedente).HasMaxLength(500);

        // Una sola configurazione Wubook per Struttura.
        builder.HasIndex(w => w.StrutturaId).IsUnique();
    }
}
