using System.Net;
using System.Net.Http.Json;
using static StoreIt.Api.Service.Tests.ApiTestHelpers;

namespace StoreIt.Api.Service.Tests;

/// <summary>
/// Service tests derived from SPEC-001 (AC-05..AC-12, EC-01..EC-05) — black-box over HTTP
/// against the real API + PostgreSQL (ApiTestFixture), never against the implementation.
/// Expiry dates are derived from the test host's clock because expiryStatus is
/// computed server-side against "today".
/// </summary>
public class ItemEndpointsTests(ApiTestFixture factory) : IClassFixture<ApiTestFixture>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static readonly DateOnly Today = ApiTestFixture.Today;

    // --- EC-03: empty storage ---

    [Fact]
    public async Task GetItems_StorageWithoutItems_ReturnsEmptyList()
    {
        // EC-03: 0 items → empty list, no error
        var storage = await _client.CreateStorageAsync("Pantry");

        var response = await _client.GetAsync($"/api/v1/storages/{storage.Id}/items");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<List<ItemResponse>>();
        Assert.NotNull(items);
        Assert.Empty(items);
    }

    [Fact]
    public async Task GetItems_UnknownStorage_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/v1/storages/{Guid.NewGuid()}/items");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("storage.notFound", await response.ReadErrorCodeAsync());
    }

    // --- AC-05: add item happy paths ---

    [Fact]
    public async Task AddItem_WithExpiryDateOnly_ReturnsCreatedAndItemAppearsInList()
    {
        // AC-05
        var storage = await _client.CreateStorageAsync("Pantry");
        var expiry = Today.AddDays(30);

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/storages/{storage.Id}/items",
            ItemBody(name: "Milk", amount: 1.5m, unit: "Liter", expiryDate: expiry)
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var itemId = await response.Content.ReadFromJsonAsync<Guid>();
        Assert.NotEqual(Guid.Empty, itemId);
        var items = await _client.GetItemsAsync(storage.Id);
        var item = Assert.Single(items);
        Assert.Equal(itemId, item.Id);
        Assert.Equal("Milk", item.Name);
        Assert.Equal(1.5m, item.Amount);
        Assert.Equal("Liter", item.Unit);
        Assert.Equal(expiry, item.ExpiryDate);
        Assert.Null(item.ProductionDate);
    }

    [Fact]
    public async Task AddItem_WithProductionDateOnly_ReturnsCreatedWithStatusOk()
    {
        // AC-05 / EC-05: only a production date → valid, never expired/expiring soon
        var storage = await _client.CreateStorageAsync("Pantry");
        var production = Today.AddDays(-30);

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/storages/{storage.Id}/items",
            ItemBody(name: "Flour", amount: 1m, unit: "Kilogram", productionDate: production)
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var items = await _client.GetItemsAsync(storage.Id);
        var item = Assert.Single(items);
        Assert.Null(item.ExpiryDate);
        Assert.Equal(production, item.ProductionDate);
        Assert.Equal("Ok", item.ExpiryStatus);
    }

    [Fact]
    public async Task AddItem_WithBothDates_ReturnsCreatedAndPersistsBothDates()
    {
        // AC-05: both dates allowed
        var storage = await _client.CreateStorageAsync("Pantry");
        var expiry = Today.AddDays(14);
        var production = Today.AddDays(-2);

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/storages/{storage.Id}/items",
            ItemBody(name: "Yogurt", amount: 4m, expiryDate: expiry, productionDate: production)
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var items = await _client.GetItemsAsync(storage.Id);
        var item = Assert.Single(items);
        Assert.Equal(expiry, item.ExpiryDate);
        Assert.Equal(production, item.ProductionDate);
    }

    // --- AC-06 / EC-04: add item validation ---

    [Fact]
    public async Task AddItem_WithEmptyName_ReturnsBadRequestWithErrorCode()
    {
        // AC-06: empty name
        var storage = await _client.CreateStorageAsync("Pantry");

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/storages/{storage.Id}/items",
            ItemBody(name: "", expiryDate: Today.AddDays(5))
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errorCode = await response.ReadErrorCodeAsync();
        Assert.False(string.IsNullOrEmpty(errorCode));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.5)]
    public async Task AddItem_WithNonPositiveAmount_ReturnsBadRequestWithErrorCode(double amount)
    {
        // AC-06: amount ≤ 0
        var storage = await _client.CreateStorageAsync("Pantry");

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/storages/{storage.Id}/items",
            ItemBody(amount: (decimal)amount, expiryDate: Today.AddDays(5))
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("item.amount.notPositive", await response.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task AddItem_WithMoreThanOneDecimalPlace_ReturnsBadRequestWithErrorCode()
    {
        // AC-06 / EC-04: 0.25 → validation error, no silent rounding
        var storage = await _client.CreateStorageAsync("Pantry");

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/storages/{storage.Id}/items",
            ItemBody(amount: 0.25m, expiryDate: Today.AddDays(5))
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("item.amount.tooManyDecimals", await response.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task AddItem_WithUnitOutsideFixedList_ReturnsBadRequestWithErrorCode()
    {
        // AC-06: unit outside the fixed list (unknown string → enum binding fails)
        var storage = await _client.CreateStorageAsync("Pantry");

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/storages/{storage.Id}/items",
            ItemBody(unit: "Bottle", expiryDate: Today.AddDays(5))
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("request.invalid", await response.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task AddItem_WithUnitAsUndefinedInteger_ReturnsBadRequestWithErrorCode()
    {
        // AC-06: JsonStringEnumConverter accepts integer tokens, so an out-of-range
        // value (999) binds to an undefined enum — the domain must reject it.
        var storage = await _client.CreateStorageAsync("Pantry");

        var body = JsonContent.Create(
            new
            {
                name = "Mystery",
                amount = 1m,
                unit = 999,
                expiryDate = Today.AddDays(5),
                productionDate = (DateOnly?)null,
            }
        );
        var response = await _client.PostAsync($"/api/v1/storages/{storage.Id}/items", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("item.unit.invalid", await response.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task AddItem_WithoutAnyDate_ReturnsBadRequestWithErrorCode()
    {
        // AC-06: at least one of expiry / production date required
        var storage = await _client.CreateStorageAsync("Pantry");

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/storages/{storage.Id}/items",
            ItemBody()
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("item.dates.missing", await response.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task AddItem_ToUnknownStorage_ReturnsNotFound()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/storages/{Guid.NewGuid()}/items",
            ItemBody(expiryDate: Today.AddDays(5))
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("storage.notFound", await response.ReadErrorCodeAsync());
    }

    // --- EC-01: duplicate names ---

    [Fact]
    public async Task AddItem_WithSameNameTwice_CreatesTwoSeparateItems()
    {
        // EC-01
        var storage = await _client.CreateStorageAsync("Fridge");

        var firstId = await _client.AddItemAsync(
            storage.Id,
            ItemBody(name: "Yogurt", expiryDate: Today.AddDays(5))
        );
        var secondId = await _client.AddItemAsync(
            storage.Id,
            ItemBody(name: "Yogurt", expiryDate: Today.AddDays(12))
        );

        Assert.NotEqual(firstId, secondId);
        var items = await _client.GetItemsAsync(storage.Id);
        Assert.Equal(2, items.Count);
        Assert.All(items, item => Assert.Equal("Yogurt", item.Name));
    }

    // --- AC-07: edit item ---

    [Fact]
    public async Task UpdateItem_WithValidData_ReturnsNoContentAndUpdatesAllFields()
    {
        // AC-07
        var storage = await _client.CreateStorageAsync("Pantry");
        var itemId = await _client.AddItemAsync(
            storage.Id,
            ItemBody(name: "Milk", amount: 1m, unit: "Liter", expiryDate: Today.AddDays(3))
        );
        var newExpiry = Today.AddDays(10);
        var newProduction = Today.AddDays(-1);

        var response = await _client.PutAsJsonAsync(
            $"/api/v1/storages/{storage.Id}/items/{itemId}",
            ItemBody(
                name: "Oat Milk",
                amount: 2.5m,
                unit: "Pack",
                expiryDate: newExpiry,
                productionDate: newProduction
            )
        );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var items = await _client.GetItemsAsync(storage.Id);
        var item = Assert.Single(items);
        Assert.Equal("Oat Milk", item.Name);
        Assert.Equal(2.5m, item.Amount);
        Assert.Equal("Pack", item.Unit);
        Assert.Equal(newExpiry, item.ExpiryDate);
        Assert.Equal(newProduction, item.ProductionDate);
    }

    [Fact]
    public async Task UpdateItem_WithNegativeAmount_ReturnsBadRequestWithErrorCode()
    {
        // AC-07: negative amount rejected (0 means removal per AC-08)
        var storage = await _client.CreateStorageAsync("Pantry");
        var itemId = await _client.AddItemAsync(storage.Id, ItemBody(expiryDate: Today.AddDays(3)));

        var response = await _client.PutAsJsonAsync(
            $"/api/v1/storages/{storage.Id}/items/{itemId}",
            ItemBody(amount: -1m, expiryDate: Today.AddDays(3))
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("item.amount.notPositive", await response.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task UpdateItem_WithMoreThanOneDecimalPlace_ReturnsBadRequestWithErrorCode()
    {
        // AC-07 / EC-04
        var storage = await _client.CreateStorageAsync("Pantry");
        var itemId = await _client.AddItemAsync(storage.Id, ItemBody(expiryDate: Today.AddDays(3)));

        var response = await _client.PutAsJsonAsync(
            $"/api/v1/storages/{storage.Id}/items/{itemId}",
            ItemBody(amount: 1.25m, expiryDate: Today.AddDays(3))
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("item.amount.tooManyDecimals", await response.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task UpdateItem_WithoutAnyDate_ReturnsBadRequestWithErrorCode()
    {
        // AC-07 (same validation as AC-06)
        var storage = await _client.CreateStorageAsync("Pantry");
        var itemId = await _client.AddItemAsync(storage.Id, ItemBody(expiryDate: Today.AddDays(3)));

        var response = await _client.PutAsJsonAsync(
            $"/api/v1/storages/{storage.Id}/items/{itemId}",
            ItemBody()
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("item.dates.missing", await response.ReadErrorCodeAsync());
    }

    // --- AC-08: amount 0 removes the item ---

    [Fact]
    public async Task UpdateItem_WithAmountZero_ReturnsNoContentAndRemovesItem()
    {
        // AC-08
        var storage = await _client.CreateStorageAsync("Pantry");
        var itemId = await _client.AddItemAsync(storage.Id, ItemBody(expiryDate: Today.AddDays(3)));

        var response = await _client.PutAsJsonAsync(
            $"/api/v1/storages/{storage.Id}/items/{itemId}",
            ItemBody(amount: 0m, expiryDate: Today.AddDays(3))
        );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var items = await _client.GetItemsAsync(storage.Id);
        Assert.DoesNotContain(items, item => item.Id == itemId);
    }

    [Fact]
    public async Task UpdateItem_UnknownItem_ReturnsNotFound()
    {
        var storage = await _client.CreateStorageAsync("Pantry");

        var response = await _client.PutAsJsonAsync(
            $"/api/v1/storages/{storage.Id}/items/{Guid.NewGuid()}",
            ItemBody(expiryDate: Today.AddDays(3))
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("item.notFound", await response.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task UpdateItem_UnknownStorage_ReturnsNotFound()
    {
        var response = await _client.PutAsJsonAsync(
            $"/api/v1/storages/{Guid.NewGuid()}/items/{Guid.NewGuid()}",
            ItemBody(expiryDate: Today.AddDays(3))
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("storage.notFound", await response.ReadErrorCodeAsync());
    }

    // --- AC-09: delete item ---

    [Fact]
    public async Task DeleteItem_ExistingItem_ReturnsNoContentAndRemovesFromList()
    {
        // AC-09: removed regardless of amount
        var storage = await _client.CreateStorageAsync("Pantry");
        var itemId = await _client.AddItemAsync(
            storage.Id,
            ItemBody(amount: 5m, expiryDate: Today.AddDays(3))
        );

        var response = await _client.DeleteAsync($"/api/v1/storages/{storage.Id}/items/{itemId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var items = await _client.GetItemsAsync(storage.Id);
        Assert.DoesNotContain(items, item => item.Id == itemId);
    }

    [Fact]
    public async Task DeleteItem_UnknownItem_ReturnsNotFound()
    {
        var storage = await _client.CreateStorageAsync("Pantry");

        var response = await _client.DeleteAsync(
            $"/api/v1/storages/{storage.Id}/items/{Guid.NewGuid()}"
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("item.notFound", await response.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task DeleteItem_UnknownStorage_ReturnsNotFound()
    {
        var response = await _client.DeleteAsync(
            $"/api/v1/storages/{Guid.NewGuid()}/items/{Guid.NewGuid()}"
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("storage.notFound", await response.ReadErrorCodeAsync());
    }

    // --- #69: a malformed route id is a client error (400), not a missing resource ---

    [Fact]
    public async Task GetItems_MalformedStorageId_ReturnsBadRequestWithErrorCode()
    {
        var response = await _client.GetAsync("/api/v1/storages/abc/items");

        await response.AssertInvalidRouteIdAsync("storageId");
    }

    [Fact]
    public async Task AddItem_MalformedStorageId_ReturnsBadRequestWithErrorCode()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/storages/abc/items",
            ItemBody(expiryDate: Today.AddDays(3))
        );

        await response.AssertInvalidRouteIdAsync("storageId");
    }

    [Fact]
    public async Task UpdateItem_MalformedStorageId_ReturnsBadRequestWithErrorCode()
    {
        var response = await _client.PutAsJsonAsync(
            $"/api/v1/storages/abc/items/{Guid.NewGuid()}",
            ItemBody(expiryDate: Today.AddDays(3))
        );

        await response.AssertInvalidRouteIdAsync("storageId");
    }

    [Fact]
    public async Task UpdateItem_MalformedItemId_ReturnsBadRequestWithErrorCode()
    {
        var storage = await _client.CreateStorageAsync("MalformedItemIdUpdate");

        var response = await _client.PutAsJsonAsync(
            $"/api/v1/storages/{storage.Id}/items/abc",
            ItemBody(expiryDate: Today.AddDays(3))
        );

        await response.AssertInvalidRouteIdAsync("itemId");
    }

    [Fact]
    public async Task DeleteItem_MalformedStorageId_ReturnsBadRequestWithErrorCode()
    {
        var response = await _client.DeleteAsync($"/api/v1/storages/abc/items/{Guid.NewGuid()}");

        await response.AssertInvalidRouteIdAsync("storageId");
    }

    [Fact]
    public async Task DeleteItem_MalformedItemId_ReturnsBadRequestWithErrorCode()
    {
        var storage = await _client.CreateStorageAsync("MalformedItemIdDelete");

        var response = await _client.DeleteAsync($"/api/v1/storages/{storage.Id}/items/abc");

        await response.AssertInvalidRouteIdAsync("itemId");
    }

    // --- AC-10: sorting ---

    [Fact]
    public async Task GetItems_MixedExpiryDates_SortedByExpiryAscendingWithItemsWithoutExpiryLast()
    {
        // AC-10: sorted by expiry date ascending; items without expiry date last
        var storage = await _client.CreateStorageAsync("Pantry");
        var withoutExpiryId = await _client.AddItemAsync(
            storage.Id,
            ItemBody(name: "Flour", productionDate: Today.AddDays(-10))
        );
        var lateId = await _client.AddItemAsync(
            storage.Id,
            ItemBody(name: "Cheese", expiryDate: Today.AddDays(40))
        );
        var earlyId = await _client.AddItemAsync(
            storage.Id,
            ItemBody(name: "Milk", expiryDate: Today.AddDays(10))
        );
        var middleId = await _client.AddItemAsync(
            storage.Id,
            ItemBody(name: "Eggs", expiryDate: Today.AddDays(25))
        );

        var items = await _client.GetItemsAsync(storage.Id);

        Assert.Equal(
            new[] { earlyId, middleId, lateId, withoutExpiryId },
            items.Select(item => item.Id)
        );
    }

    // --- AC-11 / AC-12 / EC-02 / EC-05: server-side expiry status ---

    [Fact]
    public async Task GetItems_ItemsWithVariousExpiryDates_ExpiryStatusComputedPerSpec()
    {
        // AC-11 (within 3 days → ExpiringSoon), AC-12 (past → Expired),
        // EC-05 (production date only → Ok)
        var storage = await _client.CreateStorageAsync("Fridge");
        var expiredId = await _client.AddItemAsync(
            storage.Id,
            ItemBody(name: "Old Yogurt", expiryDate: Today.AddDays(-5))
        );
        var soonId = await _client.AddItemAsync(
            storage.Id,
            ItemBody(name: "Milk", expiryDate: Today.AddDays(1))
        );
        var okId = await _client.AddItemAsync(
            storage.Id,
            ItemBody(name: "Butter", expiryDate: Today.AddDays(30))
        );
        var noExpiryId = await _client.AddItemAsync(
            storage.Id,
            ItemBody(name: "Honey", productionDate: Today.AddDays(-100))
        );

        var items = await _client.GetItemsAsync(storage.Id);

        Assert.Equal("Expired", Assert.Single(items, i => i.Id == expiredId).ExpiryStatus);
        Assert.Equal("ExpiringSoon", Assert.Single(items, i => i.Id == soonId).ExpiryStatus);
        Assert.Equal("Ok", Assert.Single(items, i => i.Id == okId).ExpiryStatus);
        Assert.Equal("Ok", Assert.Single(items, i => i.Id == noExpiryId).ExpiryStatus);
    }

    [Fact]
    public async Task GetItems_ItemExpiringToday_MarkedExpiringSoon()
    {
        // EC-02: expiry exactly today → "expiring soon", not "expired"
        var storage = await _client.CreateStorageAsync("Fridge");
        var itemId = await _client.AddItemAsync(
            storage.Id,
            ItemBody(name: "Cream", expiryDate: Today)
        );

        var items = await _client.GetItemsAsync(storage.Id);

        Assert.Equal("ExpiringSoon", Assert.Single(items, i => i.Id == itemId).ExpiryStatus);
    }
}
