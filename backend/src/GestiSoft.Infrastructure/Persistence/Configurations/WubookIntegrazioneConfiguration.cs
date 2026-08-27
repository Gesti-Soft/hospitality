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

        // Una sola configurazione Wubook per Struttura.
        builder.HasIndex(w => w.StrutturaId).IsUnique();
    }
}
