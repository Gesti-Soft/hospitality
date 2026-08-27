using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class OsservatorioAppartamentoTipologiaConfiguration : IEntityTypeConfiguration<OsservatorioAppartamentoTipologia>
{
    public void Configure(EntityTypeBuilder<OsservatorioAppartamentoTipologia> builder)
    {
        builder.ToTable("osservatorio_appartamenti_tipologie");
        builder.HasKey(t => t.Id);

        builder.HasOne(t => t.Tipologia)
            .WithMany()
            .HasForeignKey(t => t.TipologiaId)
            .OnDelete(DeleteBehavior.Restrict);

        // Una tipologia è instradata su al più un appartamento per la stessa struttura.
        builder.HasIndex(t => new { t.OsservatorioAppartamentoId, t.TipologiaId }).IsUnique();
    }
}
