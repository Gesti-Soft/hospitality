using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class OsservatorioAppartamentoConfiguration : IEntityTypeConfiguration<OsservatorioAppartamento>
{
    public void Configure(EntityTypeBuilder<OsservatorioAppartamento> builder)
    {
        builder.ToTable("osservatorio_appartamenti");
        builder.ConfigureTenant();

        builder.HasMany(a => a.Tipologie)
            .WithOne(t => t.OsservatorioAppartamento)
            .HasForeignKey(t => t.OsservatorioAppartamentoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
