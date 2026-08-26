using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class SpesaConfiguration : IEntityTypeConfiguration<Spesa>
{
    public void Configure(EntityTypeBuilder<Spesa> builder)
    {
        builder.ToTable("spese");
        builder.ConfigureTenant();

        builder.Property(s => s.ImportoSpesa).HasPrecision(18, 2);
        builder.HasIndex(s => new { s.StrutturaId, s.DataSpesa });
    }
}
