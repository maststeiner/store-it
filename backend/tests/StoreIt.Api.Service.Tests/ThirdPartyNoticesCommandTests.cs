namespace StoreIt.Api.Service.Tests;

/// <summary>
/// SPEC-009 AC-09 — the publish-time generator, against a synthetic dependency manifest and
/// package cache: one entry per shipped package, metadata from the nuspec, the three license
/// shapes (expression · file · legacy URL, EC-04) and the fail-on-missing rule.
/// </summary>
public sealed class ThirdPartyNoticesCommandTests : IDisposable
{
    private readonly string _packages = Path.Combine(
        Path.GetTempPath(),
        $"nuget-{Guid.NewGuid():N}"
    );

    public ThirdPartyNoticesCommandTests()
    {
        Directory.CreateDirectory(_packages);
    }

    public void Dispose() => Directory.Delete(_packages, recursive: true);

    private const string Manifest = """
        {
          "targets": {},
          "libraries": {
            "StoreIt.Application/1.0.0": { "type": "project", "serviceable": false, "sha512": "" },
            "Npgsql/10.0.3": { "type": "package", "serviceable": true, "sha512": "sha512-x", "path": "npgsql/10.0.3" },
            "Acme.Legacy/1.0.0": { "type": "package", "serviceable": true, "sha512": "sha512-y", "path": "acme.legacy/1.0.0" },
            "Acme.FileLicensed/2.0.0": { "type": "package", "serviceable": true, "sha512": "sha512-z", "path": "acme.filelicensed/2.0.0" }
          }
        }
        """;

    [Fact]
    public void Generate_ListsEveryShippedPackageWithItsLicense_SortedByName()
    {
        WriteNuspec(
            "Npgsql",
            "10.0.3",
            """
            <license type="expression">PostgreSQL</license>
            <projectUrl>https://github.com/npgsql/npgsql</projectUrl>
            <copyright>Copyright 2025 © The Npgsql Development Team</copyright>
            """
        );
        // EC-04: a legacy package with a licenseUrl only, and a repository url instead of projectUrl.
        WriteNuspec(
            "Acme.Legacy",
            "1.0.0",
            """
            <licenseUrl>https://example.org/acme/LICENSE</licenseUrl>
            <repository type="git" url="https://example.org/acme/legacy" />
            """
        );
        // A license shipped as a file inside the package: its text is embedded.
        WriteNuspec(
            "Acme.FileLicensed",
            "2.0.0",
            """
            <license type="file">LICENSE.txt</license>
            <projectUrl>https://example.org/acme/file</projectUrl>
            """
        );
        File.WriteAllText(
            Path.Combine(_packages, "acme.filelicensed", "2.0.0", "LICENSE.txt"),
            "Acme Public License — do what you like."
        );

        var components = ThirdPartyNoticesCommand.Generate(Manifest, _packages);

        Assert.Equal(
            ["Acme.FileLicensed", "Acme.Legacy", "Npgsql"],
            components.Select(c => c.Name)
        );
        Assert.DoesNotContain(
            components,
            c => c.Name.StartsWith("StoreIt", StringComparison.Ordinal)
        );

        var npgsql = components.Single(c => c.Name == "Npgsql");
        Assert.Equal("10.0.3", npgsql.Version);
        Assert.Equal("PostgreSQL", npgsql.License);
        Assert.Equal("Copyright 2025 © The Npgsql Development Team", npgsql.Copyright);
        Assert.Equal("https://github.com/npgsql/npgsql", npgsql.Url);
        Assert.Null(npgsql.Text);

        var legacy = components.Single(c => c.Name == "Acme.Legacy");
        Assert.Equal("https://example.org/acme/LICENSE", legacy.License);
        Assert.Equal("https://example.org/acme/legacy", legacy.Url);
        Assert.Null(legacy.Copyright);

        var fileLicensed = components.Single(c => c.Name == "Acme.FileLicensed");
        Assert.Equal("See license text", fileLicensed.License);
        Assert.Equal("Acme Public License — do what you like.", fileLicensed.Text);
    }

    [Fact]
    public void Generate_PackageWithoutAnyLicenseInformation_Fails()
    {
        WriteNuspec(
            "Npgsql",
            "10.0.3",
            "<projectUrl>https://github.com/npgsql/npgsql</projectUrl>"
        );
        WriteNuspec("Acme.Legacy", "1.0.0", "<licenseUrl>https://example.org/l</licenseUrl>");
        WriteNuspec("Acme.FileLicensed", "2.0.0", "<license type=\"expression\">MIT</license>");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ThirdPartyNoticesCommand.Generate(Manifest, _packages)
        );

        Assert.Contains("Npgsql 10.0.3", exception.Message, StringComparison.Ordinal);
        Assert.Contains("no license", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_PackageMissingFromTheCache_Fails()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ThirdPartyNoticesCommand.Generate(Manifest, _packages)
        );

        Assert.Contains(".nuspec", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TryHandle_OtherArguments_AreNotTheCommand()
    {
        Assert.False(ThirdPartyNoticesCommand.TryHandle([], out var exitCode));
        Assert.Equal(0, exitCode);
        Assert.False(
            ThirdPartyNoticesCommand.TryHandle(["--urls", "http://localhost:5000"], out _)
        );
    }

    [Fact]
    public void TryHandle_WritesTheFileAndReportsUsageErrors()
    {
        WriteNuspec("Npgsql", "10.0.3", "<license type=\"expression\">PostgreSQL</license>");
        WriteNuspec("Acme.Legacy", "1.0.0", "<licenseUrl>https://example.org/l</licenseUrl>");
        WriteNuspec("Acme.FileLicensed", "2.0.0", "<license type=\"expression\">MIT</license>");
        var deps = Path.Combine(_packages, "StoreIt.Api.deps.json");
        var output = Path.Combine(_packages, "third-party-notices.json");
        File.WriteAllText(deps, Manifest);

        Assert.True(
            ThirdPartyNoticesCommand.TryHandle(
                [ThirdPartyNoticesCommand.Name, deps, output, _packages],
                out var exitCode
            )
        );
        Assert.Equal(0, exitCode);
        var written = ThirdPartyNotices.Load(
            output,
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance
        );
        Assert.Equal(3, written.Count);

        Assert.True(
            ThirdPartyNoticesCommand.TryHandle([ThirdPartyNoticesCommand.Name], out exitCode)
        );
        Assert.Equal(2, exitCode);

        Assert.True(
            ThirdPartyNoticesCommand.TryHandle(
                [
                    ThirdPartyNoticesCommand.Name,
                    Path.Combine(_packages, "nope.json"),
                    output,
                    _packages,
                ],
                out exitCode
            )
        );
        Assert.Equal(1, exitCode);
    }

    private void WriteNuspec(string id, string version, string metadataXml)
    {
        var dir = Path.Combine(_packages, id.ToLowerInvariant(), version.ToLowerInvariant());
        Directory.CreateDirectory(dir);
        File.WriteAllText(
            Path.Combine(dir, id.ToLowerInvariant() + ".nuspec"),
            $"""
            <?xml version="1.0" encoding="utf-8"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <id>{id}</id>
                <version>{version}</version>
                {metadataXml}
              </metadata>
            </package>
            """
        );
    }
}
