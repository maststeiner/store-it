using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StoreIt.Infrastructure;
using static StoreIt.Api.Service.Tests.ApiTestHelpers;

namespace StoreIt.Api.Service.Tests;

/// <summary>
/// SPEC-006 — a user deletes their own account and all their data, black-box over HTTP
/// against the real API + PostgreSQL (the cascade under test is the database's).
/// </summary>
public class AccountEndpointsTests(ApiTestFixture factory) : IClassFixture<ApiTestFixture>
{
    private static readonly DateOnly Today = ApiTestFixture.Today;

    // --- AC-03: the group rules (401 / 403) apply ---

    [Fact]
    public async Task DeleteAccount_Anonymous_Returns401()
    {
        var anonymous = factory.CreateClient();

        var response = await anonymous.DeleteAsync("/api/v1/account");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAccount_WithoutCsrfToken_Returns403()
    {
        // Authenticated, but no CSRF priming: neither antiforgery cookie nor X-XSRF-TOKEN.
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Subject", "account-no-csrf");

        var response = await client.DeleteAsync("/api/v1/account");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("csrf.invalid", await response.ReadErrorCodeAsync());
    }

    // --- AC-01 / AC-02: the account, its storages and items are gone; the session ends ---

    [Fact]
    public async Task DeleteAccount_RemovesUserWithStoragesAndItems_Returns204AndEndsSession()
    {
        var client = factory.CreateClientAs("account-delete-me");
        var userId = await client.MeIdAsync();
        var pantry = await client.CreateStorageAsync("Pantry to delete");
        var freezer = await client.CreateStorageAsync("Freezer to delete");
        await client.AddItemAsync(pantry.Id, ItemBody("Rice", expiryDate: Today.AddDays(30)));
        await client.AddItemAsync(freezer.Id, ItemBody("Peas", expiryDate: Today.AddDays(90)));

        var response = await client.DeleteAsync("/api/v1/account");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var sessionCookie = SetCookie(response, ".AspNetCore.Cookies");
        Assert.Contains(
            "expires=Thu, 01 Jan 1970",
            sessionCookie,
            StringComparison.OrdinalIgnoreCase
        );

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StoreItDbContext>();
        Assert.False(await db.Users.AnyAsync(u => u.Id == userId));
        Assert.Equal(
            0,
            await db.Storages.IgnoreQueryFilters().CountAsync(s => s.OwnerId == userId)
        );
        Assert.Equal(
            0,
            await db
                .Storages.IgnoreQueryFilters()
                .Where(s => s.Id == pantry.Id || s.Id == freezer.Id)
                .SelectMany(s => s.Items)
                .CountAsync()
        );
    }

    // --- AC-04: nobody else's data moves ---

    [Fact]
    public async Task DeleteAccount_LeavesOtherUsersDataUntouched()
    {
        var leaving = factory.CreateClientAs("account-leaving");
        var staying = factory.CreateClientAs("account-staying");
        await leaving.CreateStorageAsync("Leaving's cellar");
        var stayingStorage = await staying.CreateStorageAsync("Staying's cellar");
        await staying.AddItemAsync(
            stayingStorage.Id,
            ItemBody("Wine", expiryDate: Today.AddYears(5))
        );

        var response = await leaving.DeleteAsync("/api/v1/account");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var stayingList = await staying.GetStoragesAsync();
        Assert.Contains(stayingList, s => s.Id == stayingStorage.Id);
        Assert.Single(await staying.GetItemsAsync(stayingStorage.Id));
    }

    // --- EC-01: two tabs confirm — the second call finds nothing and still answers 204 ---

    [Fact]
    public async Task DeleteAccount_AlreadyDeleted_IsIdempotent()
    {
        var client = factory.CreateClientAs("account-twice");
        var userId = await client.MeIdAsync();
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.DeleteAsync("/api/v1/account")).StatusCode
        );

        // Same cookie-like principal, user row gone (no re-provisioning on this request).
        client.DefaultRequestHeaders.Add(TestAuthHandler.LocalIdHeader, userId.ToString());
        var second = await client.DeleteAsync("/api/v1/account");

        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
    }

    // --- AC-05 (D6): a stale session on another device writes → 401 + cookie ended, no 500 ---

    [Fact]
    public async Task StaleSession_CreateStorage_Returns401AuthSessionStaleAndEndsSession()
    {
        var client = factory.CreateClientAs("account-stale");
        var userId = await client.MeIdAsync();
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.DeleteAsync("/api/v1/account")).StatusCode
        );
        client.DefaultRequestHeaders.Add(TestAuthHandler.LocalIdHeader, userId.ToString());

        var response = await client.PostAsJsonAsync("/api/v1/storages", new { name = "Ghost" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("auth.session.stale", await response.ReadErrorCodeAsync());
        var sessionCookie = SetCookie(response, ".AspNetCore.Cookies");
        Assert.Contains(
            "expires=Thu, 01 Jan 1970",
            sessionCookie,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public async Task StaleSession_ListStorages_ReturnsEmptyList()
    {
        var client = factory.CreateClientAs("account-stale-reader");
        var userId = await client.MeIdAsync();
        await client.CreateStorageAsync("Soon gone");
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.DeleteAsync("/api/v1/account")).StatusCode
        );
        client.DefaultRequestHeaders.Add(TestAuthHandler.LocalIdHeader, userId.ToString());

        var list = await client.GetStoragesAsync();

        Assert.Empty(list);
    }

    // --- AC-06 (D7): the same provider identity comes back as a new, empty user ---

    [Fact]
    public async Task SignInAfterDeletion_ProvisionsFreshEmptyUser()
    {
        var client = factory.CreateClientAs("account-returning");
        var firstId = await client.MeIdAsync();
        await client.CreateStorageAsync("Old life");
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.DeleteAsync("/api/v1/account")).StatusCode
        );

        // The Test scheme provisions on every request, like a fresh OIDC sign-in would.
        var secondId = await client.MeIdAsync();
        var storages = await client.GetStoragesAsync();

        Assert.NotEqual(firstId, secondId);
        Assert.Empty(storages);
    }

    private static string SetCookie(HttpResponseMessage response, string name)
    {
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
        var line = cookies.FirstOrDefault(c => c.StartsWith(name + "=", StringComparison.Ordinal));
        Assert.NotNull(line);
        return line;
    }
}

file static class AccountTestExtensions
{
    /// <summary>The internal user id of the caller, read from <c>GET /auth/me</c>.</summary>
    public static async Task<Guid> MeIdAsync(this HttpClient client)
    {
        var me = await client.GetFromJsonAsync<JsonElement>("/auth/me");
        return Guid.Parse(me.GetProperty("id").GetString()!);
    }
}
