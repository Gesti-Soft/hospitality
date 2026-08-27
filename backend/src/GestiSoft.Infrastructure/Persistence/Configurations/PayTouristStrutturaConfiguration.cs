using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class PayTouristStrutturaConfiguration : IEntityTypeConfiguration<PayTouristStruttura>
{
    public void Configure(EntityTypeBuilder<PayTouristStruttura> builder)
    {
        builder.ToTable("paytourist_strutture");
        builder.ConfigureTenant();

        builder.HasMany(p => p.Tipologie)
            .WithOne(t => t.PayTouristStruttura)
            .HasForeignKey(t => t.PayTouristStrutturaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
