using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class RinnovoLicenzaConfiguration : IEntityTypeConfiguration<RinnovoLicenza>
{
    public void Configure(EntityTypeBuilder<RinnovoLicenza> builder)
    {
        builder.ToTable("rinnovi_licenza");
        builder.ConfigureTenant();

        builder.Property(r => r.Importo).HasColumnType("numeric(10,2)");
    }
}
