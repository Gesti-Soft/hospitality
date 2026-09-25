using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class TrattamentoStrutturaConfiguration : IEntityTypeConfiguration<TrattamentoStruttura>
{
    public void Configure(EntityTypeBuilder<TrattamentoStruttura> builder)
    {
        builder.ToTable("trattamenti_struttura");
        builder.ConfigureTenant();

        builder.Property(t => t.PrezzoPerPersona).HasPrecision(18, 2);
        builder.Property(t => t.PrezzoBambini).HasPrecision(18, 2);
        builder.Property(t => t.EsercizioConvenzionato).HasMaxLength(200);

        // Un listino per tipo di trattamento: due "Colazione" con prezzi diversi non avrebbero senso.
        builder.HasIndex(t => new { t.StrutturaId, t.Tipo }).IsUnique();
    }
}
