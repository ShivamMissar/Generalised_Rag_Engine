using Rag.Api;
using Rag.Api.Protection;

var builder = WebApplication.CreateBuilder(args);

// The hosted web page (on Netlify) calls this API from a different address, which browsers only allow for the
// origins listed here.
var allowedOrigins = builder.Configuration.GetSection("Rag:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(cors => cors.AddDefaultPolicy(policy =>
    policy.WithOrigins(allowedOrigins).WithMethods("GET", "POST", "DELETE").WithHeaders("Content-Type")));

builder.Services.AddRagEngine(builder.Configuration);
builder.Services.AddRagProtection();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<RagExceptionHandler>();
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseCors();
app.UseRateLimiter();

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
