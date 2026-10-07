using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class TrasmissioneFatturaConfiguration : IEntityTypeConfiguration<TrasmissioneFattura>
{
    public void Configure(EntityTypeBuilder<TrasmissioneFattura> builder)
    {
        builder.ToTable("trasmissioni_fattura");
        builder.ConfigureTenant();

        // Il tracciato vuole il nome in [a-zA-Z0-9_.]{9,50}. Unico in tutto il sistema: è il dato a cui
        // si agganciano le ricevute dello SdI, che arrivano senza nessun contesto di struttura.
        builder.Property(t => t.NomeFile).HasMaxLength(50).IsRequired();
        builder.HasIndex(t => t.NomeFile).IsUnique();
        builder.Property(t => t.ProgressivoInvio).HasMaxLength(10).IsRequired();
        builder.Property(t => t.CodiceTrasmittente).HasMaxLength(28).IsRequired();
        builder.Property(t => t.IdentificativoSdi).HasMaxLength(20);
        builder.HasIndex(t => t.IdentificativoSdi);
        builder.Property(t => t.Nota).HasMaxLength(4000);
        builder.Property(t => t.InviataDa).HasMaxLength(256);

        builder.HasIndex(t => t.DatiFatturaId);
        // Una fattura non si cancella; se sparisce con la sua struttura, i suoi file vanno con lei.
        builder.HasOne<DatiFattura>().WithMany().HasForeignKey(t => t.DatiFatturaId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ContatoreProgressivoInvioConfiguration : IEntityTypeConfiguration<ContatoreProgressivoInvio>
{
    public void Configure(EntityTypeBuilder<ContatoreProgressivoInvio> builder)
    {
        builder.ToTable("contatori_progressivo_invio");
        builder.HasKey(c => c.CodiceFiscale);
        builder.Property(c => c.CodiceFiscale).HasMaxLength(28);
    }
}
