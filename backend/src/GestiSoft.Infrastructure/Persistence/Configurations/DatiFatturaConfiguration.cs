using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class DatiFatturaConfiguration : IEntityTypeConfiguration<DatiFattura>
{
    public void Configure(EntityTypeBuilder<DatiFattura> builder)
    {
        builder.ToTable("dati_fattura");
        builder.ConfigureTenant();

        builder.Property(f => f.Quantita).HasPrecision(18, 2);
        builder.Property(f => f.PrezzoUnitario).HasPrecision(18, 2);
        builder.Property(f => f.PrezzoTotale).HasPrecision(18, 2);
        builder.Property(f => f.ImportoTotale).HasPrecision(18, 2);
        builder.Property(f => f.Arrotondamento).HasPrecision(18, 2);

        // Numero progressivo univoco per anno e Struttura (le fatture non si duplicano per anno fiscale).
        builder.HasIndex(f => new { f.StrutturaId, f.Anno, f.Progressivo }).IsUnique();
    }
}
