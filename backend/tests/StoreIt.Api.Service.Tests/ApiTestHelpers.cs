using System.Net.Http.Json;
using System.Text.Json;

namespace StoreIt.Api.Service.Tests;

public sealed record StorageResponse(
    Guid Id,
    string Name,
    int ItemCount,
    int ExpiredCount,
    int ExpiringSoonCount
);

public sealed record ItemResponse(
    Guid Id,
    string Name,
    decimal Amount,
    string Unit,
    DateOnly? ExpiryDate,
    DateOnly? ProductionDate,
    string ExpiryStatus
);

/// <summary>
/// Black-box helpers for the public REST contract (backend/openapi/StoreIt.Api.json).
/// The contract marks expiryDate/productionDate as required nullable properties,
/// so request bodies always carry both keys explicitly.
/// </summary>
internal static class ApiTestHelpers
{
    public static object ItemBody(
        string name = "Milk",
        decimal amount = 1m,
        string unit = "Piece",
        DateOnly? expiryDate = null,
        DateOnly? productionDate = null
    ) =>
        new
        {
            name,
            amount,
            unit,
            expiryDate,
            productionDate,
        };

    public static async Task<StorageResponse> CreateStorageAsync(
        this HttpClient client,
        string name
    )
    {
        var response = await client.PostAsJsonAsync("/api/v1/storages", new { name });
        response.EnsureSuccessStatusCode();
        var storage = await response.Content.ReadFromJsonAsync<StorageResponse>();
        Assert.NotNull(storage);
        return storage;
    }

    public static async Task<Guid> AddItemAsync(
        this HttpClient client,
        Guid storageId,
        object request
    )
    {
        var response = await client.PostAsJsonAsync($"/api/v1/storages/{storageId}/items", request);
        if (!response.IsSuccessStatusCode)
        {
            Assert.Fail(
                $"AddItem failed: {(int)response.StatusCode} — {await response.Content.ReadAsStringAsync()}"
            );
        }
        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    public static async Task<IReadOnlyList<StorageResponse>> GetStoragesAsync(
        this HttpClient client
    )
    {
        var storages = await client.GetFromJsonAsync<List<StorageResponse>>("/api/v1/storages");
        Assert.NotNull(storages);
        return storages;
    }

    public static async Task<StorageResponse> GetStorageAsync(this HttpClient client, Guid id)
    {
        var storage = await client.GetFromJsonAsync<StorageResponse>($"/api/v1/storages/{id}");
        Assert.NotNull(storage);
        return storage;
    }

    public static async Task<IReadOnlyList<ItemResponse>> GetItemsAsync(
        this HttpClient client,
        Guid storageId
    )
    {
        var items = await client.GetFromJsonAsync<List<ItemResponse>>(
            $"/api/v1/storages/{storageId}/items"
        );
        Assert.NotNull(items);
        return items;
    }

    /// <summary>Reads the "errorCode" extension from a ProblemDetails response body.</summary>
    public static async Task<string?> ReadErrorCodeAsync(this HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("errorCode").GetString();
    }
}
