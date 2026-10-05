using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;

namespace StoreIt.Api.Service.Tests;

/// <summary>
/// SPEC-008 AC-01 / AC-02 / AC-08 — who may open the operator view, black-box over HTTP.
/// The fixture allowlists <c>operator@test.local</c> and <c>Second.Operator@Test.local</c>.
/// </summary>
public class AdminEndpointsTests(ApiTestFixture factory) : IClassFixture<ApiTestFixture>
{
    private const string Statistics = "/api/v1/admin/statistics";

    [Fact]
    public async Task Statistics_Anonymous_Returns401()
    {
        var anonymous = factory.CreateClient();

        var response = await anonymous.GetAsync(Statistics);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Statistics_SignedInButNotOnAllowlist_Returns403()
    {
        var user = factory.CreateClientAs("admin-nobody", email: "nobody@test.local");

        var response = await user.GetAsync(Statistics);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Statistics_SignedInWithoutEmailClaim_Returns403()
    {
        // EC-01: a provider that delivers no e-mail can never be the operator.
        var user = factory.CreateClientAs("admin-no-email");

        var response = await user.GetAsync(Statistics);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("operator@test.local")]
    [InlineData("OPERATOR@test.local")]
    [InlineData("second.operator@test.local")]
    public async Task Statistics_OnAllowlist_Returns200_CaseInsensitive(string email)
    {
        var admin = factory.CreateClientAs("admin-" + email, email: email);

        var response = await admin.GetAsync(Statistics);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("generatedAt", out _));
        Assert.True(body.GetProperty("users").GetProperty("total").GetInt32() >= 1);
    }

    [Fact]
    public async Task Me_ReportsIsAdminOnlyForTheAllowlist()
    {
        var admin = factory.CreateClientAs("admin-me-yes", email: "operator@test.local");
        var other = factory.CreateClientAs("admin-me-no", email: "other@test.local");

        var adminMe = await admin.GetFromJsonAsync<JsonElement>("/auth/me");
        var otherMe = await other.GetFromJsonAsync<JsonElement>("/auth/me");

        Assert.True(adminMe.GetProperty("isAdmin").GetBoolean());
        Assert.False(otherMe.GetProperty("isAdmin").GetBoolean());
    }

    [Fact]
    public async Task Statistics_EmptyAllowlist_Returns403EvenForTheOperator()
    {
        // D2: an empty Admin:Emails is a valid state in which nobody is admin.
        using var noAdmins = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Admin:Emails", "")
        );
        var client = noAdmins.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Subject", "admin-empty-list");
        client.DefaultRequestHeaders.Add("X-Test-Email", "operator@test.local");

        var response = await client.GetAsync(Statistics);
        var me = await client.GetFromJsonAsync<JsonElement>("/auth/me");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(me.GetProperty("isAdmin").GetBoolean());
    }
}
