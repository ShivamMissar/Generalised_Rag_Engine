using Rag.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRagEngine(builder.Configuration);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<RagExceptionHandler>();
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    // A machine-readable description of every endpoint, at /openapi/v1.json.
    app.MapOpenApi();
}

app.MapHealthChecks("/health");
app.MapRagEndpoints();

app.Run();

// Lets the integration tests start this app in memory (WebApplicationFactory<Program>).
public partial class Program;
