using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class OsservatorioInvioConfiguration : IEntityTypeConfiguration<OsservatorioInvio>
{
    public void Configure(EntityTypeBuilder<OsservatorioInvio> builder)
    {
        builder.ToTable("osservatorio_invii");
        builder.ConfigureTenant();

        builder.HasIndex(o => o.PrenotazioneId);
    }
}
