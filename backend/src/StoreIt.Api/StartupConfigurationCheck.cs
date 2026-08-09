using System.Reflection;

namespace StoreIt.Api;

/// <summary>
/// Rejects a misconfigured process before it starts serving (SPEC-004 AC-02/AC-09).
/// <para>
/// Without this the first request would be the one to discover a missing connection string,
/// because <c>AddInfrastructure</c> resolves it lazily. In a container that is the wrong way
/// round: a misconfigured instance should fail immediately and visibly, not come up and
/// serve errors.
/// </para>
/// <para>
/// This runs before the host is built, and reports rather than throws. Both were measured,
/// not assumed: as a hosted service the failure surfaced only during host startup, and an
/// unhandled exception did not end the process — the container was still alive 60 seconds
/// after printing "Unhandled exception". A checked result plus an explicit exit code makes
/// the failure immediate and deterministic, and spares the operator a stack trace.
/// </para>
/// </summary>
internal static class StartupConfigurationCheck
{
    /// <summary>
    /// True while the build-time OpenAPI generator is driving the entry point.
    /// <para>
    /// <c>GetDocument.Insider</c> (Microsoft.Extensions.ApiDescription.Server) runs this
    /// program to build and start the host, with no database and legitimately none needed —
    /// which is why the connection string is resolved lazily in the first place. During
    /// generation the process entry assembly is the tool rather than <c>StoreIt.Api</c>,
    /// and that is the reliable way to tell the two contexts apart.
    /// </para>
    /// </summary>
    private static bool IsBuildTimeDocumentGeneration =>
        (Assembly.GetEntryAssembly()?.GetName().Name ?? string.Empty).StartsWith(
            "GetDocument",
            StringComparison.Ordinal
        );

    /// <summary>
    /// Checks the configuration required to serve traffic. Returns <c>false</c> and a
    /// human-readable reason instead of throwing: a missing environment variable is an
    /// expected operator error, and the caller turns it into a plain message plus a
    /// non-zero exit code rather than a stack trace (AC-09).
    /// </summary>
    public static bool TryValidate(
        IConfiguration configuration,
        IHostEnvironment environment,
        out string? error
    )
    {
        error = null;

        if (IsBuildTimeDocumentGeneration)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("storeit")))
        {
            error =
                "Configuration error: the 'storeit' connection string is not set, so the "
                + "API cannot start. Set the ConnectionStrings__storeit environment "
                + "variable (12-factor). For the container stack, copy .env.example to "
                + $".env and set POSTGRES_PASSWORD. Environment: {environment.EnvironmentName}.";
            return false;
        }

        return true;
    }
}
