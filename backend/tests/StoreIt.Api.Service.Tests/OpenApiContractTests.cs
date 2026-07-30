using System.Text.Json;
using Microsoft.OpenApi;

namespace StoreIt.Api.Service.Tests;

public sealed class OpenApiContractTests(ApiTestFixture factory) : IClassFixture<ApiTestFixture>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<JsonElement> GetContractAsync()
    {
        var json = await _client.GetStringAsync("/openapi/v1.json");
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    [Fact]
    public async Task Endpoints_have_stable_operation_ids()
    {
        var root = await GetContractAsync();
        var paths = root.GetProperty("paths");

        static string OperationId(JsonElement paths, string path, string verb) =>
            paths.GetProperty(path).GetProperty(verb).GetProperty("operationId").GetString()!;

        Assert.Equal("getStorages", OperationId(paths, "/api/v1/storages", "get"));
        Assert.Equal("createStorage", OperationId(paths, "/api/v1/storages", "post"));
        Assert.Equal("renameStorage", OperationId(paths, "/api/v1/storages/{storageId}", "put"));
        Assert.Equal("deleteStorage", OperationId(paths, "/api/v1/storages/{storageId}", "delete"));
        Assert.Equal("getItems", OperationId(paths, "/api/v1/storages/{storageId}/items", "get"));
        Assert.Equal("addItem", OperationId(paths, "/api/v1/storages/{storageId}/items", "post"));
        Assert.Equal("updateItem", OperationId(paths, "/api/v1/storages/{storageId}/items/{itemId}", "put"));
        Assert.Equal("deleteItem", OperationId(paths, "/api/v1/storages/{storageId}/items/{itemId}", "delete"));
    }

    [Fact]
    public async Task Numeric_properties_are_plain_numbers_without_string_union()
    {
        var root = await GetContractAsync();
        var schemas = root.GetProperty("components").GetProperty("schemas");

        var amount = schemas
            .GetProperty("ItemResponse")
            .GetProperty("properties")
            .GetProperty("amount");
        // single "number", not a ["number","string"] array
        Assert.Equal(JsonValueKind.String, amount.GetProperty("type").ValueKind);
        Assert.Equal("number", amount.GetProperty("type").GetString());
        Assert.False(amount.TryGetProperty("pattern", out _));

        var count = schemas
            .GetProperty("StorageResponse")
            .GetProperty("properties")
            .GetProperty("itemCount");
        Assert.Equal("integer", count.GetProperty("type").GetString());

        // dates must stay nullable strings — the transformer must not touch them
        var expiryDate = schemas
            .GetProperty("ItemResponse")
            .GetProperty("properties")
            .GetProperty("expiryDate")
            .GetProperty("type");
        Assert.Equal(JsonValueKind.Array, expiryDate.ValueKind);
    }
}

public sealed class NumericSchemaTransformerTests
{
    [Fact]
    public async Task Collapses_number_or_string_to_number_and_drops_pattern()
    {
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Number | JsonSchemaType.String,
            Pattern = @"^-?(?:0|[1-9]\d*)(?:\.\d+)?$",
            Format = "double",
        };

        await new NumericSchemaTransformer().TransformAsync(schema, null!, CancellationToken.None);

        Assert.Equal(JsonSchemaType.Number, schema.Type);
        Assert.Null(schema.Pattern);
        Assert.Equal("double", schema.Format);
    }

    [Fact]
    public async Task Collapses_integer_or_string_to_integer()
    {
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Integer | JsonSchemaType.String,
            Pattern = @"^-?(?:0|[1-9]\d*)$",
        };

        await new NumericSchemaTransformer().TransformAsync(schema, null!, CancellationToken.None);

        Assert.Equal(JsonSchemaType.Integer, schema.Type);
        Assert.Null(schema.Pattern);
    }

    [Fact]
    public async Task Leaves_nullable_string_dates_untouched()
    {
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Null | JsonSchemaType.String,
            Format = "date",
        };

        await new NumericSchemaTransformer().TransformAsync(schema, null!, CancellationToken.None);

        Assert.Equal(JsonSchemaType.Null | JsonSchemaType.String, schema.Type);
    }
}
