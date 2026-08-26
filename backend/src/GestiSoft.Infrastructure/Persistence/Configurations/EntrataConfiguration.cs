using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class EntrataConfiguration : IEntityTypeConfiguration<Entrata>
{
    public void Configure(EntityTypeBuilder<Entrata> builder)
    {
        builder.ToTable("entrate");
        builder.ConfigureTenant();

        builder.Property(e => e.ImportoEntrata).HasPrecision(18, 2);
        builder.HasIndex(e => new { e.StrutturaId, e.Data });
    }
}
