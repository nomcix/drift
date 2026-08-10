using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DirectiveDrift.Api;

namespace DirectiveDrift.Api.Tests;

public sealed class ScriptedRunHttpTests(P4ApiFactory application)
    : IClassFixture<P4ApiFactory>
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task CompleteScriptedRunIsIdempotentOwnedPaginatedAndReplayable()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        using var ownerClient = application.CreateClient();
        var csrf = await StartSessionAsync(ownerClient, cancellation.Token);
        var buildJson = await ReadDesignedBuildAsync(cancellation.Token);

        using var createBuild = await PostJsonAsync(
            ownerClient,
            "/api/v1/builds",
            buildJson,
            csrf,
            null,
            cancellation.Token);
        var createBuildBody = await createBuild.Content.ReadAsStringAsync(cancellation.Token);
        Assert.True(createBuild.StatusCode == HttpStatusCode.Created, createBuildBody);

        using var secondClient = application.CreateClient();
        await StartSessionAsync(secondClient, cancellation.Token);
        using var hiddenBuild = await secondClient.GetAsync(
            "/api/v1/builds/split-lantern",
            cancellation.Token);
        Assert.Equal(HttpStatusCode.NotFound, hiddenBuild.StatusCode);

        using var startRun = await PostJsonAsync(
            ownerClient,
            "/api/v1/runs",
            "{\"buildId\":\"split-lantern\",\"buildVersion\":1,\"variantId\":\"cs-practice-01\"}",
            csrf,
            null,
            cancellation.Token);
        var startBody = await startRun.Content.ReadAsStringAsync(cancellation.Token);
        Assert.True(startRun.StatusCode == HttpStatusCode.Created, startBody);
        var runId = JsonDocument.Parse(startBody).RootElement
            .GetProperty("runId")
            .GetProperty("value")
            .GetString();
        Assert.False(string.IsNullOrWhiteSpace(runId));

        var turn = 0;
        string? firstOperationId = null;
        JsonElement terminalRun = default;
        while (true)
        {
            using var runResponse = await ownerClient.GetAsync(
                $"/api/v1/runs/{runId}",
                cancellation.Token);
            runResponse.EnsureSuccessStatusCode();
            var run = JsonDocument.Parse(
                await runResponse.Content.ReadAsStringAsync(cancellation.Token)).RootElement;
            if (run.GetProperty("status").GetInt32() != 0)
            {
                Assert.Equal(1, run.GetProperty("status").GetInt32());
                terminalRun = run.Clone();
                break;
            }

            turn++;
            var idempotencyKey = $"http-turn-{turn}";
            using var enqueue = await PostJsonAsync(
                ownerClient,
                $"/api/v1/runs/{runId}/turns",
                "{}",
                csrf,
                idempotencyKey,
                cancellation.Token);
            var enqueueBody = await enqueue.Content.ReadAsStringAsync(cancellation.Token);
            Assert.Equal(HttpStatusCode.Accepted, enqueue.StatusCode);
            var operationId = JsonDocument.Parse(enqueueBody).RootElement
                .GetProperty("operationId")
                .GetString();
            Assert.False(string.IsNullOrWhiteSpace(operationId));
            await WaitForOperationAsync(ownerClient, operationId!, cancellation.Token);

            if (turn == 1)
            {
                firstOperationId = operationId;
                using var retryAfterCommit = await PostJsonAsync(
                    ownerClient,
                    $"/api/v1/runs/{runId}/turns",
                    "{}",
                    csrf,
                    idempotencyKey,
                    cancellation.Token);
                Assert.Equal(HttpStatusCode.Accepted, retryAfterCommit.StatusCode);
                var retriedId = JsonDocument.Parse(
                    await retryAfterCommit.Content.ReadAsStringAsync(cancellation.Token))
                    .RootElement.GetProperty("operationId").GetString();
                Assert.Equal(firstOperationId, retriedId);
            }

            Assert.InRange(turn, 1, 17);
        }

        using var eventsResponse = await ownerClient.GetAsync(
            $"/api/v1/runs/{runId}/events?afterSequence=-1&limit=500",
            cancellation.Token);
        eventsResponse.EnsureSuccessStatusCode();
        var events = JsonDocument.Parse(
            await eventsResponse.Content.ReadAsStringAsync(cancellation.Token)).RootElement;
        Assert.True(events.GetArrayLength() > turn);
        var sequences = events.EnumerateArray()
            .Select(value => value.GetProperty("sequence").GetInt64())
            .ToArray();
        Assert.Equal(sequences.Order(), sequences);

        using var replayResponse = await ownerClient.GetAsync(
            $"/api/v1/runs/{runId}/replay",
            cancellation.Token);
        replayResponse.EnsureSuccessStatusCode();
        var replay = JsonDocument.Parse(
            await replayResponse.Content.ReadAsStringAsync(cancellation.Token)).RootElement;
        Assert.Equal(events.GetArrayLength(), replay.GetProperty("events").GetArrayLength());
        Assert.Equal(turn * 2, replay.GetProperty("decisions").GetArrayLength());
        Assert.Equal(0, replay.GetProperty("initialState").GetProperty("turn").GetInt32());
        var succeeded = replay.GetProperty("events").EnumerateArray().Single(
            value => value.GetProperty("type").GetInt32() == 26);
        Assert.Equal(1330, succeeded.GetProperty("payload").GetProperty("score").GetInt32());
        var finalTurn = replay.GetProperty("events").EnumerateArray().Last(
            value => value.GetProperty("type").GetInt32() == 29);
        Assert.Equal(
            terminalRun.GetProperty("stateHash").GetString(),
            finalTurn.GetProperty("payload").GetProperty("stateHash").GetString());
        Assert.False(string.IsNullOrWhiteSpace(firstOperationId));
    }

    [Fact]
    public async Task GenericOnboardingBuildFailsWithConsoleSyncEvidence()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        using var client = application.CreateClient();
        var csrf = await StartSessionAsync(client, cancellation.Token);
        var buildJson = await ReadBuildAsync("generic-optimal-build.json", cancellation.Token);
        using var createBuild = await PostJsonAsync(
            client, "/api/v1/builds", buildJson, csrf, null, cancellation.Token);
        Assert.Equal(HttpStatusCode.Created, createBuild.StatusCode);
        using var startRun = await PostJsonAsync(
            client,
            "/api/v1/runs",
            "{\"buildId\":\"generic-optimal\",\"buildVersion\":1,\"variantId\":\"cs-practice-01\"}",
            csrf,
            null,
            cancellation.Token);
        var startBody = await startRun.Content.ReadAsStringAsync(cancellation.Token);
        Assert.True(startRun.StatusCode == HttpStatusCode.Created, startBody);
        var runId = JsonDocument.Parse(startBody).RootElement.GetProperty("runId").GetProperty("value").GetString()!;

        for (var turn = 1; turn <= 18; turn++)
        {
            using var enqueue = await PostJsonAsync(
                client,
                $"/api/v1/runs/{runId}/turns",
                "{}",
                csrf,
                $"generic-turn-{turn}",
                cancellation.Token);
            Assert.Equal(HttpStatusCode.Accepted, enqueue.StatusCode);
            var operationId = JsonDocument.Parse(await enqueue.Content.ReadAsStringAsync(cancellation.Token))
                .RootElement.GetProperty("operationId").GetString()!;
            await WaitForOperationAsync(client, operationId, cancellation.Token);
            using var runResponse = await client.GetAsync($"/api/v1/runs/{runId}", cancellation.Token);
            var run = JsonDocument.Parse(await runResponse.Content.ReadAsStringAsync(cancellation.Token)).RootElement;
            if (run.GetProperty("status").GetInt32() == 0)
            {
                continue;
            }

            Assert.Equal(2, run.GetProperty("status").GetInt32());
            break;
        }

        using var replayResponse = await client.GetAsync($"/api/v1/runs/{runId}/replay", cancellation.Token);
        var replay = JsonDocument.Parse(await replayResponse.Content.ReadAsStringAsync(cancellation.Token)).RootElement;
        Assert.Contains(replay.GetProperty("events").EnumerateArray(), value => value.GetProperty("type").GetInt32() == 17);
        Assert.DoesNotContain(replay.GetProperty("events").EnumerateArray(), value => value.GetProperty("type").GetInt32() == 26);
    }

    [Fact]
    public async Task OpenApiDocumentContainsP4Routes()
    {
        using var client = application.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/api/v1/builds", out _));
        Assert.True(paths.TryGetProperty("/api/v1/runs", out _));
        Assert.True(paths.TryGetProperty("/api/v1/runs/{runId}/turns", out _));
        Assert.True(paths.TryGetProperty("/api/v1/runs/{runId}/replay", out _));
    }

    private static async Task<string> StartSessionAsync(
        HttpClient client,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync("/api/v1/runtime", cancellationToken);
        response.EnsureSuccessStatusCode();
        var cookies = response.Headers.GetValues("Set-Cookie");
        var csrfCookie = cookies.Single(value => value.StartsWith("dd_csrf=", StringComparison.Ordinal));
        return csrfCookie["dd_csrf=".Length..csrfCookie.IndexOf(';')];
    }

    private static async Task<HttpResponseMessage> PostJsonAsync(
        HttpClient client,
        string path,
        string json,
        string csrf,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add(GuestSessionMiddleware.CsrfHeader, csrf);
        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        return await client.SendAsync(request, cancellationToken);
    }

    private static async Task WaitForOperationAsync(
        HttpClient client,
        string operationId,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            using var response = await client.GetAsync(
                $"/api/v1/operations/{operationId}",
                cancellationToken);
            response.EnsureSuccessStatusCode();
            var operation = await response.Content.ReadFromJsonAsync<JsonElement>(
                WebJson,
                cancellationToken);
            var status = operation.GetProperty("status").GetInt32();
            if (status == 2)
            {
                return;
            }

            Assert.NotEqual(3, status);
            Assert.NotEqual(4, status);
            await Task.Delay(20, cancellationToken);
        }
    }

    private static async Task<string> ReadDesignedBuildAsync(CancellationToken cancellationToken)
        => await ReadBuildAsync("designed-build.json", cancellationToken);

    private static async Task<string> ReadBuildAsync(
        string fileName,
        CancellationToken cancellationToken)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "DirectiveDrift.sln")))
        {
            current = current.Parent;
        }

        var root = current?.FullName
            ?? throw new InvalidOperationException("Could not locate the repository test fixture root.");
        return await File.ReadAllTextAsync(Path.Combine(root, "examples", fileName), cancellationToken);
    }
}
