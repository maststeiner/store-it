using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace StoreIt.Api.Service.Tests;

/// <summary>
/// SPEC-009 AC-04 / AC-09 — what the API says about itself, black-box over HTTP, plus the
/// version parsing and notices loading that the stamping path (AC-06) relies on.
/// </summary>
public sealed class AboutEndpointsTests(ApiTestFixture factory) : IClassFixture<ApiTestFixture>
{
    private const string About = "/api/v1/about";

    [Fact]
    public async Task About_Anonymous_Returns401()
    {
        var anonymous = factory.CreateClient();

        var response = await anonymous.GetAsync(About);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task About_SignedIn_ReportsDevBuildWithRuntimeAndNoComponents()
    {
        // EC-01: the test host is not stamped and has no notices file next to it.
        var user = factory.CreateClientAs("about-user");

        var body = await user.GetFromJsonAsync<JsonElement>(About);

        Assert.Equal("dev", body.GetProperty("version").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("revision").ValueKind);
        Assert.StartsWith(
            ".NET",
            body.GetProperty("runtime").GetString(),
            StringComparison.Ordinal
        );
        Assert.Equal(0, body.GetProperty("components").GetArrayLength());
    }

    [Fact]
    public async Task About_StampedBuild_ReportsVersionRevisionAndComponents()
    {
        // AC-04 / AC-09: the endpoint passes through what the build stamped and generated.
        using var stamped = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IAboutInformation>(
                    new StubAbout(
                        new BuildVersion("v0.3.0", "fe65db263c99"),
                        [
                            new ThirdPartyComponent(
                                "Npgsql",
                                "10.0.3",
                                "PostgreSQL",
                                "Copyright 2025 © The Npgsql Development Team",
                                "https://github.com/npgsql/npgsql",
                                null
                            ),
                        ]
                    )
                )
            )
        );
        var client = stamped.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Subject", "about-stamped");

        var body = await client.GetFromJsonAsync<JsonElement>(About);

        Assert.Equal("v0.3.0", body.GetProperty("version").GetString());
        Assert.Equal("fe65db263c99", body.GetProperty("revision").GetString());
        var component = Assert.Single(body.GetProperty("components").EnumerateArray());
        Assert.Equal("Npgsql", component.GetProperty("name").GetString());
        Assert.Equal("10.0.3", component.GetProperty("version").GetString());
        Assert.Equal("PostgreSQL", component.GetProperty("license").GetString());
        Assert.Equal("https://github.com/npgsql/npgsql", component.GetProperty("url").GetString());
        Assert.Equal(JsonValueKind.Null, component.GetProperty("text").ValueKind);
    }

    [Theory]
    [InlineData(
        "v0.3.0+fe65db263c991632c7814ed7dbbcfb8764a95317",
        "v0.3.0",
        "fe65db263c991632c7814ed7dbbcfb8764a95317"
    )]
    [InlineData("v1.2.3", "v1.2.3", null)]
    [InlineData("v1.0.0-rc.1+abc123", "v1.0.0-rc.1", "abc123")]
    [InlineData("1.0.0", "dev", null)] // the SDK default of an unstamped build
    [InlineData("1.0.0+abc123", "dev", null)]
    [InlineData("dev", "dev", null)]
    [InlineData("", "dev", null)]
    [InlineData(null, "dev", null)]
    public void BuildVersion_Parse_RecognisesOnlyTheStamp(
        string? stamp,
        string version,
        string? revision
    )
    {
        var parsed = BuildVersion.Parse(stamp);

        Assert.Equal(version, parsed.Version);
        Assert.Equal(revision, parsed.Revision);
    }

    [Fact]
    public void BuildStamp_IsReadFromTheInformationalVersion()
    {
        // The test build passes no -p:InformationalVersion, so the SDK default ("1.0.0") is read
        // and the API reports a development build.
        var stamp = AboutInformation.ReadBuildStamp(typeof(AboutInformation).Assembly);

        Assert.NotNull(stamp);
        Assert.Equal(BuildVersion.Dev, BuildVersion.Parse(stamp));
    }

    [Fact]
    public void Notices_Load_ReadsTheGeneratedFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"notices-{Guid.NewGuid():N}.json");
        File.WriteAllText(
            path,
            """
            [
              { "name": "Microsoft.OpenApi", "version": "2.12.2", "license": "MIT",
                "copyright": "© Microsoft Corporation. All rights reserved.",
                "url": "https://github.com/Microsoft/OpenAPI.NET", "text": null }
            ]
            """
        );
        try
        {
            var components = ThirdPartyNotices.Load(path, NullLogger.Instance);

            var component = Assert.Single(components);
            Assert.Equal("Microsoft.OpenApi", component.Name);
            Assert.Equal("MIT", component.License);
            Assert.Equal("© Microsoft Corporation. All rights reserved.", component.Copyright);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Notices_Load_MissingFile_IsEmpty()
    {
        var components = ThirdPartyNotices.Load(
            Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.json"),
            NullLogger.Instance
        );

        Assert.Empty(components);
    }

    [Fact]
    public void Notices_Load_MalformedFile_IsEmptyNotAnException()
    {
        var path = Path.Combine(Path.GetTempPath(), $"notices-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, "{ not json");
        try
        {
            Assert.Empty(ThirdPartyNotices.Load(path, NullLogger.Instance));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class StubAbout(
        BuildVersion version,
        IReadOnlyList<ThirdPartyComponent> components
    ) : IAboutInformation
    {
        public BuildVersion Version => version;

        public string Runtime => ".NET 10.0.0 (test)";

        public IReadOnlyList<ThirdPartyComponent> Components => components;
    }
}
