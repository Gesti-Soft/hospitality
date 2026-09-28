using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class PagamentoPrenotazioneConfiguration : IEntityTypeConfiguration<PagamentoPrenotazione>
{
    public void Configure(EntityTypeBuilder<PagamentoPrenotazione> builder)
    {
        builder.ToTable("pagamenti_prenotazione");
        builder.ConfigureTenant();

        builder.Property(p => p.Importo).HasPrecision(18, 2);
        builder.Property(p => p.Nota).HasMaxLength(200);
        builder.Property(p => p.RegistratoDa).HasMaxLength(256);

        // Una prenotazione non si cancella mai davvero (si annulla): i suoi incassi restano.
        builder.HasOne<Prenotazione>().WithMany().HasForeignKey(p => p.PrenotazioneId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => p.PrenotazioneId);
        // La Cassa somma per struttura e data.
        builder.HasIndex(p => new { p.StrutturaId, p.Data });
    }
}
