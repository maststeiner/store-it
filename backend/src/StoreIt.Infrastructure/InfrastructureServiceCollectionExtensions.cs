using Microsoft.AspNetCore.DataProtection;
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
    /// <paramref name="persistDataProtectionKeys"/> is false only in that build-time
    /// context: the key ring is preloaded at host start, which needs the database.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        bool persistDataProtectionKeys = true
    )
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
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IInvitationRepository, InvitationRepository>();
        services.AddSingleton<IInvitationTokens, InvitationTokens>();
        services.AddScoped<IUsageStatisticsQuery, UsageStatisticsQuery>(); // SPEC-008

        // Data-protection key ring in the database (table data_protection_keys). Without a
        // persisted store the keys live in the container file system and are lost whenever
        // compose recreates the container — i.e. on every release — which invalidates every
        // session and CSRF cookie at once. The application name pins the key-ring
        // discriminator; by default it is the content-root path, which would silently
        // change if the image layout ever moved.
        // Keys are stored unencrypted (ASP.NET Core logs a warning at startup): the
        // database is reachable from the compose network only and already holds the data
        // the keys protect. ProtectKeysWithCertificate is the documented step up if that
        // threat model changes.
        if (persistDataProtectionKeys)
        {
            services
                .AddDataProtection()
                .SetApplicationName("store-it")
                .PersistKeysToDbContext<StoreItDbContext>();
        }
        return services;
    }
}
