using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class PayTouristPortaleAttivoConfiguration : IEntityTypeConfiguration<PayTouristPortaleAttivo>
{
    public void Configure(EntityTypeBuilder<PayTouristPortaleAttivo> builder)
    {
        builder.ToTable("paytourist_portali_attivi");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Nome).HasMaxLength(200).IsRequired();

        // Lo stesso portale non può comparire due volte nella stessa configurazione: sarebbe un
        // doppione senza significato, e l'abbinamento per nome ne troverebbe comunque uno solo.
        builder.HasIndex(p => new { p.PayTouristIntegrazioneId, p.IdPortale }).IsUnique();
    }
}
