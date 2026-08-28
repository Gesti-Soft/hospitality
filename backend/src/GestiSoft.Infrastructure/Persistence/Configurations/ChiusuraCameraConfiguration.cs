using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class ChiusuraCameraConfiguration : IEntityTypeConfiguration<ChiusuraCamera>
{
    public void Configure(EntityTypeBuilder<ChiusuraCamera> builder)
    {
        builder.ToTable("chiusure_camera");
        builder.ConfigureTenant();

        builder.HasIndex(c => new { c.CameraId, c.DataInizio, c.DataFine });

        builder.HasOne(c => c.Camera)
            .WithMany()
            .HasForeignKey(c => c.CameraId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
