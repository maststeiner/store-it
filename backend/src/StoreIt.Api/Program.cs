using System.Text.Json.Serialization;
using StoreIt.Api;
using StoreIt.Application;
using StoreIt.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// 12-factor: config strictly from the environment — no committed fallback.
// The connection string is resolved lazily in AddInfrastructure (required at
// runtime, not during build-time OpenAPI generation).
builder.Services.AddApplication();
builder.Services.AddInfrastructure();

// SPEC-003: BFF cookie session + per-provider OIDC challenge schemes.
builder.Services.AddStoreItAuthentication(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter())
);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi(
    "v1",
    options =>
    {
        options.AddSchemaTransformer<NumericSchemaTransformer>();
        options.AddSchemaTransformer<EnumSchemaTransformer>();
        options.AddOperationTransformer<RouteIdFormatTransformer>();
    }
);

var app = builder.Build();

app.UseExceptionHandler();

// Authentication runs before authorization. Secure-by-default (SPEC-003): a
// RequireAuthenticatedUser fallback policy guards every endpoint; the public ones
// below opt out explicitly with .AllowAnonymous().
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health").AllowAnonymous();
app.MapOpenApi().AllowAnonymous();
app.MapAuthEndpoints(); // the /auth group is already .AllowAnonymous()
app.MapStorageEndpointsV1();

await app.RunAsync();
