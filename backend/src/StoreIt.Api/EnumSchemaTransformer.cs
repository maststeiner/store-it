using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace StoreIt.Api;

/// <summary>
/// .NET emits string enums (JsonStringEnumConverter) as a bare `enum: [...]` with no
/// `type`. This declares them as `type: string` so the published contract is a proper
/// JSON-Schema string enum — letting generated clients emit a typed enum plus a runtime
/// value list (SPEC-002). Contract-only; runtime serialisation is unchanged.
///
/// Only typeless enums are touched: numeric enums (no string converter) already carry an
/// explicit `type: integer`, so `Type is null` selects exactly the string-serialised ones.
/// </summary>
internal sealed class EnumSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        if (schema.Type is null && schema.Enum is { Count: > 0 })
        {
            schema.Type = JsonSchemaType.String;
        }

        return Task.CompletedTask;
    }
}
