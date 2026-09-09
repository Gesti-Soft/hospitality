using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class SettingAgenziaConfiguration : IEntityTypeConfiguration<SettingAgenzia>
{
    public void Configure(EntityTypeBuilder<SettingAgenzia> builder)
    {
        builder.ToTable("canali_vendita");
        builder.ConfigureTenant();

        builder.Property(a => a.Descrizione).IsRequired().HasMaxLength(200);
        builder.Property(a => a.Colore).IsRequired().HasMaxLength(7);
        builder.HasIndex(a => new { a.StrutturaId, a.Descrizione }).IsUnique();
    }
}
