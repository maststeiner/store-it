using System.Text.Json;
using System.Xml.Linq;

namespace StoreIt.Api;

/// <summary>
/// SPEC-009 D3 / AC-09: generates <c>third-party-notices.json</c> from the published
/// dependency manifest (<c>StoreIt.Api.deps.json</c>) and the <c>.nuspec</c> files in the
/// NuGet package cache. Runs at publish time (Dockerfile) as
/// <c>dotnet StoreIt.Api.dll third-party-notices &lt;deps.json&gt; &lt;out&gt; [&lt;packages dir&gt;]</c>,
/// before the web host is even configured — so the list is derived from what is actually
/// shipped and is never maintained by hand. A package without any license information
/// fails the run (exit code 1): the existing license gate denies unknown licenses, and the
/// attribution must not silently skip one.
/// </summary>
internal static class ThirdPartyNoticesCommand
{
    public const string Name = "third-party-notices";

    /// <summary>
    /// Handles the command when <paramref name="args"/> starts with <see cref="Name"/>.
    /// Returns <c>false</c> (and does nothing) for a normal web-host start.
    /// </summary>
    public static bool TryHandle(string[] args, out int exitCode)
    {
        exitCode = 0;
        if (args.Length == 0 || !string.Equals(args[0], Name, StringComparison.Ordinal))
        {
            return false;
        }

        if (args.Length is < 3 or > 4)
        {
            Console.Error.WriteLine(
                $"usage: {Name} <StoreIt.Api.deps.json> <output.json> [<nuget packages dir>]"
            );
            exitCode = 2;
            return true;
        }

        var packagesDir = args.Length == 4 ? args[3] : DefaultPackagesDirectory();
        try
        {
            var components = Generate(File.ReadAllText(args[1]), packagesDir);
            File.WriteAllText(
                args[2],
                JsonSerializer.Serialize(components, ThirdPartyNotices.JsonOptions)
            );
            Console.WriteLine($"{components.Count} third-party components written to {args[2]}");
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
            Console.Error.WriteLine($"{Name}: {exception.Message}");
            exitCode = 1;
        }

        return true;
    }

    /// <summary>
    /// One component per <c>package</c> library in the manifest, sorted by name. Pure: reads
    /// only the given manifest text and the nuspec files under <paramref name="packagesDir"/>.
    /// </summary>
    public static IReadOnlyList<ThirdPartyComponent> Generate(string depsJson, string packagesDir)
    {
        using var manifest = JsonDocument.Parse(depsJson);
        if (!manifest.RootElement.TryGetProperty("libraries", out var libraries))
        {
            throw new InvalidOperationException(
                "the dependency manifest has no 'libraries' section"
            );
        }

        var components = new List<ThirdPartyComponent>();
        foreach (var library in libraries.EnumerateObject())
        {
            if (library.Value.GetProperty("type").GetString() != "package")
            {
                continue;
            }

            var separator = library.Name.IndexOf('/', StringComparison.Ordinal);
            if (separator <= 0)
            {
                throw new InvalidOperationException($"unexpected library key '{library.Name}'");
            }

            components.Add(
                ReadPackage(library.Name[..separator], library.Name[(separator + 1)..], packagesDir)
            );
        }

        return components.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static ThirdPartyComponent ReadPackage(string id, string version, string packagesDir)
    {
        var packageDir = Path.Combine(
            packagesDir,
            id.ToLowerInvariant(),
            version.ToLowerInvariant()
        );
        var nuspecPath = Path.Combine(packageDir, id.ToLowerInvariant() + ".nuspec");
        if (!File.Exists(nuspecPath))
        {
            throw new InvalidOperationException(
                $"{id} {version}: no .nuspec at {nuspecPath} — is the package cache restored?"
            );
        }

        var metadata = XDocument
            .Load(nuspecPath)
            .Root?.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "metadata");
        if (metadata is null)
        {
            throw new InvalidOperationException($"{id} {version}: {nuspecPath} has no <metadata>");
        }

        var (license, text) = ReadLicense(id, version, metadata, packageDir);
        return new ThirdPartyComponent(
            id,
            version,
            license,
            NonEmpty(Element(metadata, "copyright")),
            NonEmpty(Element(metadata, "projectUrl"))
                ?? NonEmpty(
                    metadata
                        .Elements()
                        .FirstOrDefault(e => e.Name.LocalName == "repository")
                        ?.Attribute("url")
                        ?.Value
                ),
            text
        );
    }

    /// <summary>
    /// SPDX expression (the normal case) · a license file shipped in the package (its text
    /// is embedded) · a legacy licenseUrl only (EC-04) · nothing → fail.
    /// </summary>
    private static (string License, string? Text) ReadLicense(
        string id,
        string version,
        XElement metadata,
        string packageDir
    )
    {
        var licenseElement = metadata.Elements().FirstOrDefault(e => e.Name.LocalName == "license");
        var licenseValue = NonEmpty(licenseElement?.Value);
        if (licenseElement is not null && licenseValue is not null)
        {
            if (licenseElement.Attribute("type")?.Value == "file")
            {
                // The declared file must exist inside the package directory: a missing file is
                // missing license information (fail, like the no-license case), and a path that
                // escapes the package would read something that is not this package's license.
                var root = Path.GetFullPath(packageDir) + Path.DirectorySeparatorChar;
                var licenseFile = Path.GetFullPath(
                    Path.Combine(packageDir, licenseValue.Replace('\\', '/'))
                );
                if (
                    !licenseFile.StartsWith(root, StringComparison.Ordinal)
                    || !File.Exists(licenseFile)
                )
                {
                    throw new InvalidOperationException(
                        $"{id} {version}: declared license file '{licenseValue}' not found in the package"
                    );
                }

                return ("See license text", File.ReadAllText(licenseFile));
            }

            return (licenseValue, null);
        }

        var licenseUrl = NonEmpty(Element(metadata, "licenseUrl"));
        if (licenseUrl is not null)
        {
            return (licenseUrl, null);
        }

        throw new InvalidOperationException(
            $"{id} {version} declares no license (neither <license> nor <licenseUrl>) — "
                + "add the information upstream or exclude the package before shipping it"
        );
    }

    private static string? Element(XElement metadata, string localName) =>
        metadata.Elements().FirstOrDefault(e => e.Name.LocalName == localName)?.Value;

    private static string? NonEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string DefaultPackagesDirectory() =>
        Environment.GetEnvironmentVariable("NUGET_PACKAGES")
        ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".nuget",
            "packages"
        );
}
