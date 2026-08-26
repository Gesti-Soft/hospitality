using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class ImpostazioniStrutturaConfiguration : IEntityTypeConfiguration<ImpostazioniStruttura>
{
    public void Configure(EntityTypeBuilder<ImpostazioniStruttura> builder)
    {
        builder.ToTable("impostazioni_struttura");
        builder.ConfigureTenant();

        builder.Property(i => i.TassaSoggiornoPrezzo).HasPrecision(18, 2);

        // Una sola riga di impostazioni per Struttura.
        builder.HasIndex(i => i.StrutturaId).IsUnique();
    }
}
