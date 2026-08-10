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
builder.Services.AddSingleton<IAgentTurnContextFactory>(
    _ => new AgentTurnContextFactory(runtimeCatalog.Mission));
builder.Services.AddDirectiveDriftPersistence(
    _ => builder.Configuration.GetConnectionString("DirectiveDrift")
        ?? $"Data Source={Path.Combine(builder.Environment.ContentRootPath, "directive-drift.db")}");
builder.Services.AddHttpClient(
    "openai",
    client => client.BaseAddress = new Uri(
        builder.Configuration["Provider:BaseUrl"] ?? "https://api.openai.com/"));
builder.Services.AddSingleton<IAgentDecisionProvider>(serviceProvider =>
{
    var mode = builder.Configuration["Provider:Mode"] ?? "scripted";
    if (string.Equals(mode, "scripted", StringComparison.OrdinalIgnoreCase))
    {
        return new ScriptedDecisionProvider();
    }

    if (string.Equals(mode, "fake", StringComparison.OrdinalIgnoreCase))
    {
        return new StructuredDecisionProvider(
            ProviderProfiles.Fake,
            new FakeProviderTransport());
    }

    if (!string.Equals(mode, "live", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException($"Unknown provider mode '{mode}'.");
    }

    var apiKey = builder.Configuration["Provider:ApiKey"];
    if (string.IsNullOrWhiteSpace(apiKey))
    {
        throw new InvalidOperationException(
            "Provider:ApiKey is required when Provider:Mode is live.");
    }

    var configured = ProviderProfiles.OpenAi with
    {
        Model = builder.Configuration["Provider:Model"] ?? ProviderProfiles.OpenAi.Model,
        RunCostCapMicros = builder.Configuration.GetValue<int?>("Provider:RunCostCapMicros")
            ?? ProviderProfiles.OpenAi.RunCostCapMicros,
        GuestDailyCostCapMicros = builder.Configuration.GetValue<int?>("Provider:GuestDailyCostCapMicros")
            ?? ProviderProfiles.OpenAi.GuestDailyCostCapMicros,
        DeploymentDailyCostCapMicros = builder.Configuration.GetValue<int?>("Provider:DeploymentDailyCostCapMicros")
            ?? ProviderProfiles.OpenAi.DeploymentDailyCostCapMicros,
    };
    var client = serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("openai");
    return new StructuredDecisionProvider(
        configured,
        new OpenAiResponsesTransport(client, apiKey));
});
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
