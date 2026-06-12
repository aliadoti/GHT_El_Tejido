var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new HealthResponse("ok")))
    .WithName("Health")
    .WithSummary("Liveness endpoint for App Service and CI smoke tests.");

app.Run();

public sealed record HealthResponse(string Status);

public partial class Program;
