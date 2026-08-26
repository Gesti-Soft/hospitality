using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class OspiteRigaConfiguration : IEntityTypeConfiguration<OspiteRiga>
{
    public void Configure(EntityTypeBuilder<OspiteRiga> builder)
    {
        builder.ToTable("ospiti_righe");
        builder.ConfigureTenant();

        builder.HasOne(r => r.Camera)
            .WithMany()
            .HasForeignKey(r => r.CameraId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
