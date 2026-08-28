using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class RestrizioneSoggiornoCameraConfiguration : IEntityTypeConfiguration<RestrizioneSoggiornoCamera>
{
    public void Configure(EntityTypeBuilder<RestrizioneSoggiornoCamera> builder)
    {
        builder.ToTable("restrizioni_soggiorno_camera");
        builder.ConfigureTenant();

        builder.HasIndex(r => new { r.CameraId, r.DataInizio, r.DataFine });

        builder.HasOne(r => r.Camera)
            .WithMany()
            .HasForeignKey(r => r.CameraId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
