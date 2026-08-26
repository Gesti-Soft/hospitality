using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

public class LogEventoConfiguration : IEntityTypeConfiguration<LogEvento>
{
    public void Configure(EntityTypeBuilder<LogEvento> builder)
    {
        builder.ToTable("log_eventi");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Messaggio).IsRequired();
        builder.Property(l => l.Origine).IsRequired().HasMaxLength(100);

        builder.HasIndex(l => new { l.ClienteId, l.StrutturaId, l.CreatedAtUtc });
        builder.HasIndex(l => l.CorrelationId);
    }
}
