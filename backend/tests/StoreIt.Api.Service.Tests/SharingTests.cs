using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static StoreIt.Api.Service.Tests.ApiTestHelpers;

namespace StoreIt.Api.Service.Tests;

/// <summary>
/// SPEC-007 sharing (PR 1, AC-01 – AC-14), black-box over HTTP against the real API + PostgreSQL.
/// Three people: Olga owns, Max joins, Stranger never gets in.
/// </summary>
public class SharingTests(ApiTestFixture factory) : IClassFixture<ApiTestFixture>
{
    private static readonly DateOnly Today = ApiTestFixture.Today;

    private HttpClient? _olga;
    private HttpClient? _max;
    private HttpClient? _stranger;

    private HttpClient Olga =>
        _olga ??= factory.CreateClientAs(
            "share-olga",
            name: "Olga Owner",
            email: "olga@example.com"
        );
    private HttpClient Max =>
        _max ??= factory.CreateClientAs("share-max", name: "Max Member", email: "max@example.com");
    private HttpClient Stranger => _stranger ??= factory.CreateClientAs("share-stranger");

    /// <summary>Olga creates a storage and Max joins it through a fresh link.</summary>
    private async Task<(StorageResponse storage, string token)> SharedStorageAsync(string name)
    {
        var storage = await Olga.CreateStorageAsync(name);
        var token = await Olga.CreateInvitationAsync(storage.Id);
        var accepted = await Max.AcceptAsync(token);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        return (storage, token);
    }

    // --- AC-05 / AC-06 / AC-07: the invitation link ---

    [Fact]
    public async Task CreateInvitation_ByOwner_ReturnsTokenOnceAndSevenDayExpiry()
    {
        var storage = await Olga.CreateStorageAsync("Olga's pantry");

        var response = await Olga.PostAsync($"/api/v1/storages/{storage.Id}/invitation", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = created.GetProperty("token").GetString()!;
        Assert.True(token.Length >= 43, "256 random bits, base64url");
        var expires = created.GetProperty("expiresAt").GetDateTimeOffset();
        Assert.Equal(Today.AddDays(7), DateOnly.FromDateTime(expires.UtcDateTime));

        // AC-06: the status never repeats the token.
        var status = await Olga.GetFromJsonAsync<JsonElement>(
            $"/api/v1/storages/{storage.Id}/invitation"
        );
        Assert.True(status.GetProperty("active").GetBoolean());
        Assert.False(status.TryGetProperty("token", out _));
        Assert.Equal(expires, status.GetProperty("expiresAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task CreateInvitation_Again_ReplacesTheOldToken()
    {
        var storage = await Olga.CreateStorageAsync("Olga's replaced link");
        var first = await Olga.CreateInvitationAsync(storage.Id);
        var second = await Olga.CreateInvitationAsync(storage.Id);

        var oldPreview = await Max.PreviewAsync(first);
        var newPreview = await Max.PreviewAsync(second);

        Assert.Equal(HttpStatusCode.NotFound, oldPreview.StatusCode);
        Assert.Equal("invite.invalid", await oldPreview.ReadErrorCodeAsync());
        Assert.Equal(HttpStatusCode.OK, newPreview.StatusCode);
    }

    [Fact]
    public async Task DeactivateInvitation_ByOwner_MakesTokenUnusableAndStatusInactive()
    {
        var storage = await Olga.CreateStorageAsync("Olga's deactivated link");
        var token = await Olga.CreateInvitationAsync(storage.Id);

        var deactivate = await Olga.DeleteAsync($"/api/v1/storages/{storage.Id}/invitation");

        Assert.Equal(HttpStatusCode.NoContent, deactivate.StatusCode);
        var accept = await Max.AcceptAsync(token);
        Assert.Equal(HttpStatusCode.NotFound, accept.StatusCode);
        Assert.Equal("invite.invalid", await accept.ReadErrorCodeAsync());
        var status = await Olga.GetFromJsonAsync<JsonElement>(
            $"/api/v1/storages/{storage.Id}/invitation"
        );
        Assert.False(status.GetProperty("active").GetBoolean());
        Assert.Equal(JsonValueKind.Null, status.GetProperty("expiresAt").ValueKind);
    }

    // --- AC-08 / AC-09 / AC-10: preview and accept ---

    [Fact]
    public async Task PreviewInvitation_ValidToken_NamesStorageAndOwner()
    {
        var storage = await Olga.CreateStorageAsync("Olga's cellar");
        var token = await Olga.CreateInvitationAsync(storage.Id);

        var response = await Stranger.PreviewAsync(token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var preview = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(storage.Id, preview.GetProperty("storageId").GetGuid());
        Assert.Equal("Olga's cellar", preview.GetProperty("storageName").GetString());
        Assert.Equal("Olga Owner", preview.GetProperty("ownerName").GetString());
        Assert.False(preview.GetProperty("alreadyMember").GetBoolean());
        Assert.False(preview.TryGetProperty("ownerEmail", out _), "D6: no e-mail");
    }

    [Fact]
    public async Task PreviewInvitation_UnknownToken_Returns404InviteInvalid()
    {
        var response = await Max.PreviewAsync("no-such-token-at-all-000000000000000000000");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("invite.invalid", await response.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task AcceptInvitation_ValidToken_AddsMemberAndListsStorageForBoth()
    {
        var (storage, _) = await SharedStorageAsync("Olga's freezer");

        var maxList = await Max.GetStoragesAsync();
        var olgaList = await Olga.GetStoragesAsync();

        var forMax = Assert.Single(maxList, s => s.Id == storage.Id);
        Assert.False(forMax.IsOwner);
        Assert.Equal(1, forMax.MemberCount);
        Assert.Equal("Olga Owner", forMax.OwnerName);
        var forOlga = Assert.Single(olgaList, s => s.Id == storage.Id);
        Assert.True(forOlga.IsOwner);
        Assert.Equal(1, forOlga.MemberCount);
    }

    [Fact]
    public async Task AcceptInvitation_TwiceAndByOwner_IsIdempotent()
    {
        var (storage, token) = await SharedStorageAsync("Olga's twice");

        var again = await Max.AcceptAsync(token);
        var byOwner = await Olga.AcceptAsync(token);

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(HttpStatusCode.OK, byOwner.StatusCode);
        var preview = await (
            await Max.PreviewAsync(token)
        ).Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(preview.GetProperty("alreadyMember").GetBoolean());
        var members = await Olga.GetFromJsonAsync<JsonElement[]>(
            $"/api/v1/storages/{storage.Id}/members"
        );
        Assert.Equal(2, members!.Length); // owner + Max, nobody twice
    }

    // --- AC-02 / AC-03 / AC-04: what members and strangers may do ---

    [Fact]
    public async Task Member_EditsItemsAndRenames_LikeTheOwner()
    {
        var (storage, _) = await SharedStorageAsync("Olga's shared pantry");

        var itemId = await Max.AddItemAsync(
            storage.Id,
            ItemBody("Beans", expiryDate: Today.AddDays(200))
        );
        var rename = await Max.PutAsJsonAsync(
            $"/api/v1/storages/{storage.Id}",
            new { name = "Our pantry" }
        );
        var deleteItem = await Max.DeleteAsync($"/api/v1/storages/{storage.Id}/items/{itemId}");

        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleteItem.StatusCode);
        Assert.Equal("Our pantry", (await Olga.GetStorageAsync(storage.Id)).Name);
    }

    [Fact]
    public async Task Stranger_ById_Returns404()
    {
        var (storage, _) = await SharedStorageAsync("Olga's private");

        var response = await Stranger.GetAsync($"/api/v1/storages/{storage.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain(await Stranger.GetStoragesAsync(), s => s.Id == storage.Id);
    }

    [Fact]
    public async Task Member_OwnerOnlyOperations_Return403OwnerOnly()
    {
        var (storage, _) = await SharedStorageAsync("Olga's owner-only");

        var deleteStorage = await Max.DeleteAsync($"/api/v1/storages/{storage.Id}");
        var createLink = await Max.PostAsync($"/api/v1/storages/{storage.Id}/invitation", null);
        var readLink = await Max.GetAsync($"/api/v1/storages/{storage.Id}/invitation");
        var removeSelf = await Max.DeleteAsync(
            $"/api/v1/storages/{storage.Id}/members/{Guid.NewGuid()}"
        );

        foreach (var response in new[] { deleteStorage, createLink, readLink, removeSelf })
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("storage.ownerOnly", await response.ReadErrorCodeAsync());
        }
        Assert.Equal(
            HttpStatusCode.OK,
            (await Olga.GetAsync($"/api/v1/storages/{storage.Id}")).StatusCode
        );
    }

    // --- AC-11 / AC-12 / AC-13 / AC-14: members ---

    [Fact]
    public async Task GetMembers_ListsOwnerFirstThenMembers_DisplayNamesOnly()
    {
        var (storage, _) = await SharedStorageAsync("Olga's member list");

        var members = await Max.GetFromJsonAsync<JsonElement[]>(
            $"/api/v1/storages/{storage.Id}/members"
        );

        Assert.NotNull(members);
        Assert.Equal(2, members.Length);
        Assert.True(members[0].GetProperty("isOwner").GetBoolean());
        Assert.Equal("Olga Owner", members[0].GetProperty("displayName").GetString());
        Assert.False(members[1].GetProperty("isOwner").GetBoolean());
        Assert.Equal("Max Member", members[1].GetProperty("displayName").GetString());
        Assert.NotEqual(JsonValueKind.Null, members[1].GetProperty("joinedAt").ValueKind);
        foreach (var member in members)
        {
            Assert.False(member.TryGetProperty("email", out _), "D6: no e-mail addresses");
        }
    }

    [Fact]
    public async Task RemoveMember_ByOwner_EndsAccess()
    {
        var (storage, _) = await SharedStorageAsync("Olga's removal");
        var members = await Olga.GetFromJsonAsync<JsonElement[]>(
            $"/api/v1/storages/{storage.Id}/members"
        );
        var maxId = members!
            .Single(m => !m.GetProperty("isOwner").GetBoolean())
            .GetProperty("userId")
            .GetGuid();

        var remove = await Olga.DeleteAsync($"/api/v1/storages/{storage.Id}/members/{maxId}");

        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await Max.GetAsync($"/api/v1/storages/{storage.Id}")).StatusCode
        );
        Assert.Equal(0, (await Olga.GetStorageAsync(storage.Id)).MemberCount);
    }

    [Fact]
    public async Task LeaveStorage_ByMember_KeepsStorageAndItems()
    {
        var (storage, _) = await SharedStorageAsync("Olga's left behind");
        await Max.AddItemAsync(storage.Id, ItemBody("Jam", expiryDate: Today.AddDays(365)));

        var leave = await Max.DeleteAsync($"/api/v1/storages/{storage.Id}/membership");

        Assert.Equal(HttpStatusCode.NoContent, leave.StatusCode);
        Assert.DoesNotContain(await Max.GetStoragesAsync(), s => s.Id == storage.Id);
        var olgas = await Olga.GetStorageAsync(storage.Id);
        Assert.Equal(1, olgas.ItemCount);
        Assert.Equal(0, olgas.MemberCount);
    }

    [Fact]
    public async Task LeaveStorage_ByOwner_Returns409OwnerCannotLeave()
    {
        var (storage, _) = await SharedStorageAsync("Olga stays");

        var leave = await Olga.DeleteAsync($"/api/v1/storages/{storage.Id}/membership");

        Assert.Equal(HttpStatusCode.Conflict, leave.StatusCode);
        Assert.Equal("storage.ownerCannotLeave", await leave.ReadErrorCodeAsync());
    }
}

file static class SharingTestExtensions
{
    public static async Task<string> CreateInvitationAsync(this HttpClient owner, Guid storageId)
    {
        var response = await owner.PostAsync($"/api/v1/storages/{storageId}/invitation", null);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        return created.GetProperty("token").GetString()!;
    }

    public static Task<HttpResponseMessage> PreviewAsync(this HttpClient client, string token) =>
        client.PostAsJsonAsync("/api/v1/invitations/preview", new { token });

    public static Task<HttpResponseMessage> AcceptAsync(this HttpClient client, string token) =>
        client.PostAsJsonAsync("/api/v1/invitations/accept", new { token });
}
