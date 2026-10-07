using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class RicevutaSdiConfiguration : IEntityTypeConfiguration<RicevutaSdi>
{
    public void Configure(EntityTypeBuilder<RicevutaSdi> builder)
    {
        builder.ToTable("ricevute_sdi");
        builder.Property(r => r.IdentificativoSdi).HasMaxLength(20);
        builder.Property(r => r.NomeFileFattura).HasMaxLength(50);
        builder.Property(r => r.NomeFile).HasMaxLength(100);
        builder.Property(r => r.Descrizione).HasMaxLength(4000);
        builder.HasIndex(r => r.StrutturaId);
        builder.HasIndex(r => r.TrasmissioneFatturaId);
        // Lo storico delle ricevute resta anche se il file a cui si riferisce sparisce con la sua struttura.
        builder.HasOne<TrasmissioneFattura>().WithMany().HasForeignKey(r => r.TrasmissioneFatturaId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class FileRicevutoSdiConfiguration : IEntityTypeConfiguration<FileRicevutoSdi>
{
    public void Configure(EntityTypeBuilder<FileRicevutoSdi> builder)
    {
        builder.ToTable("file_ricevuti_sdi");
        builder.Property(f => f.IdentificativoSdi).HasMaxLength(20).IsRequired();
        builder.Property(f => f.NomeFile).HasMaxLength(100).IsRequired();
        builder.Property(f => f.NomeFileMetadati).HasMaxLength(100);
        builder.Property(f => f.IdentificativoCessionario).HasMaxLength(40);
        builder.Property(f => f.Fornitore).HasMaxLength(200);
        builder.Property(f => f.Messaggio).HasMaxLength(1000);
        // La consegna ritentata dallo SdI non produce una seconda riga, né una seconda fattura.
        builder.HasIndex(f => new { f.IdentificativoSdi, f.Prova }).IsUnique();
        builder.HasIndex(f => f.StrutturaId);
    }
}

public class FatturaRicevutaConfiguration : IEntityTypeConfiguration<FatturaRicevuta>
{
    public void Configure(EntityTypeBuilder<FatturaRicevuta> builder)
    {
        builder.ToTable("fatture_ricevute");
        builder.ConfigureTenant();
        builder.Property(f => f.IdentificativoSdi).HasMaxLength(20).IsRequired();
        builder.Property(f => f.Fornitore).HasMaxLength(200).IsRequired();
        builder.Property(f => f.FornitorePartitaIva).HasMaxLength(30);
        builder.Property(f => f.FornitoreCodiceFiscale).HasMaxLength(30);
        builder.Property(f => f.TipoDocumento).HasMaxLength(10).IsRequired();
        builder.Property(f => f.Numero).HasMaxLength(20).IsRequired();
        builder.Property(f => f.Divisa).HasMaxLength(3).IsRequired();
        builder.Property(f => f.ImportoTotale).HasPrecision(18, 2);
        builder.Property(f => f.Imponibile).HasPrecision(18, 2);
        builder.Property(f => f.Imposta).HasPrecision(18, 2);
        builder.Property(f => f.Impronta).HasMaxLength(64).IsRequired();
        builder.HasIndex(f => new { f.StrutturaId, f.Impronta }).IsUnique();
        builder.HasIndex(f => new { f.StrutturaId, f.Data });
        builder.HasOne<FileRicevutoSdi>().WithMany().HasForeignKey(f => f.FileRicevutoSdiId).OnDelete(DeleteBehavior.Restrict);
    }
}
