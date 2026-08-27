using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class GestionePrezzoConfiguration : IEntityTypeConfiguration<GestionePrezzo>
{
    public void Configure(EntityTypeBuilder<GestionePrezzo> builder)
    {
        builder.ToTable("prezzi_camera");
        builder.ConfigureTenant();

        builder.Property(p => p.PrezzoPerNotte).HasPrecision(18, 2);
        builder.HasIndex(p => new { p.CameraId, p.DataInizio, p.DataFine });
        builder.HasIndex(p => new { p.TipologiaId, p.DataInizio, p.DataFine });

        builder.HasOne(p => p.Camera)
            .WithMany()
            .HasForeignKey(p => p.CameraId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Tipologia)
            .WithMany()
            .HasForeignKey(p => p.TipologiaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
