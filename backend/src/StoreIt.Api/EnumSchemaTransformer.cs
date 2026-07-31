using System.Text.Json;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace StoreIt.Api;

/// <summary>
/// .NET emits string enums (JsonStringEnumConverter) as a bare `enum: [...]` with no
/// `type`. This declares them as `type: string` so the published contract is a proper
/// JSON-Schema string enum — letting generated clients emit a typed enum plus a runtime
/// value list (SPEC-002). Contract-only; runtime serialisation is unchanged.
///
/// Guarded to string-valued enums only: a future numeric enum (no converter) keeps its
/// numeric wire type instead of being mislabelled as a string.
/// </summary>
internal sealed class EnumSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        if (schema.Type is null && schema.Enum is { Count: > 0 } values && AllStrings(values))
        {
            schema.Type = JsonSchemaType.String;
        }

        return Task.CompletedTask;
    }

    private static bool AllStrings(IEnumerable<System.Text.Json.Nodes.JsonNode?> values)
    {
        foreach (var value in values)
        {
            if (value is null || value.GetValueKind() != JsonValueKind.String)
            {
                return false;
            }
        }

        return true;
    }
}
