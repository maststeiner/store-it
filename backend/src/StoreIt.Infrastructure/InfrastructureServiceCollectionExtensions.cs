using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StoreIt.Application;

namespace StoreIt.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers persistence (ADR-003: PostgreSQL + EF Core). Called from the
    /// composition root only (ADR-001 amendment 2026-07-19).
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString
    )
    {
        services.AddDbContext<StoreItDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IStorageRepository, StorageRepository>();
        return services;
    }
}
