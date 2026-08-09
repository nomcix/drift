using DirectiveDrift.AI;
using DirectiveDrift.Api;
using DirectiveDrift.Application;
using DirectiveDrift.Application.Ports;
using DirectiveDrift.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);
var repositoryRoot = ColdStartRuntimeCatalog.FindRepositoryRoot(builder.Environment.ContentRootPath);
var runtimeCatalog = await ColdStartRuntimeCatalog.LoadAsync(
    repositoryRoot,
    CancellationToken.None);

builder.Services
    .AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["ready"]);
builder.Services.AddOpenApi("v1");
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(runtimeCatalog);
builder.Services.AddDirectiveDriftPersistence(
    _ => builder.Configuration.GetConnectionString("DirectiveDrift")
        ?? $"Data Source={Path.Combine(builder.Environment.ContentRootPath, "directive-drift.db")}");
builder.Services.AddSingleton<IAgentDecisionProvider, ScriptedDecisionProvider>();
builder.Services.AddSingleton<IUsageReservationService, ScriptedUsageReservationService>();
builder.Services.AddSingleton<TurnOperationProcessor>();
if (builder.Configuration.GetValue("TurnWorker:Enabled", true))
{
    builder.Services.AddHostedService<TurnWorker>();
}

var app = builder.Build();

if (app.Environment.IsDevelopment()
    || app.Configuration.GetValue("Database:MigrateOnStartup", false))
{
    await app.Services.InitializeDirectiveDriftDatabaseAsync(CancellationToken.None);
}

app.MapHealthChecks(
    "/health/live",
    new HealthCheckOptions
    {
        Predicate = _ => false,
    });

app.MapHealthChecks(
    "/health/ready",
    new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("ready"),
    });

app.MapOpenApi("/openapi/{documentName}.json");
app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/api"),
    branch => branch.UseMiddleware<GuestSessionMiddleware>());
app.MapDirectiveDriftApi();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
