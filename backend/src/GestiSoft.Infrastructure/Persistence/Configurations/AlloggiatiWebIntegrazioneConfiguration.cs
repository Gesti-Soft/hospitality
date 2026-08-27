using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class AlloggiatiWebIntegrazioneConfiguration : IEntityTypeConfiguration<AlloggiatiWebIntegrazione>
{
    public void Configure(EntityTypeBuilder<AlloggiatiWebIntegrazione> builder)
    {
        builder.ToTable("alloggiati_web_integrazioni");
        builder.ConfigureTenant();

        // Una sola configurazione Alloggiati Web per Struttura.
        builder.HasIndex(a => a.StrutturaId).IsUnique();
    }
}
