using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class ImpostazioniGlobaliConfiguration : IEntityTypeConfiguration<ImpostazioniGlobali>
{
    public void Configure(EntityTypeBuilder<ImpostazioniGlobali> builder)
    {
        builder.ToTable("impostazioni_globali");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.TokenWubook).HasMaxLength(200);
    }
}
