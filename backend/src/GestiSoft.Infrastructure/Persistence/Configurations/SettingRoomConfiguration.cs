using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class SettingRoomConfiguration : IEntityTypeConfiguration<SettingRoom>
{
    public void Configure(EntityTypeBuilder<SettingRoom> builder)
    {
        builder.ToTable("camere");
        builder.ConfigureTenant();

        builder.Property(r => r.Nome).IsRequired().HasMaxLength(200);
        builder.HasIndex(r => new { r.StrutturaId, r.Nome }).IsUnique();

        builder.HasOne(r => r.Tipologia)
            .WithMany()
            .HasForeignKey(r => r.TipologiaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
