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

// SPEC-003 (Task 8a): double-submit CSRF protection for cookie-authenticated mutations.
// The SPA reads the JS-readable XSRF-TOKEN cookie (set by GET /auth/csrf) and echoes it
// back as the X-XSRF-TOKEN request header; the antiforgery middleware validates the pair.
builder.Services.AddAntiforgery(options => options.HeaderName = "X-XSRF-TOKEN");

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
// SPEC-003 (Task 8a): CSRF middleware after auth so it can access the user context.
// Mutation endpoints validate the double-submit token pair via an endpoint filter
// (see StorageEndpoints); the middleware itself is placed here for correctness but
// ValidateRequestAsync is called per-endpoint rather than globally, so GET/HEAD/HEAD
// requests and anonymous endpoints (auth, health) are unaffected.
app.UseAntiforgery();

app.MapHealthChecks("/health").AllowAnonymous();
app.MapOpenApi().AllowAnonymous();
app.MapAuthEndpoints(); // the /auth group is already .AllowAnonymous()
app.MapStorageEndpointsV1();

// ⚠️  DEVELOPMENT ONLY — never reachable in Staging or Production.
// Provides a POST /auth/dev-login shortcut for Playwright E2E tests so they can
// establish a real cookie session without an OIDC provider. The endpoint runs the
// same ProvisionUserUseCase + sub_local claim contract as the production OIDC flow.
if (app.Environment.IsDevelopment())
{
    app.MapDevAuthEndpoints();
}

await app.RunAsync();
