var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");

app.Run();

/// <summary>Public for architecture tests and service tests (WebApplicationFactory).</summary>
public partial class Program { }
