using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class ServizioStrutturaConfiguration : IEntityTypeConfiguration<ServizioStruttura>
{
    public void Configure(EntityTypeBuilder<ServizioStruttura> builder)
    {
        builder.ToTable("servizi_struttura");
        builder.ConfigureTenant();

        builder.Property(s => s.Nome).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Codice).HasMaxLength(30);
        builder.Property(s => s.Prezzo).HasPrecision(18, 2);
    }
}
