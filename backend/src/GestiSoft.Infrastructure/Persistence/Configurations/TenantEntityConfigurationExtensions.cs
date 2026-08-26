using GestiSoft.Domain.Common;
using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestiSoft.Infrastructure.Persistence.Configurations;

/// <summary>
/// Applica lo scoping multi-tenant comune a tutte le entità operative: FK verso Struttura
/// (Restrict: non si cancella una Struttura finché ha dati collegati) + indice su StrutturaId,
/// usato in ogni query per filtrare i dati del Cliente/Struttura corrente.
/// </summary>
public static class TenantEntityConfigurationExtensions
{
    public static void ConfigureTenant<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : TenantEntity
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.StrutturaId);
        builder.HasOne<Struttura>()
            .WithMany()
            .HasForeignKey(e => e.StrutturaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
