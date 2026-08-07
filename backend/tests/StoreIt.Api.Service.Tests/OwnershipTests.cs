using System.Net;
using System.Net.Http.Json;
using static StoreIt.Api.Service.Tests.ApiTestHelpers;

namespace StoreIt.Api.Service.Tests;

/// <summary>
/// SPEC-003 ownership isolation, black-box over HTTP against the real API + PostgreSQL.
/// Enforcement is layered: the RequireAuthenticatedUser fallback policy rejects
/// anonymous callers (401), and the EF global query filter scopes every read to the
/// caller — so another user's storage is simply invisible and every by-id operation
/// on it surfaces the existing StorageNotFoundException as a 404 (no leak of existence).
/// </summary>
public class OwnershipTests(ApiTestFixture factory) : IClassFixture<ApiTestFixture>
{
    private static readonly DateOnly Today = ApiTestFixture.Today;

    // Distinct subjects → distinct provisioned users → distinct owners. Cached (lazy):
    // each client is created once per test class. Expression-bodied properties would
    // re-create the client — and re-run the /auth/csrf priming + user provisioning — on
    // every access, so two references to "AlfredsClient" would otherwise be two owners.
    private HttpClient? _alfredsClient;
    private HttpClient? _bettysClient;

    private HttpClient AlfredsClient => _alfredsClient ??= factory.CreateClientAs("owner-alfred");

    private HttpClient BettysClient => _bettysClient ??= factory.CreateClientAs("owner-betty");

    // --- Anonymous is rejected before it reaches a handler (fallback policy) ---

    [Fact]
    public async Task Storages_Anonymous_Returns401()
    {
        var anonymous = factory.CreateClient();

        var response = await anonymous.GetAsync("/api/v1/storages");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // --- Reads are scoped to the caller (query filter) ---

    [Fact]
    public async Task List_AnotherUserHasStorages_ReturnsOnlyOwn()
    {
        var alfredsStorage = await AlfredsClient.CreateStorageAsync("Alfred's Pantry");
        var bettysStorage = await BettysClient.CreateStorageAsync("Betty's Freezer");

        var alfredsList = await AlfredsClient.GetStoragesAsync();

        Assert.Contains(alfredsList, s => s.Id == alfredsStorage.Id);
        Assert.DoesNotContain(alfredsList, s => s.Id == bettysStorage.Id);
    }

    // --- By-id reads on another user's storage 404 (IDOR guard) ---

    [Fact]
    public async Task GetById_OnAnotherUsersStorage_Returns404()
    {
        var bettysStorage = await BettysClient.CreateStorageAsync("Betty's Attic");

        // Betty can read her own storage by id.
        var bettysOwn = await BettysClient.GetStorageAsync(bettysStorage.Id);
        Assert.Equal(bettysStorage.Id, bettysOwn.Id);

        // Alfred requesting Betty's storage by id must see a 404 — the query filter makes
        // it invisible, so its existence never leaks (no 403 distinguishing found-but-
        // forbidden from not-found).
        var alfredResponse = await AlfredsClient.GetAsync($"/api/v1/storages/{bettysStorage.Id}");
        Assert.Equal(HttpStatusCode.NotFound, alfredResponse.StatusCode);
        Assert.Equal("storage.notFound", await alfredResponse.ReadErrorCodeAsync());
        await alfredResponse.AssertNoStorageDataAsync();
    }

    // --- By-id writes on another user's storage 404 (getStorage exists, but the query
    //     filter also hides the resource from cross-user PUT rename and DELETE) ---

    [Fact]
    public async Task ByIdWrite_OnAnotherUsersStorage_Returns404()
    {
        var bettysStorage = await BettysClient.CreateStorageAsync("Betty's Cellar");

        // Alfred tries to rename Betty's storage.
        var renameResponse = await AlfredsClient.PutAsJsonAsync(
            $"/api/v1/storages/{bettysStorage.Id}",
            new { name = "Hijacked" }
        );
        Assert.Equal(HttpStatusCode.NotFound, renameResponse.StatusCode);
        Assert.Equal("storage.notFound", await renameResponse.ReadErrorCodeAsync());

        // Alfred tries to delete Betty's storage.
        var deleteResponse = await AlfredsClient.DeleteAsync(
            $"/api/v1/storages/{bettysStorage.Id}"
        );
        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
        Assert.Equal("storage.notFound", await deleteResponse.ReadErrorCodeAsync());

        // Betty's storage is untouched.
        var bettysList = await BettysClient.GetStoragesAsync();
        Assert.Contains(bettysList, s => s.Id == bettysStorage.Id && s.Name == "Betty's Cellar");
    }

    // --- Every item operation on another user's storage 404s (read + create + update + delete) ---

    [Fact]
    public async Task Items_CrossUser_AllOperationsReturn404()
    {
        var bettysStorage = await BettysClient.CreateStorageAsync("Betty's Fridge");
        var bettysItemId = await BettysClient.AddItemAsync(
            bettysStorage.Id,
            ItemBody(name: "Milk", expiryDate: Today.AddDays(5))
        );

        // Read
        var readResponse = await AlfredsClient.GetAsync(
            $"/api/v1/storages/{bettysStorage.Id}/items"
        );
        Assert.Equal(HttpStatusCode.NotFound, readResponse.StatusCode);
        Assert.Equal("storage.notFound", await readResponse.ReadErrorCodeAsync());

        // Create
        var createResponse = await AlfredsClient.PostAsJsonAsync(
            $"/api/v1/storages/{bettysStorage.Id}/items",
            ItemBody(name: "Intruder", expiryDate: Today.AddDays(5))
        );
        Assert.Equal(HttpStatusCode.NotFound, createResponse.StatusCode);
        Assert.Equal("storage.notFound", await createResponse.ReadErrorCodeAsync());

        // Update
        var updateResponse = await AlfredsClient.PutAsJsonAsync(
            $"/api/v1/storages/{bettysStorage.Id}/items/{bettysItemId}",
            ItemBody(name: "Tampered", expiryDate: Today.AddDays(5))
        );
        Assert.Equal(HttpStatusCode.NotFound, updateResponse.StatusCode);
        Assert.Equal("storage.notFound", await updateResponse.ReadErrorCodeAsync());

        // Delete
        var deleteResponse = await AlfredsClient.DeleteAsync(
            $"/api/v1/storages/{bettysStorage.Id}/items/{bettysItemId}"
        );
        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
        Assert.Equal("storage.notFound", await deleteResponse.ReadErrorCodeAsync());

        // Betty's item is untouched.
        var bettysItems = await BettysClient.GetItemsAsync(bettysStorage.Id);
        var item = Assert.Single(bettysItems);
        Assert.Equal("Milk", item.Name);
    }
}
