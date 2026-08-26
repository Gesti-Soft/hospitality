using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class CauzioneConfiguration : IEntityTypeConfiguration<Cauzione>
{
    public void Configure(EntityTypeBuilder<Cauzione> builder)
    {
        builder.ToTable("cauzioni");
        builder.ConfigureTenant();

        builder.Property(c => c.ImportoCauzione).HasPrecision(18, 2);

        builder.HasOne(c => c.Prenotazione)
            .WithMany()
            .HasForeignKey(c => c.PrenotazioneId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
