using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class OspiteConfiguration : IEntityTypeConfiguration<Ospite>
{
    public void Configure(EntityTypeBuilder<Ospite> builder)
    {
        builder.ToTable("ospiti");
        builder.ConfigureTenant();

        builder.HasOne(o => o.Prenotazione)
            .WithOne(p => p.Ospite)
            .HasForeignKey<Ospite>(o => o.PrenotazioneId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasMany(o => o.Membri)
            .WithOne(m => m.Ospite)
            .HasForeignKey(m => m.OspiteId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
