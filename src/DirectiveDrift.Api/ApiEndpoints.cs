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
using DirectiveDrift.Core.Model;
using DirectiveDrift.Core.Serialization;
using DirectiveDrift.Core.Simulation;

namespace DirectiveDrift.Api;

public static class ApiEndpoints
{
    public static IEndpointRouteBuilder MapDirectiveDriftApi(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api/v1");

        api.MapGet(
            "/runtime",
            () => new RuntimeResponse("v1", "scripted", CanonicalStateSerializer.Version));
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
                            variant => new { variant.VariantId, variant.Label }))
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
                return run is null ? NotFound("run-not-found") : Results.Ok(run);
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
        api.MapGet(
            "/runs/{runId}/replay",
            async (
                HttpContext context,
                string runId,
                IGameRepository repository,
                CancellationToken token) =>
            {
                var replay = await repository.GetReplayAsync(
                    GuestSessionMiddleware.Owner(context),
                    new RunId(runId),
                    token);
                return replay is null ? NotFound("run-not-found") : Results.Ok(replay);
            });

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

        var variant = catalog.Variants.PracticeVariants.SingleOrDefault(
            candidate => string.Equals(
                candidate.VariantId,
                request.VariantId,
                StringComparison.Ordinal));
        if (variant is null)
        {
            return NotFound("practice-variant-not-found");
        }

        var buildDocument = ContractJson.Deserialize<BuildDocument>(build.CanonicalJson);
        var modules = ColdStartMissionMaterializer.MapBuildModules(catalog.Mission, buildDocument);
        var materialized = ColdStartMissionMaterializer.Materialize(catalog.Mission, variant, modules);
        if (!materialized.IsValid)
        {
            return ValidationProblem(materialized.Errors);
        }

        var definition = materialized.Definition!;
        var recon = definition.Agents.Single(agent => agent.Archetype == AgentArchetype.Recon);
        var engineer = definition.Agents.Single(agent => agent.Archetype == AgentArchetype.Engineer);
        var alphaAgentId = definition.ConsoleAlpha.InitialCondition == ConsoleCondition.Damaged
            ? engineer.AgentId
            : recon.AgentId;
        var solution = ReferenceSolver.Solve(
            definition,
            new ReferencePolicyOptions(
                alphaAgentId,
                recon.AgentId,
                MaximumTurns: 17,
                RequireNoDamage: false));
        if (!solution.Solved)
        {
            return Problem(
                StatusCodes.Status500InternalServerError,
                "content-invariant",
                solution.Failure ?? "The scripted reference plan could not be created.");
        }

        var runId = new RunId(CreateId("run"));
        var start = RunStartFactory.Create(
            runId,
            definition,
            ReferenceSolver.ScriptedSeed,
            ReferenceSolver.ScriptedStream);
        var plan = solution.Turns.ToImmutableDictionary(
            turn => turn.Turn,
            turn => turn.Decisions.ToImmutableDictionary(
                decision => decision.AgentId,
                decision => decision.ActionId));
        var prepared = new PreparedRun(
            runId,
            build.BuildId,
            build.Version,
            start.State,
            start.Event,
            plan,
            provider.ProfileId);
        var created = await repository.CreateRunAsync(
            GuestSessionMiddleware.Owner(context),
            prepared,
            timeProvider.GetUtcNow(),
            cancellationToken);
        return Results.Created($"/api/v1/runs/{runId.Value}", created);
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
        var events = await repository.GetEventsAsync(
            GuestSessionMiddleware.Owner(context),
            new RunId(runId),
            afterSequence ?? -1,
            boundedLimit,
            cancellationToken);
        return events is null ? NotFound("run-not-found") : Results.Ok(events);
    }

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
