using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class FasciaEtaSupplementoConfiguration : IEntityTypeConfiguration<FasciaEtaSupplemento>
{
    public void Configure(EntityTypeBuilder<FasciaEtaSupplemento> builder)
    {
        builder.ToTable("fasce_eta_supplemento");
        builder.ConfigureTenant();

        builder.Property(f => f.ImportoPerNotte).HasPrecision(18, 2);

        // Le fasce vivono e muoiono con la tipologia: senza, non significano niente.
        builder.HasOne<SettingTipologia>()
            .WithMany()
            .HasForeignKey(f => f.TipologiaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
