using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class WubookEventoRicevutoConfiguration : IEntityTypeConfiguration<WubookEventoRicevuto>
{
    public void Configure(EntityTypeBuilder<WubookEventoRicevuto> builder)
    {
        builder.ToTable("wubook_eventi_ricevuti");
        builder.ConfigureTenant();

        builder.Property(e => e.Lcode).IsRequired().HasMaxLength(50);

        // Un solo record per Rcode per Struttura: un evento rielaborato (retry dopo un errore)
        // aggiorna la riga esistente invece di accumularne una nuova ad ogni tentativo.
        builder.HasIndex(e => new { e.StrutturaId, e.Rcode }).IsUnique();
    }
}
