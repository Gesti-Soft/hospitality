using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class StrutturaConfiguration : IEntityTypeConfiguration<Struttura>
{
    public void Configure(EntityTypeBuilder<Struttura> builder)
    {
        builder.ToTable("strutture");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Nome).IsRequired().HasMaxLength(200);
        builder.HasIndex(s => s.ClienteId);

        // Default true a livello DB (non solo C#): serve a backfillare correttamente le Strutture
        // già esistenti quando questa colonna è stata aggiunta — non devono ritrovarsi "eliminate"
        // di colpo.
        builder.Property(s => s.Attivo).HasDefaultValue(true);

        // Default false a livello DB (non solo C#): serve a backfillare le righe già esistenti al
        // momento dell'aggiunta di queste colonne — vedi la migration dedicata, che le popola dal
        // valore che aveva il Cliente prima dello spostamento, non le azzera.
        builder.Property(s => s.WubookAbilitato).HasDefaultValue(false);
        builder.Property(s => s.AlloggiatiWebAbilitato).HasDefaultValue(false);
        builder.Property(s => s.OsservatorioAbilitato).HasDefaultValue(false);
        builder.Property(s => s.PayTouristAbilitato).HasDefaultValue(false);
    }
}
