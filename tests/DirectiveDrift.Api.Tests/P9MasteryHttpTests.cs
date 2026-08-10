using System.Net;
using System.Text;
using System.Text.Json;
using DirectiveDrift.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DirectiveDrift.Api.Tests;

public sealed class P9MasteryHttpTests(P4ApiFactory application) : IClassFixture<P4ApiFactory>
{
    [Fact]
    public async Task RandomPracticeRevealsSafeMaterializationAndBurstMarksAssisted()
    {
        using var client = application.CreateClient();
        var session = await StartSessionAsync(client);
        var build = await ReadBuildAsync();
        build = build.Replace("\"split-lantern\"", "\"random-practice-build\"", StringComparison.Ordinal);
        using var create = await PostAsync(client, "/api/v1/builds", build, session.Csrf);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        using var start = await PostAsync(
            client,
            "/api/v1/runs",
            "{\"buildId\":\"random-practice-build\",\"buildVersion\":1,\"variantId\":\"cs-practice-random\"}",
            session.Csrf);
        var startJson = await start.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Created, start.StatusCode);
        Assert.Contains("cs-practice-random-", startJson, StringComparison.Ordinal);
        Assert.Contains("mutations", startJson, StringComparison.Ordinal);
        Assert.Contains("seed", startJson, StringComparison.OrdinalIgnoreCase);
        var runId = JsonDocument.Parse(startJson).RootElement.GetProperty("runId").GetProperty("value").GetString()!;

        using var burst = await PostAsync(
            client,
            $"/api/v1/runs/{runId}/emergency-burst",
            "{\"text\":\"Regroup and use the safe route.\"}",
            session.Csrf);
        var burstJson = await burst.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, burst.StatusCode);
        Assert.True(JsonDocument.Parse(burstJson).RootElement.GetProperty("assisted").GetBoolean());
        using var allowance = await client.GetAsync("/api/v1/usage-allowance");
        allowance.EnsureSuccessStatusCode();
        var allowanceJson = JsonDocument.Parse(await allowance.Content.ReadAsStringAsync()).RootElement;
        Assert.True(allowanceJson.TryGetProperty("remainingMicros", out _));
        Assert.True(allowanceJson.TryGetProperty("scriptedRunsRemaining", out _));
    }

    [Fact]
    public async Task CertificationResponseAndRunEndpointsKeepSelectedVariantsServerSide()
    {
        using var client = application.CreateClient();
        var session = await StartSessionAsync(client);
        var build = (await ReadBuildAsync()).Replace(
            "\"split-lantern\"", "\"certification-build\"", StringComparison.Ordinal);
        using var create = await PostAsync(client, "/api/v1/builds", build, session.Csrf);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        await SeedEligibilityAsync(session.OwnerId);

        using var start = await PostAsync(
            client,
            "/api/v1/certifications",
            "{\"buildId\":\"certification-build\",\"buildVersion\":1}",
            session.Csrf);
        var body = await start.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Created, start.StatusCode);
        Assert.DoesNotContain("cs-cert-", body, StringComparison.Ordinal);
        var certificate = JsonDocument.Parse(body).RootElement;
        Assert.False(certificate.GetProperty("revealed").GetBoolean());
        Assert.Equal(3, certificate.GetProperty("runs").GetArrayLength());
        var runId = certificate.GetProperty("runs")[0].GetProperty("runId").GetProperty("value").GetString()!;

        using var run = await client.GetAsync($"/api/v1/runs/{runId}");
        var runBody = await run.Content.ReadAsStringAsync();
        Assert.DoesNotContain("cs-cert-", runBody, StringComparison.Ordinal);
        Assert.Equal(JsonValueKind.Null, JsonDocument.Parse(runBody).RootElement.GetProperty("variant").ValueKind);
        using var events = await client.GetAsync($"/api/v1/runs/{runId}/events?afterSequence=-1");
        Assert.Equal("[]", await events.Content.ReadAsStringAsync());
        using var replay = await client.GetAsync($"/api/v1/runs/{runId}/replay");
        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
        using var share = await client.GetAsync($"/api/v1/runs/{runId}/share");
        Assert.Equal(HttpStatusCode.Conflict, share.StatusCode);
    }

    private async Task SeedEligibilityAsync(string ownerId)
    {
        var factory = application.Services.GetRequiredService<IDbContextFactory<GameDbContext>>();
        await using var database = await factory.CreateDbContextAsync();
        for (var index = 1; index <= 3; index++)
        {
            database.Runs.Add(new RunEntity
            {
                Id = $"eligibility-{Guid.NewGuid():N}",
                OwnerId = ownerId,
                BuildId = "certification-build",
                BuildVersion = 1,
                MissionId = "cold-start",
                VariantId = $"cs-practice-0{index}",
                Turn = 16,
                Status = 1,
                StateHash = "eligible",
                ProviderProfileId = "scripted-reference-v1",
                ScriptedPlanJson = "[]",
                Kind = 0,
                Assisted = false,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
        }
        await database.SaveChangesAsync();
    }

    private static async Task<(string Csrf, string OwnerId)> StartSessionAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/v1/runtime");
        response.EnsureSuccessStatusCode();
        var cookies = response.Headers.GetValues("Set-Cookie").ToArray();
        static string Value(string cookie) => cookie[(cookie.IndexOf('=') + 1)..cookie.IndexOf(';')];
        return (
            Value(cookies.Single(value => value.StartsWith("dd_csrf=", StringComparison.Ordinal))),
            Value(cookies.Single(value => value.StartsWith("dd_guest=", StringComparison.Ordinal))));
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, string json, string csrf)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add(GuestSessionMiddleware.CsrfHeader, csrf);
        return await client.SendAsync(request);
    }

    private static async Task<string> ReadBuildAsync()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "DirectiveDrift.sln"))) current = current.Parent;
        return await File.ReadAllTextAsync(Path.Combine(current!.FullName, "examples", "designed-build.json"));
    }
}
