using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class PrenotazioneConfiguration : IEntityTypeConfiguration<Prenotazione>
{
    public void Configure(EntityTypeBuilder<Prenotazione> builder)
    {
        builder.ToTable("prenotazioni");
        builder.ConfigureTenant();

        builder.Property(p => p.ImportoPrenotazione).HasPrecision(18, 2);
        builder.Property(p => p.ImportoPagato).HasPrecision(18, 2);
        builder.Property(p => p.ImportoTotale).HasPrecision(18, 2);
        builder.Property(p => p.TotalTax).HasPrecision(18, 2);

        // Default true a livello DB (non solo nell'initializer C#, che EF non traduce da solo in
        // HasDefaultValue): senza questo, la migration avrebbe backfillato false sulle prenotazioni
        // esistenti, mostrandole a torto come "toggle spento" — stesso tipo di bug del defaultValue
        // già imparato più volte in questo progetto (vedi Struttura.Attivo/WubookAbilitato ecc.).
        builder.Property(p => p.TassaSoggiornoAttiva).HasDefaultValue(true);
        builder.Property(p => p.SpesePuliziaAttiva).HasDefaultValue(true);
        // A differenza degli altri: default false, si applica solo se l'ospite porta un animale.
        builder.Property(p => p.AnimaliAttiva).HasDefaultValue(false);
        builder.Property(p => p.CauzioneAttiva).HasDefaultValue(true);

        builder.HasIndex(p => new { p.StrutturaId, p.CheckIn, p.CheckOut });

        // Chiave di deduplica per il pull da Wubook — filtrato perché le prenotazioni non-OTA hanno IdPrenotazioneWubook null.
        builder.HasIndex(p => new { p.StrutturaId, p.IdPrenotazioneWubook })
            .IsUnique()
            .HasFilter("\"IdPrenotazioneWubook\" IS NOT NULL");

        builder.HasOne(p => p.Camera)
            .WithMany()
            .HasForeignKey(p => p.CameraId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
