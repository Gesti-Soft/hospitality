using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class SettingTipologiaConfiguration : IEntityTypeConfiguration<SettingTipologia>
{
    public void Configure(EntityTypeBuilder<SettingTipologia> builder)
    {
        builder.ToTable("tipologie_camera");
        builder.ConfigureTenant();

        builder.Property(t => t.TipologiaCamera).IsRequired().HasMaxLength(200);
        builder.HasIndex(t => new { t.StrutturaId, t.TipologiaCamera }).IsUnique();

        builder.Property(t => t.SpesePulizia).HasPrecision(18, 2);
        builder.Property(t => t.Animali).HasPrecision(18, 2);
        builder.Property(t => t.Cauzione).HasPrecision(18, 2);
        builder.Property(t => t.PrezzoDefault).HasPrecision(18, 2);
        builder.Property(t => t.Implemento).HasPrecision(18, 2);
        builder.Property(t => t.RiduzioneOspiteInMeno).HasPrecision(18, 2);
        // Default a livello DB: senza, le tipologie esistenti prenderebbero 0, che non è un valore dell'enum.
        builder.Property(t => t.TipoRiduzioneOspiteInMeno).HasDefaultValue(TipoVariazionePrezzo.Euro);
        builder.Property(t => t.TipoImplemento).HasDefaultValue(TipoVariazionePrezzo.Euro);

        builder.Property(t => t.CodiceCameraWubook).HasMaxLength(4);
        builder.Property(t => t.WubookSoloWoodoo).HasDefaultValue(false);
        builder.HasIndex(t => new { t.StrutturaId, t.IdCameraWubook });
    }
}
