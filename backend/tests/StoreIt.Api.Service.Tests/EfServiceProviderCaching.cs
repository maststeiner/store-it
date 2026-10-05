using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StoreIt.Infrastructure;

namespace StoreIt.Api.Service.Tests;

/// <summary>
/// Test-host tweak for the <see cref="StoreItDbContext"/> options: switches off EF Core's
/// internal service-provider cache, so every test host builds its own EF model.
/// </summary>
/// <remarks>
/// EF Core caches its internal service provider (and with it the compiled model, i.e. the result of
/// <c>OnModelCreating</c> and the <c>IEntityTypeConfiguration</c>s) process-wide, keyed by the
/// service-affecting options only. A connection string does not count, so every
/// <c>WebApplicationFactory</c> host in one test process shares the first model that was built.
/// Stryker's Microsoft.Testing.Platform runner keeps the test process alive across mutants and has
/// no "static mutant" handling yet (stryker-net#3695): a mutant in the model configuration would
/// only ever be observed by the first host of the process and otherwise survive unseen. With the
/// cache off, the model is rebuilt per host (per fixture), which costs a few hundred milliseconds
/// per test class and nothing in production — this code runs in the test project only.
/// </remarks>
internal static class EfServiceProviderCaching
{
    public static IServiceCollection DisableEfServiceProviderCaching(this IServiceCollection services)
    {
        var descriptor = services.Single(d =>
            d.ServiceType == typeof(DbContextOptions<StoreItDbContext>)
        );
        services.Remove(descriptor);
        services.Add(
            new ServiceDescriptor(
                typeof(DbContextOptions<StoreItDbContext>),
                serviceProvider =>
                {
                    var original = (DbContextOptions<StoreItDbContext>)
                        descriptor.ImplementationFactory!(serviceProvider);
                    return new DbContextOptionsBuilder<StoreItDbContext>(original)
                        .EnableServiceProviderCaching(false)
                        .Options;
                },
                descriptor.Lifetime
            )
        );
        return services;
    }
}
