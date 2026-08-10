using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using DirectiveDrift.Application;
using DirectiveDrift.Application.Models;
using DirectiveDrift.Application.Ports;
using DirectiveDrift.Content.Authoring;
using DirectiveDrift.Content.Loading;
using DirectiveDrift.Content.Materialization;
using DirectiveDrift.Content.Solving;
using DirectiveDrift.Content.Validation;
using DirectiveDrift.Core.Events;
using DirectiveDrift.Core.Model;
using DirectiveDrift.Core.Serialization;
using DirectiveDrift.Core.Simulation;

namespace DirectiveDrift.Api;

public static class ApiEndpoints
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapDirectiveDriftApi(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api/v1");

        api.MapGet(
            "/runtime",
            (IAgentDecisionProvider provider) => new RuntimeResponse(
                "v1",
                provider.Profile.Mode.ToString().ToLowerInvariant(),
                CanonicalStateSerializer.Version));
        api.MapGet(
            "/missions",
            (ColdStartRuntimeCatalog catalog) => new[]
            {
                new
                {
                    missionId = catalog.Mission.Authoring.MissionId,
                    title = catalog.Mission.Authoring.Title,
                    contentVersion = catalog.Mission.Authoring.ContentVersion,
                },
            });
        api.MapGet(
            "/missions/{missionId}",
            (string missionId, ColdStartRuntimeCatalog catalog) =>
                string.Equals(missionId, catalog.Mission.Authoring.MissionId, StringComparison.Ordinal)
                    ? Results.Ok(catalog.Mission.Authoring)
                    : NotFound("mission-not-found"));
        api.MapGet(
            "/missions/{missionId}/practice-variants",
            (string missionId, ColdStartRuntimeCatalog catalog) =>
                string.Equals(missionId, catalog.Mission.Authoring.MissionId, StringComparison.Ordinal)
                    ? Results.Ok(
                        catalog.Variants.PracticeVariants.Select(
                            variant => new
                            {
                                variant.VariantId,
                                variant.Label,
                                Seed = (ulong?)null,
                                variant.Mutations,
                            }).Append(new
                            {
                                VariantId = "cs-practice-random",
                                Label = "Safe random practice",
                                Seed = (ulong?)0,
                                Mutations = (IReadOnlyList<MutationDocument>)[],
                            }))
                    : NotFound("mission-not-found"));

        api.MapPost("/builds", CreateBuildAsync);
        api.MapGet(
            "/builds",
            async (HttpContext context, IGameRepository repository, CancellationToken token) =>
                Results.Ok(
                    await repository.ListBuildsAsync(
                        GuestSessionMiddleware.Owner(context),
                        token)));
        api.MapGet("/builds/{buildId}", GetBuildAsync);
        api.MapPost("/builds/{buildId}/versions", AddBuildVersionAsync);
        api.MapGet(
            "/builds/{buildId}/versions",
            async (
                HttpContext context,
                string buildId,
                IGameRepository repository,
                CancellationToken token) =>
            {
                var values = await repository.ListBuildVersionsAsync(
                    GuestSessionMiddleware.Owner(context),
                    buildId,
                    token);
                return values.Length == 0 ? NotFound("build-not-found") : Results.Ok(values);
            });

        api.MapPost("/runs", StartRunAsync);
        api.MapGet(
            "/runs/{runId}",
            async (
                HttpContext context,
                string runId,
                IGameRepository repository,
                CancellationToken token) =>
            {
                var run = await repository.GetRunAsync(
                    GuestSessionMiddleware.Owner(context),
                    new RunId(runId),
                    token);
                if (run is null)
                {
                    return NotFound("run-not-found");
                }
                var revealed = await IsRunRevealedAsync(
                    context,
                    run,
                    context.RequestServices.GetRequiredService<IMasteryRepository>(),
                    token);
                return Results.Ok(ToPublicRun(run, revealed));
            });
        api.MapPost("/runs/{runId}/turns", EnqueueTurnAsync);
        api.MapGet(
            "/operations/{operationId}",
            async (
                HttpContext context,
                string operationId,
                IGameRepository repository,
                CancellationToken token) =>
            {
                var operation = await repository.GetOperationAsync(
                    GuestSessionMiddleware.Owner(context),
                    operationId,
                    token);
                return operation is null ? NotFound("operation-not-found") : Results.Ok(operation);
            });
        api.MapGet("/runs/{runId}/events", GetEventsAsync);
        api.MapGet("/runs/{runId}/replay", GetReplayAsync);
        api.MapPost("/runs/{runId}/emergency-burst", ApplyEmergencyBurstAsync);
        api.MapPost("/certifications", StartCertificationAsync);
        api.MapGet(
            "/certifications/{certificationId}",
            async (HttpContext context, string certificationId, IMasteryRepository repository, CancellationToken token) =>
            {
                var certificate = await repository.GetCertificationAsync(
                    GuestSessionMiddleware.Owner(context), certificationId, token);
                return certificate is null ? NotFound("certification-not-found") : Results.Ok(certificate);
            });
        api.MapGet("/comparisons", GetComparisonAsync);
        api.MapGet(
            "/usage-allowance",
            async (HttpContext context, IMasteryRepository repository, IAgentDecisionProvider provider, TimeProvider timeProvider, CancellationToken token) =>
                Results.Ok(await repository.GetUsageAllowanceAsync(
                    GuestSessionMiddleware.Owner(context),
                    provider.Profile.GuestDailyCostCapMicros,
                    timeProvider.GetUtcNow(),
                    token)));
        api.MapGet("/runs/{runId}/share", GetShareAsync);
        api.MapGet("/runs/{runId}/share-card.svg", GetShareCardAsync);

        return endpoints;
    }

    private static async Task<IResult> CreateBuildAsync(
        HttpContext context,
        JsonElement body,
        ColdStartRuntimeCatalog catalog,
        IGameRepository repository,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var validated = ValidateBuild(body.GetRawText(), catalog);
        if (validated.Document is null)
        {
            return ValidationProblem(validated.Errors);
        }

        var document = validated.Document;
        if (document.Version != 1)
        {
            return Conflict("build-version-conflict", "A new build must start at version 1.");
        }

        try
        {
            var created = await repository.CreateBuildAsync(
                GuestSessionMiddleware.Owner(context),
                document.BuildId,
                document.MissionId,
                document.Name,
                ContractJson.Serialize(document),
                timeProvider.GetUtcNow(),
                cancellationToken);
            return Results.Created($"/api/v1/builds/{document.BuildId}", created);
        }
        catch (ResourceConflictException)
        {
            return Conflict("build-id-conflict", "The build ID is already in use.");
        }
    }

    private static async Task<IResult> GetBuildAsync(
        HttpContext context,
        string buildId,
        IGameRepository repository,
        CancellationToken cancellationToken)
    {
        var versions = await repository.ListBuildVersionsAsync(
            GuestSessionMiddleware.Owner(context),
            buildId,
            cancellationToken);
        return versions.Length == 0
            ? NotFound("build-not-found")
            : Results.Content(versions[^1].CanonicalJson, "application/json");
    }

    private static async Task<IResult> AddBuildVersionAsync(
        HttpContext context,
        string buildId,
        JsonElement body,
        ColdStartRuntimeCatalog catalog,
        IGameRepository repository,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var validated = ValidateBuild(body.GetRawText(), catalog);
        if (validated.Document is null)
        {
            return ValidationProblem(validated.Errors);
        }

        var existing = await repository.ListBuildVersionsAsync(
            GuestSessionMiddleware.Owner(context),
            buildId,
            cancellationToken);
        if (existing.Length == 0)
        {
            return NotFound("build-not-found");
        }

        var expectedVersion = existing[^1].Version + 1;
        if (!string.Equals(validated.Document.BuildId, buildId, StringComparison.Ordinal)
            || validated.Document.Version != expectedVersion)
        {
            return Conflict(
                "build-version-conflict",
                $"The next immutable version must be {expectedVersion} for build '{buildId}'.");
        }

        var created = await repository.AddBuildVersionAsync(
            GuestSessionMiddleware.Owner(context),
            buildId,
            ContractJson.Serialize(validated.Document),
            timeProvider.GetUtcNow(),
            cancellationToken);
        return Results.Created($"/api/v1/builds/{buildId}", created);
    }

    private static async Task<IResult> StartRunAsync(
        HttpContext context,
        StartRunRequest request,
        ColdStartRuntimeCatalog catalog,
        IGameRepository repository,
        DirectiveDrift.Application.Ports.IAgentDecisionProvider provider,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var build = await repository.GetBuildVersionAsync(
            GuestSessionMiddleware.Owner(context),
            request.BuildId,
            request.BuildVersion,
            cancellationToken);
        if (build is null)
        {
            return NotFound("build-version-not-found");
        }

        VariantDocument? variant;
        ulong? randomSeed = null;
        if (string.Equals(request.VariantId, "cs-practice-random", StringComparison.Ordinal))
        {
            randomSeed = BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(sizeof(ulong)));
            variant = ColdStartRandomMaterializer.Materialize(
                catalog.Mission,
                catalog.Variants,
                randomSeed.Value).Variant;
        }
        else
        {
            variant = catalog.Variants.PracticeVariants.SingleOrDefault(
                candidate => string.Equals(candidate.VariantId, request.VariantId, StringComparison.Ordinal));
        }
        if (variant is null)
        {
            return NotFound("practice-variant-not-found");
        }

        var prepared = PrepareRun(
            catalog,
            build,
            variant,
            provider.ProfileId,
            RunKind.Practice,
            null,
            randomSeed);
        var created = await repository.CreateRunAsync(
            GuestSessionMiddleware.Owner(context),
            prepared,
            timeProvider.GetUtcNow(),
            cancellationToken);
        return Results.Created($"/api/v1/runs/{prepared.RunId.Value}", ToPublicRun(created, true));
    }

    private static async Task<IResult> StartCertificationAsync(
        HttpContext context,
        StartCertificationRequest request,
        ColdStartRuntimeCatalog catalog,
        IGameRepository gameRepository,
        IMasteryRepository masteryRepository,
        IAgentDecisionProvider provider,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var owner = GuestSessionMiddleware.Owner(context);
        var build = await gameRepository.GetBuildVersionAsync(owner, request.BuildId, request.BuildVersion, cancellationToken);
        if (build is null)
        {
            return NotFound("build-version-not-found");
        }
        if (!await masteryRepository.HasCertificationEligibilityAsync(
                owner, request.BuildId, request.BuildVersion, provider.ProfileId, cancellationToken))
        {
            return Conflict("certification-ineligible", "Three successful unassisted practice variants are required.");
        }

        var certificationId = CreateId("cert");
        var selected = catalog.Variants.CertificationVariants
            .Select(value => new { Value = value, Key = RandomNumberGenerator.GetBytes(16) })
            .OrderBy(value => Convert.ToHexString(value.Key), StringComparer.Ordinal)
            .Take(3)
            .Select(value => value.Value)
            .ToArray();
        var prepared = selected.Select(value => PrepareRun(
            catalog, build, value, provider.ProfileId, RunKind.Certification, certificationId, null)).ToArray();
        var mission = catalog.Mission.Authoring;
        var certificate = await masteryRepository.CreateCertificationAsync(
            owner,
            certificationId,
            build.BuildId,
            build.Version,
            provider.ProfileId,
            mission.ContentVersion,
            mission.RulesVersion,
            mission.ScoreVersion,
            catalog.Variants.CertificationVersion,
            prepared,
            timeProvider.GetUtcNow(),
            cancellationToken);
        return Results.Created($"/api/v1/certifications/{certificationId}", certificate);
    }

    private static PreparedRun PrepareRun(
        ColdStartRuntimeCatalog catalog,
        BuildVersionSnapshot build,
        VariantDocument variant,
        string providerProfileId,
        RunKind kind,
        string? certificationId,
        ulong? randomSeed)
    {
        var buildDocument = ContractJson.Deserialize<BuildDocument>(build.CanonicalJson);
        var modules = ColdStartMissionMaterializer.MapBuildModules(catalog.Mission, buildDocument);
        var materialized = ColdStartMissionMaterializer.Materialize(catalog.Mission, variant, modules);
        if (!materialized.IsValid)
        {
            throw new InvalidOperationException($"Variant materialization failed: {string.Join("; ", materialized.Errors)}");
        }
        var definition = materialized.Definition!;
        var recon = definition.Agents.Single(agent => agent.Archetype == AgentArchetype.Recon);
        var engineer = definition.Agents.Single(agent => agent.Archetype == AgentArchetype.Engineer);
        var solution = ReferenceSolver.Solve(
            definition,
            new ReferencePolicyOptions(
                definition.ConsoleAlpha.InitialCondition == ConsoleCondition.Damaged ? engineer.AgentId : recon.AgentId,
                recon.AgentId,
                MaximumTurns: 17,
                RequireNoDamage: false));
        if (!solution.Solved)
        {
            throw new InvalidOperationException(solution.Failure ?? "Scripted reference plan failed.");
        }
        var runId = new RunId(CreateId("run"));
        var start = RunStartFactory.Create(runId, definition, ReferenceSolver.ScriptedSeed, ReferenceSolver.ScriptedStream);
        var plan = ScriptedKnowledgePlan.Apply(buildDocument, definition, solution.Turns).ToImmutableDictionary(
            turn => turn.Turn,
            turn => turn.Decisions.ToImmutableDictionary(decision => decision.AgentId, decision => decision.ActionId));
        var disclosure = JsonSerializer.Serialize(
            new
            {
                variant.VariantId,
                variant.Label,
                Seed = randomSeed,
                variant.Mutations,
            },
            WebJson);
        return new PreparedRun(
            runId, build.BuildId, build.Version, start.State, start.Event, plan,
            providerProfileId, kind, certificationId, disclosure);
    }

    private static async Task<IResult> EnqueueTurnAsync(
        HttpContext context,
        string runId,
        IGameRepository repository,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = context.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 128)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                "idempotency-key-invalid",
                "A non-empty Idempotency-Key header of at most 128 characters is required.");
        }

        var queued = await repository.EnqueueTurnAsync(
            GuestSessionMiddleware.Owner(context),
            new RunId(runId),
            idempotencyKey,
            timeProvider.GetUtcNow(),
            cancellationToken);
        if (queued is null)
        {
            return NotFound("run-not-found");
        }

        if (queued.IsConflict)
        {
            return Conflict("turn-operation-conflict", "The run already has an active turn operation.");
        }

        return Results.Accepted(
            $"/api/v1/operations/{queued.Operation.OperationId}",
            new OperationAcceptedResponse(
                queued.Operation.OperationId,
                queued.Operation.Status.ToString().ToLowerInvariant()));
    }

    private static async Task<IResult> GetEventsAsync(
        HttpContext context,
        string runId,
        long? afterSequence,
        int? limit,
        IGameRepository repository,
        CancellationToken cancellationToken)
    {
        var boundedLimit = Math.Clamp(limit ?? 200, 1, 500);
        var owner = GuestSessionMiddleware.Owner(context);
        var run = await repository.GetRunAsync(owner, new RunId(runId), cancellationToken);
        if (run is null)
        {
            return NotFound("run-not-found");
        }
        var revealed = await IsRunRevealedAsync(
            context,
            run,
            context.RequestServices.GetRequiredService<IMasteryRepository>(),
            cancellationToken);
        var events = await repository.GetEventsAsync(
            owner,
            new RunId(runId),
            afterSequence ?? -1,
            boundedLimit,
            cancellationToken);
        if (events is null)
        {
            return NotFound("run-not-found");
        }
        var visibleEvents = revealed
            ? events.Value.AsEnumerable()
            : events.Value.Where(value => value.Type != CanonicalEventType.RunStarted);
        return Results.Ok(visibleEvents);
    }

    private static async Task<IResult> GetReplayAsync(
        HttpContext context,
        string runId,
        IGameRepository repository,
        IMasteryRepository masteryRepository,
        CancellationToken cancellationToken)
    {
        var owner = GuestSessionMiddleware.Owner(context);
        var run = await repository.GetRunAsync(owner, new RunId(runId), cancellationToken);
        if (run is null)
        {
            return NotFound("run-not-found");
        }
        if (!await IsRunRevealedAsync(context, run, masteryRepository, cancellationToken))
        {
            return Conflict("certification-hidden", "Certification replay unlocks after all three runs finish.");
        }
        var replay = await repository.GetReplayAsync(owner, new RunId(runId), cancellationToken);
        return replay is null ? NotFound("run-not-found") : Results.Ok(replay);
    }

    private static async Task<IResult> ApplyEmergencyBurstAsync(
        HttpContext context,
        string runId,
        EmergencyBurstRequest request,
        IMasteryRepository repository,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        try
        {
            var run = await repository.ApplyEmergencyBurstAsync(
                GuestSessionMiddleware.Owner(context),
                new RunId(runId),
                request.Text,
                timeProvider.GetUtcNow(),
                cancellationToken);
            return run is null ? NotFound("run-not-found") : Results.Ok(ToPublicRun(run, true));
        }
        catch (ArgumentException exception)
        {
            return Problem(StatusCodes.Status400BadRequest, "emergency-burst-invalid", exception.Message);
        }
        catch (ResourceConflictException exception)
        {
            return Conflict("emergency-burst-ineligible", exception.Message);
        }
    }

    private static async Task<IResult> GetComparisonAsync(
        HttpContext context,
        string leftRunId,
        string rightRunId,
        IMasteryRepository repository,
        CancellationToken cancellationToken)
    {
        var comparison = await repository.GetComparisonAsync(
            GuestSessionMiddleware.Owner(context),
            new RunId(leftRunId),
            new RunId(rightRunId),
            cancellationToken);
        return comparison is null
            ? NotFound("comparison-not-available")
            : Results.Ok(new
            {
                Left = ToPublicRun(comparison.Left, true),
                Right = ToPublicRun(comparison.Right, true),
                comparison.Build,
                comparison.FirstDifferingDecision,
                comparison.LeftScore,
                comparison.RightScore,
            });
    }

    private static async Task<IResult> GetShareAsync(
        HttpContext context,
        string runId,
        IGameRepository gameRepository,
        IMasteryRepository masteryRepository,
        CancellationToken cancellationToken)
    {
        var share = await CreateShareAsync(
            context, runId, gameRepository, masteryRepository, cancellationToken);
        return share.Result ?? Results.Ok(share.Value);
    }

    private static async Task<IResult> GetShareCardAsync(
        HttpContext context,
        string runId,
        IGameRepository gameRepository,
        IMasteryRepository masteryRepository,
        CancellationToken cancellationToken)
    {
        var share = await CreateShareAsync(
            context, runId, gameRepository, masteryRepository, cancellationToken);
        if (share.Result is not null)
        {
            return share.Result;
        }
        var value = share.Value!;
        var result = System.Net.WebUtility.HtmlEncode(value.Result);
        var codename = System.Net.WebUtility.HtmlEncode(value.BuildCodename);
        var decisive = System.Net.WebUtility.HtmlEncode(value.DecisiveEvent);
        var svg = $"""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1200 630" role="img" aria-label="Directive Drift share card">
              <rect width="1200" height="630" fill="#071218"/>
              <path d="M80 420H330L440 300H720L840 205H1120" fill="none" stroke="#55d6be" stroke-width="12"/>
              <text x="80" y="105" fill="#8ca5b3" font-family="sans-serif" font-size="30">DIRECTIVE DRIFT</text>
              <text x="80" y="185" fill="#f1f7f8" font-family="sans-serif" font-size="58" font-weight="700">{codename}</text>
              <text x="80" y="270" fill="#55d6be" font-family="sans-serif" font-size="44">{result}</text>
              <text x="80" y="550" fill="#c5d2d8" font-family="sans-serif" font-size="28">{decisive}</text>
            </svg>
            """;
        return Results.Text(svg, "image/svg+xml");
    }

    private static async Task<(ShareResponse? Value, IResult? Result)> CreateShareAsync(
        HttpContext context,
        string runId,
        IGameRepository gameRepository,
        IMasteryRepository masteryRepository,
        CancellationToken cancellationToken)
    {
        var owner = GuestSessionMiddleware.Owner(context);
        var run = await gameRepository.GetRunAsync(owner, new RunId(runId), cancellationToken);
        if (run is null)
        {
            return (null, NotFound("run-not-found"));
        }
        if (!await IsRunRevealedAsync(context, run, masteryRepository, cancellationToken))
        {
            return (null, Conflict("certification-hidden", "Share output unlocks after certification reveal."));
        }
        if (run.Status == RunStatus.Active)
        {
            return (null, Conflict("run-active", "Share output is available after the run completes."));
        }
        var replay = await gameRepository.GetReplayAsync(owner, run.RunId, cancellationToken);
        var build = await gameRepository.GetBuildVersionAsync(owner, run.BuildId, run.BuildVersion, cancellationToken);
        if (replay is null || build is null)
        {
            return (null, NotFound("run-not-found"));
        }
        var terminal = replay.Events.LastOrDefault(value => value.Type is CanonicalEventType.MissionSucceeded or CanonicalEventType.MissionFailed);
        var score = terminal?.Payload is MissionTerminalPayload payload ? payload.Score : null;
        var decisive = replay.Events.FirstOrDefault(value => value.Type is
            CanonicalEventType.ConsoleSyncFailed or CanonicalEventType.AgentDamaged or CanonicalEventType.MissionSucceeded or CanonicalEventType.MissionFailed)?.Type.ToString()
            ?? "Run completed";
        using var buildJson = JsonDocument.Parse(build.CanonicalJson);
        var buildCodename = buildJson.RootElement.GetProperty("name").GetString() ?? run.BuildId;
        var icons = buildJson.RootElement.GetProperty("agents").EnumerateObject()
            .OrderBy(value => value.Name, StringComparer.Ordinal)
            .Select(value => value.Value.GetProperty("moduleId").GetString() ?? "module")
            .Append("briefing-allocation")
            .ToArray();
        var badges = Badges(replay);
        return (new ShareResponse(
            buildCodename,
            run.Status == RunStatus.Succeeded ? "Mission succeeded" : "Mission failed",
            score,
            icons,
            decisive,
            badges,
            $"/api/v1/runs/{runId}/replay",
            $"/api/v1/runs/{runId}/share-card.svg"), null);
    }

    private static string[] Badges(ReplayData replay)
    {
        if (replay.Run.Status != RunStatus.Succeeded)
        {
            return [];
        }
        var badges = new List<string>();
        if (replay.Events.All(value => value.Type != CanonicalEventType.AgentDamaged)) badges.Add("no-damage");
        if (replay.Events.All(value => value.Type != CanonicalEventType.ConsoleSyncFailed)) badges.Add("no-wasted-sync");
        if (replay.Events.All(value => value.Type is not (CanonicalEventType.MessageQueued or CanonicalEventType.MessageDelivered))) badges.Add("silent-success");
        return badges.ToArray();
    }

    private static async Task<bool> IsRunRevealedAsync(
        HttpContext context,
        RunSummary run,
        IMasteryRepository repository,
        CancellationToken cancellationToken)
    {
        if (run.Kind == RunKind.Practice || run.CertificationId is null)
        {
            return true;
        }
        var certification = await repository.GetCertificationAsync(
            GuestSessionMiddleware.Owner(context), run.CertificationId, cancellationToken);
        return certification?.Revealed == true;
    }

    private static object ToPublicRun(RunSummary run, bool revealed) => new
    {
        run.RunId,
        run.BuildId,
        run.BuildVersion,
        run.MissionId,
        Variant = revealed && run.VariantDisclosureJson is not null
            ? JsonSerializer.Deserialize<JsonElement>(run.VariantDisclosureJson)
            : (JsonElement?)null,
        run.Turn,
        run.Status,
        run.StateHash,
        run.CreatedAt,
        run.UpdatedAt,
        run.ProviderProfileId,
        run.Kind,
        run.Assisted,
        run.CertificationId,
    };

    private sealed record ShareResponse(
        string BuildCodename,
        string Result,
        int? Score,
        IReadOnlyList<string> LoadoutIcons,
        string DecisiveEvent,
        IReadOnlyList<string> Badges,
        string ReplayUrl,
        string ImageUrl);

    private static (BuildDocument? Document, IReadOnlyList<ValidationError> Errors) ValidateBuild(
        string json,
        ColdStartRuntimeCatalog catalog)
    {
        var loaded = ContractDocumentLoader.LoadBuild(json, catalog.BuildSchemaJson);
        if (!loaded.IsValid)
        {
            return (null, loaded.Errors);
        }

        var references = BuildReferenceValidator.Validate(loaded.Document!, catalog.Mission);
        return references.IsValid
            ? (loaded.Document, [])
            : (null, references.Errors);
    }

    private static IResult ValidationProblem(IReadOnlyList<ValidationError> errors) =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "The request failed validation.",
            type: "https://directive-drift.invalid/problems/validation",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = "validation-failed",
                ["errors"] = errors.Select(error => new { error.Code, error.Path, error.Message }),
            });

    private static IResult NotFound(string code) =>
        Problem(StatusCodes.Status404NotFound, code, "The requested resource was not found.");

    private static IResult Conflict(string code, string detail) =>
        Problem(StatusCodes.Status409Conflict, code, detail);

    private static IResult Problem(int status, string code, string detail) =>
        Results.Problem(
            statusCode: status,
            title: detail,
            detail: detail,
            type: $"https://directive-drift.invalid/problems/{code}",
            extensions: new Dictionary<string, object?> { ["code"] = code });

    private static string CreateId(string prefix) =>
        $"{prefix}_{Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant()}";
}
