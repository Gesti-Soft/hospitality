using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class DatiAziendaliConfiguration : IEntityTypeConfiguration<DatiAziendali>
{
    public void Configure(EntityTypeBuilder<DatiAziendali> builder)
    {
        builder.ToTable("dati_aziendali");
        builder.ConfigureTenant();

        // Un solo profilo fiscale emittente per Struttura.
        builder.HasIndex(d => d.StrutturaId).IsUnique();

        // Default true anche nel database: le strutture esistenti continuano a vedere l'imposta di
        // soggiorno proposta in fattura come prima.
        builder.Property(d => d.ImpostaSoggiornoInFattura).HasDefaultValue(true);
    }
}
