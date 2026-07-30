using System.Text.Json;

namespace StoreIt.Api.Service.Tests;

public sealed class OpenApiContractTests(ApiTestFixture factory) : IClassFixture<ApiTestFixture>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<JsonElement> GetContractAsync()
    {
        var json = await _client.GetStringAsync("/openapi/v1.json");
        return JsonDocument.Parse(json).RootElement;
    }

    [Fact]
    public async Task Endpoints_have_stable_operation_ids()
    {
        var root = await GetContractAsync();
        var paths = root.GetProperty("paths");

        Assert.Equal(
            "getStorages",
            paths.GetProperty("/api/v1/storages").GetProperty("get").GetProperty("operationId").GetString());
        Assert.Equal(
            "createStorage",
            paths.GetProperty("/api/v1/storages").GetProperty("post").GetProperty("operationId").GetString());
        Assert.Equal(
            "addItem",
            paths.GetProperty("/api/v1/storages/{storageId}/items").GetProperty("post").GetProperty("operationId").GetString());
    }
}
