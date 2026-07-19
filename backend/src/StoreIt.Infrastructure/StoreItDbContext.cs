using Microsoft.EntityFrameworkCore;
using StoreIt.Domain;

namespace StoreIt.Infrastructure;

public class StoreItDbContext(DbContextOptions<StoreItDbContext> options) : DbContext(options)
{
    public DbSet<Storage> Storages => Set<Storage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StoreItDbContext).Assembly);
}
