using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class DocumentoFiscaleArchiviatoConfiguration : IEntityTypeConfiguration<DocumentoFiscaleArchiviato>
{
    public void Configure(EntityTypeBuilder<DocumentoFiscaleArchiviato> builder)
    {
        builder.ToTable("documenti_fiscali_archiviati");
        builder.Property(d => d.ClienteRagioneSociale).HasMaxLength(200).IsRequired();
        builder.Property(d => d.StrutturaNome).HasMaxLength(200).IsRequired();
        builder.Property(d => d.StrutturaPartitaIva).HasMaxLength(30);
        builder.Property(d => d.Numero).HasMaxLength(30).IsRequired();
        builder.Property(d => d.Controparte).HasMaxLength(200);
        builder.Property(d => d.Importo).HasPrecision(18, 2);
        builder.Property(d => d.NomeFile).HasMaxLength(100);
        // Nessuna chiave verso Cliente o Struttura: la copia deve sopravvivere a entrambi.
        builder.HasIndex(d => d.ClienteId);
        builder.HasIndex(d => d.Scadenza);
    }
}
