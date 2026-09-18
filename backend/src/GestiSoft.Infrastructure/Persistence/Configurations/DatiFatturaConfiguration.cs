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
        builder.Property(f => f.ImpostaSoggiorno).HasPrecision(18, 2);
        builder.Property(f => f.ImportoBollo).HasPrecision(18, 2);

        // Numero progressivo univoco per anno, Struttura e serie: fatture e ricevute sono due
        // numerazioni distinte, e dentro ciascuna un numero non si ripete.
        builder.HasIndex(f => new { f.StrutturaId, f.Anno, f.TipoEmissione, f.Progressivo }).IsUnique();
    }
}
