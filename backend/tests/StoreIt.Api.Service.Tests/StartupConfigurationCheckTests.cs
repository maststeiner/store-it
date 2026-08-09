using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace StoreIt.Api.Service.Tests;

/// <summary>
/// SPEC-004 AC-02/AC-09: a missing connection string must stop the API at startup with a
/// message naming the variable, rather than surfacing on the first request.
/// <para>
/// The container behaviour around this criterion — exit code 1, no restart loop — is a
/// property of the process and is verified by running the image. What is worth guarding in
/// the suite is the decision itself, because it is the piece a refactor can silently
/// invert: drop the check and everything still builds, boots and passes every other test,
/// while a misconfigured container goes back to serving errors instead of failing.
/// </para>
/// <para>
/// The other half of the boundary — build-time OpenAPI generation must keep working
/// *without* a database — needs no test of its own: every `dotnet build` in CI generates
/// the document with no `ConnectionStrings__storeit` set, and the API contract gate fails
/// on any drift. A regression there breaks the build directly.
/// </para>
/// </summary>
public sealed class StartupConfigurationCheckTests
{
    /// <summary>Minimal IHostEnvironment; only EnvironmentName is read by the check.</summary>
    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "StoreIt.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static TestEnvironment Environment(string name) => new() { EnvironmentName = name };

    private static IConfiguration Configuration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value))
            )
            .Build();

    [Fact]
    public void TryValidate_WithoutConnectionString_FailsAndNamesTheEnvironmentVariable()
    {
        var succeeded = StartupConfigurationCheck.TryValidate(
            Configuration(),
            Environment("Production"),
            out var error
        );

        Assert.False(succeeded);
        Assert.NotNull(error);
        // The operator has to learn *which* variable to set from the message alone.
        Assert.Contains("ConnectionStrings__storeit", error);
    }

    [Fact]
    public void TryValidate_WithBlankConnectionString_Fails()
    {
        // An empty environment variable is the common way to get this wrong — it is present
        // but useless, and must not be mistaken for configured.
        var succeeded = StartupConfigurationCheck.TryValidate(
            Configuration(("ConnectionStrings:storeit", "   ")),
            Environment("Production"),
            out var error
        );

        Assert.False(succeeded);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryValidate_WithConnectionString_Succeeds()
    {
        var succeeded = StartupConfigurationCheck.TryValidate(
            Configuration(("ConnectionStrings:storeit", "Host=db;Database=storeit")),
            Environment("Production"),
            out var error
        );

        Assert.True(succeeded);
        Assert.Null(error);
    }
}
