using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace StoreIt.Api;

/// <summary>
/// SPEC-009 D1: the product release version as stamped into the build.
/// <see cref="Version"/> is the release tag (<c>v0.3.0</c>) or <c>dev</c> for a build that
/// was not stamped; <see cref="Revision"/> is the commit SHA or <c>null</c>.
/// </summary>
public sealed record BuildVersion(string Version, string? Revision)
{
    public const string Development = "dev";

    public static readonly BuildVersion Dev = new(Development, null);

    // The stamp is "<tag>+<sha>" (see Dockerfile): a SemVer tag with the leading v, optionally
    // a pre-release suffix, optionally the commit. Anything else — empty, an explicit "dev",
    // a bare "1.0.0" — is a development build.
    private static readonly Regex Stamp = new(
        @"^(?<version>v\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?)(?:\+(?<revision>[0-9A-Za-z.-]+))?$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        TimeSpan.FromMilliseconds(100)
    );

    /// <summary>Parses the build stamp into a <see cref="BuildVersion"/>.</summary>
    public static BuildVersion Parse(string? buildStamp)
    {
        if (string.IsNullOrWhiteSpace(buildStamp))
        {
            return Dev;
        }

        var match = Stamp.Match(buildStamp.Trim());
        if (!match.Success)
        {
            return Dev;
        }

        var revision = match.Groups["revision"];
        return new BuildVersion(
            match.Groups["version"].Value,
            revision.Success ? revision.Value : null
        );
    }
}

/// <summary>
/// One shipped third-party package (SPEC-009 D3/D4). <see cref="License"/> is the SPDX
/// expression where the package declares one, otherwise the license URL (EC-04);
/// <see cref="Text"/> carries a license text only when the package ships its own file.
/// </summary>
public sealed record ThirdPartyComponent(
    string Name,
    string Version,
    string License,
    string? Copyright,
    string? Url,
    string? Text
);

/// <summary>What <c>GET /api/v1/about</c> reports about the running API.</summary>
public interface IAboutInformation
{
    BuildVersion Version { get; }

    /// <summary>The .NET runtime the API runs on, e.g. <c>.NET 10.0.2</c>.</summary>
    string Runtime { get; }

    IReadOnlyList<ThirdPartyComponent> Components { get; }
}

/// <summary>
/// Reads the version from this assembly's informational version (stamped by the Dockerfile)
/// and the third-party notices from <c>third-party-notices.json</c> next to the binaries,
/// written by <see cref="ThirdPartyNoticesCommand"/> at publish time. Both are read once.
/// </summary>
public sealed class AboutInformation : IAboutInformation
{
    public const string NoticesFileName = "third-party-notices.json";

    public AboutInformation(ILogger<AboutInformation> logger)
        : this(
            ReadBuildStamp(typeof(AboutInformation).Assembly),
            Path.Combine(AppContext.BaseDirectory, NoticesFileName),
            logger
        ) { }

    internal AboutInformation(
        string? buildStamp,
        string noticesPath,
        ILogger<AboutInformation> logger
    )
    {
        Version = BuildVersion.Parse(buildStamp);
        Components = ThirdPartyNotices.Load(noticesPath, logger);
    }

    public BuildVersion Version { get; }

    public string Runtime => RuntimeInformation.FrameworkDescription;

    public IReadOnlyList<ThirdPartyComponent> Components { get; }

    /// <summary>The assembly's informational version — the Dockerfile's stamp — or <c>null</c>.</summary>
    public static string? ReadBuildStamp(Assembly assembly) =>
        assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
}

/// <summary>Reads the generated notices file; absent or unreadable → empty list (EC-01).</summary>
public static partial class ThirdPartyNotices
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public static IReadOnlyList<ThirdPartyComponent> Load(string path, ILogger logger)
    {
        if (!File.Exists(path))
        {
            NoticesFileMissing(logger, path);
            return [];
        }

        try
        {
            using var stream = File.OpenRead(path);
            var components = JsonSerializer.Deserialize<List<ThirdPartyComponent>>(
                stream,
                JsonOptions
            );
            return components ?? [];
        }
        catch (JsonException exception)
        {
            NoticesFileUnreadable(logger, exception, path);
            return [];
        }
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "No third-party notices at {Path} — development build, GET /api/v1/about lists no API components."
    )]
    private static partial void NoticesFileMissing(ILogger logger, string path);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Third-party notices at {Path} could not be read; GET /api/v1/about lists no API components."
    )]
    private static partial void NoticesFileUnreadable(
        ILogger logger,
        Exception exception,
        string path
    );
}
