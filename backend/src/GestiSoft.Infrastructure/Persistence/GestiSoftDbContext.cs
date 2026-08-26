using GestiSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Persistence;

public class GestiSoftDbContext(DbContextOptions<GestiSoftDbContext> options) : DbContext(options)
{
    public DbSet<Cliente> Clienti => Set<Cliente>();

    public DbSet<Struttura> Strutture => Set<Struttura>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GestiSoftDbContext).Assembly);
    }
}
