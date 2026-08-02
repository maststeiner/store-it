using System.Net;
using System.Net.Http.Json;
using static StoreIt.Api.Service.Tests.ApiTestHelpers;

namespace StoreIt.Api.Service.Tests;

/// <summary>
/// Service tests derived from SPEC-001 (AC-01..AC-04, EC-06) — black-box over HTTP
/// against the real API + PostgreSQL (ApiTestFixture), never against the implementation.
/// </summary>
public class StorageEndpointsTests(ApiTestFixture factory) : IClassFixture<ApiTestFixture>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Now);

    // --- AC-01: create storage ---

    [Fact]
    public async Task CreateStorage_WithValidName_ReturnsCreatedStorage()
    {
        // AC-01
        var response = await _client.PostAsJsonAsync("/api/v1/storages", new { name = "Pantry" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var storage = await response.Content.ReadFromJsonAsync<StorageResponse>();
        Assert.NotNull(storage);
        Assert.NotEqual(Guid.Empty, storage.Id);
        Assert.Equal("Pantry", storage.Name);
        Assert.Equal(0, storage.ItemCount);
    }

    [Fact]
    public async Task CreateStorage_WithValidName_AppearsInStorageList()
    {
        // AC-01: persisted and returned in the storage list
        var created = await _client.CreateStorageAsync("Freezer");

        var storages = await _client.GetStoragesAsync();

        Assert.Contains(storages, s => s.Id == created.Id && s.Name == "Freezer");
    }

    // --- AC-01a: server-computed status counts per storage ---

    [Fact]
    public async Task GetStorages_StorageWithMixedItems_ReturnsCorrectStatusCounts()
    {
        // AC-01a: expiredCount = items with expiry date in the past;
        // expiringSoonCount = expiry date within the next 3 days incl. today;
        // items without expiry date count toward neither.
        var created = await _client.CreateStorageAsync("StatusCountsPantry");
        await _client.AddItemAsync(
            created.Id,
            ItemBody(name: "Bread", expiryDate: Today.AddDays(-1)) // expired
        );
        await _client.AddItemAsync(
            created.Id,
            ItemBody(name: "Yogurt", expiryDate: Today) // expiring today -> soon
        );
        await _client.AddItemAsync(
            created.Id,
            ItemBody(name: "Ham", expiryDate: Today.AddDays(3)) // in 3 days -> soon
        );
        await _client.AddItemAsync(
            created.Id,
            ItemBody(name: "Rice", expiryDate: Today.AddDays(4)) // later -> neither
        );
        await _client.AddItemAsync(
            created.Id,
            ItemBody(name: "Jam", productionDate: Today.AddDays(-10)) // no expiry -> neither
        );

        var storages = await _client.GetStoragesAsync();

        var storage = Assert.Single(storages, s => s.Id == created.Id);
        Assert.Equal(5, storage.ItemCount);
        Assert.Equal(1, storage.ExpiredCount);
        Assert.Equal(2, storage.ExpiringSoonCount);
    }

    [Fact]
    public async Task CreateStorage_FreshStorage_HasZeroStatusCounts()
    {
        // AC-01a: a storage without items reports zero for both counts
        // in the POST response and in the storage list.
        var created = await _client.CreateStorageAsync("EmptyStatusCounts");

        Assert.Equal(0, created.ExpiredCount);
        Assert.Equal(0, created.ExpiringSoonCount);

        var storages = await _client.GetStoragesAsync();
        var storage = Assert.Single(storages, s => s.Id == created.Id);
        Assert.Equal(0, storage.ItemCount);
        Assert.Equal(0, storage.ExpiredCount);
        Assert.Equal(0, storage.ExpiringSoonCount);
    }

    // --- #29: get a single storage by id (with status counts) ---

    [Fact]
    public async Task GetStorage_ExistingStorage_ReturnsItWithStatusCounts()
    {
        var created = await _client.CreateStorageAsync("SingleGet");
        await _client.AddItemAsync(
            created.Id,
            ItemBody(name: "Milk", expiryDate: Today.AddDays(-1))
        ); // expired
        await _client.AddItemAsync(
            created.Id,
            ItemBody(name: "Eggs", expiryDate: Today.AddDays(2))
        ); // soon

        var storage = await _client.GetStorageAsync(created.Id);

        Assert.Equal(created.Id, storage.Id);
        Assert.Equal("SingleGet", storage.Name);
        Assert.Equal(2, storage.ItemCount);
        Assert.Equal(1, storage.ExpiredCount);
        Assert.Equal(1, storage.ExpiringSoonCount);
    }

    [Fact]
    public async Task GetStorage_EmptyStorage_ReturnsZeroStatusCounts()
    {
        var created = await _client.CreateStorageAsync("EmptySingleGet");

        var storage = await _client.GetStorageAsync(created.Id);

        Assert.Equal(created.Id, storage.Id);
        Assert.Equal("EmptySingleGet", storage.Name);
        Assert.Equal(0, storage.ItemCount);
        Assert.Equal(0, storage.ExpiredCount);
        Assert.Equal(0, storage.ExpiringSoonCount);
    }

    [Fact]
    public async Task GetStorage_BoundaryExpiryDates_CountsTodayAndThreeDaysAsExpiringSoon()
    {
        // Mirrors the AC-01a boundary on the single-storage endpoint:
        // today and +3 days are "expiring soon", -1 day is expired, +4 days is neither.
        var created = await _client.CreateStorageAsync("BoundarySingleGet");
        await _client.AddItemAsync(
            created.Id,
            ItemBody(name: "Past", expiryDate: Today.AddDays(-1))
        );
        await _client.AddItemAsync(created.Id, ItemBody(name: "Today", expiryDate: Today));
        await _client.AddItemAsync(
            created.Id,
            ItemBody(name: "Edge", expiryDate: Today.AddDays(3))
        );
        await _client.AddItemAsync(
            created.Id,
            ItemBody(name: "Later", expiryDate: Today.AddDays(4))
        );

        var storage = await _client.GetStorageAsync(created.Id);

        Assert.Equal(4, storage.ItemCount);
        Assert.Equal(1, storage.ExpiredCount);
        Assert.Equal(2, storage.ExpiringSoonCount);
    }

    [Fact]
    public async Task GetStorage_UnknownStorage_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/v1/storages/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("storage.notFound", await response.ReadErrorCodeAsync());
    }

    // --- AC-02: empty name rejected ---

    [Fact]
    public async Task CreateStorage_WithEmptyName_ReturnsBadRequestWithErrorCode()
    {
        // AC-02
        var response = await _client.PostAsJsonAsync("/api/v1/storages", new { name = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("storage.name.empty", await response.ReadErrorCodeAsync());
    }

    // --- AC-03: rename ---

    [Fact]
    public async Task RenameStorage_WithValidName_ReturnsOkAndUpdatesName()
    {
        // AC-03
        var created = await _client.CreateStorageAsync("Pantry");

        var response = await _client.PutAsJsonAsync(
            $"/api/v1/storages/{created.Id}",
            new { name = "Cellar" }
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var storages = await _client.GetStoragesAsync();
        Assert.Contains(storages, s => s.Id == created.Id && s.Name == "Cellar");
    }

    [Fact]
    public async Task RenameStorage_WithEmptyName_ReturnsBadRequestWithErrorCode()
    {
        // AC-03 (same validation as AC-02)
        var created = await _client.CreateStorageAsync("Pantry");

        var response = await _client.PutAsJsonAsync(
            $"/api/v1/storages/{created.Id}",
            new { name = "" }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("storage.name.empty", await response.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task RenameStorage_UnknownStorage_ReturnsNotFound()
    {
        var response = await _client.PutAsJsonAsync(
            $"/api/v1/storages/{Guid.NewGuid()}",
            new { name = "Cellar" }
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("storage.notFound", await response.ReadErrorCodeAsync());
    }

    // --- AC-04 / EC-06: delete ---

    [Fact]
    public async Task DeleteStorage_ExistingStorage_ReturnsNoContentAndRemovesFromList()
    {
        // AC-04
        var created = await _client.CreateStorageAsync("Garage");

        var response = await _client.DeleteAsync($"/api/v1/storages/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var storages = await _client.GetStoragesAsync();
        Assert.DoesNotContain(storages, s => s.Id == created.Id);
    }

    [Fact]
    public async Task DeleteStorage_StorageWithItems_ReturnsNoContentAndDeletesItemsToo()
    {
        // AC-04 / EC-06: items are deleted with the storage, no orphans
        var created = await _client.CreateStorageAsync("Freezer");
        await _client.AddItemAsync(
            created.Id,
            ItemBody(name: "Peas", expiryDate: Today.AddDays(90))
        );
        await _client.AddItemAsync(
            created.Id,
            ItemBody(name: "Ice", expiryDate: Today.AddDays(300))
        );

        var response = await _client.DeleteAsync($"/api/v1/storages/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var itemsResponse = await _client.GetAsync($"/api/v1/storages/{created.Id}/items");
        Assert.Equal(HttpStatusCode.NotFound, itemsResponse.StatusCode);
        Assert.Equal("storage.notFound", await itemsResponse.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task DeleteStorage_UnknownStorage_ReturnsNotFound()
    {
        var response = await _client.DeleteAsync($"/api/v1/storages/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("storage.notFound", await response.ReadErrorCodeAsync());
    }
}
