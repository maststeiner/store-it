using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StoreIt.Application;

namespace StoreIt.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers persistence (ADR-003: PostgreSQL + EF Core). Called from the
    /// composition root only (ADR-001 amendment 2026-07-19).
    /// The connection string is resolved lazily inside the options callback — it is
    /// required only when the DbContext is actually created (runtime), so build-time
    /// OpenAPI generation (no DbContext instantiated) needs no database config.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<StoreItDbContext>(
            (serviceProvider, options) =>
            {
                var connectionString =
                    serviceProvider
                        .GetRequiredService<IConfiguration>()
                        .GetConnectionString("storeit")
                    ?? throw new InvalidOperationException(
                        "The 'storeit' connection string is not configured. Set the "
                            + "ConnectionStrings__storeit environment variable (12-factor); "
                            + "see docs/guidelines/test-guidelines.md."
                    );
                options.UseNpgsql(connectionString);
            }
        );
        services.AddScoped<IStorageRepository, StorageRepository>();
        return services;
    }
}
