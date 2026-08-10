using System.Collections.Immutable;
using System.Net;
using System.Text;
using DirectiveDrift.AI;
using DirectiveDrift.Application.Models;
using DirectiveDrift.Application.Ports;
using DirectiveDrift.Core.Decisions;
using DirectiveDrift.Core.Model;
using DirectiveDrift.Core.Observations;

namespace DirectiveDrift.AI.Tests;

public sealed class P8RuntimeTests
{
    [Fact]
    public void PromptAndOpenAiRequestAreStableProviderNeutralAndSecretFree()
    {
        var context = CreateContext();
        var first = PromptAssembler.Assemble(context, ProviderProfiles.OpenAi);
        var second = PromptAssembler.Assemble(context, ProviderProfiles.OpenAi);
        var request = OpenAiResponsesTransport.CreateRequestJson(
            new ProviderTransportRequest(ProviderProfiles.OpenAi, first, null));

        Assert.Equal(first.TemplateHash, second.TemplateHash);
        Assert.Equal(first.ContextHash, second.ContextHash);
        Assert.Contains("allowed-doctrine-marker", request, StringComparison.Ordinal);
        Assert.Contains("UNTRUSTED_MISSION_DATA_BEGIN", request, StringComparison.Ordinal);
        Assert.Contains("\"type\":\"json_schema\"", request, StringComparison.Ordinal);
        Assert.Contains("\"reasoning\":{\"effort\":\"none\"}", request, StringComparison.Ordinal);
        Assert.DoesNotContain("api-key-marker", request, StringComparison.Ordinal);
        Assert.DoesNotContain("reference-solution-marker", request, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(FakeProviderBehavior.IllegalAction, ProviderAttemptStatus.InvalidDecision)]
    [InlineData(FakeProviderBehavior.WrongRecipient, ProviderAttemptStatus.InvalidDecision)]
    [InlineData(FakeProviderBehavior.OversizedMessage, ProviderAttemptStatus.InvalidDecision)]
    [InlineData(FakeProviderBehavior.OversizedMemory, ProviderAttemptStatus.InvalidDecision)]
    [InlineData(FakeProviderBehavior.OversizedResponse, ProviderAttemptStatus.ResponseTooLarge)]
    [InlineData(FakeProviderBehavior.TransportError, ProviderAttemptStatus.TransportError)]
    public async Task FakeProviderCoversNonRepairableFailureCategories(
        FakeProviderBehavior behavior,
        ProviderAttemptStatus expected)
    {
        var transport = new FakeProviderTransport([behavior]);
        var provider = new StructuredDecisionProvider(ProviderProfiles.Fake, transport);

        var result = await provider.DecideAsync(CreateRequest(), default);

        Assert.Equal(expected, result.Status);
        Assert.Equal(new ActionId("wait"), result.Decision.ActionId);
        Assert.Equal(DecisionFallbackReason.Missing, result.Decision.ForcedFallbackReason);
        Assert.Equal(1, transport.CallCount);
    }

    [Theory]
    [InlineData(FakeProviderBehavior.MalformedJson)]
    [InlineData(FakeProviderBehavior.MissingField)]
    [InlineData(FakeProviderBehavior.ExtraProperty)]
    public async Task RepairableShapeFailuresRetryOnceWithTheIdenticalContext(
        FakeProviderBehavior behavior)
    {
        var transport = new RecordingTransport([behavior, FakeProviderBehavior.Valid]);
        var provider = new StructuredDecisionProvider(ProviderProfiles.Fake, transport);

        var result = await provider.DecideAsync(CreateRequest(), default);

        Assert.Equal(ProviderAttemptStatus.Accepted, result.Status);
        Assert.True(result.RepairAttempted);
        Assert.Equal(2, result.AttemptCount);
        Assert.Equal(2, transport.Prompts.Count);
        Assert.Equal(transport.Prompts[0].ContextJson, transport.Prompts[1].ContextJson);
        Assert.Equal(transport.Prompts[0].ContextHash, transport.Prompts[1].ContextHash);
    }

    [Fact]
    public async Task TimeoutProducesDeterministicWaitWithoutARepairCall()
    {
        var transport = new FakeProviderTransport([FakeProviderBehavior.Timeout]);
        var profile = ProviderProfiles.Fake with { AttemptTimeout = TimeSpan.FromMilliseconds(20) };
        var provider = new StructuredDecisionProvider(profile, transport);

        var result = await provider.DecideAsync(CreateRequest(profile), default);

        Assert.Equal(ProviderAttemptStatus.Timeout, result.Status);
        Assert.Equal(new ActionId("wait"), result.Decision.ActionId);
        Assert.Equal("previous-memory", result.Decision.Memory);
        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public void SemanticValidationRejectsWellFormedIllegalActionWithoutRepair()
    {
        var result = DecisionValidation.Validate(
            "{\"schemaVersion\":\"1\",\"actionId\":\"move:secret\",\"message\":null,\"rationale\":\"valid\",\"memory\":\"\"}",
            CreateContext(),
            new AgentId("agent-b"));

        Assert.Equal("action-not-legal", result.DiagnosticCode);
        Assert.False(result.Repairable);
    }

    [Fact]
    public async Task OpenAiResponsesAdapterSmokeParsesStructuredOutputAndUsageUnderCap()
    {
        const string decision = "{\"schemaVersion\":\"1\",\"actionId\":\"wait\",\"message\":null,\"rationale\":\"valid\",\"memory\":\"\"}";
        var responseJson = System.Text.Json.JsonSerializer.Serialize(
            new
            {
                id = "response-safe-id",
                output = new[]
                {
                    new
                    {
                        type = "message",
                        content = new[] { new { type = "output_text", text = decision } },
                    },
                },
                usage = new { input_tokens = 100, output_tokens = 20 },
            });
        var handler = new StubHandler(responseJson);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.test/") };
        var transport = new OpenAiResponsesTransport(client, "server-secret-marker");
        var prompt = PromptAssembler.Assemble(CreateContext(), ProviderProfiles.OpenAi);

        var result = await transport.SendAsync(
            new ProviderTransportRequest(ProviderProfiles.OpenAi, prompt, null),
            default);

        Assert.Equal(decision, result.Content);
        Assert.Equal(100, result.InputTokens);
        Assert.Equal(20, result.OutputTokens);
        Assert.Equal("response-safe-id", result.RequestId);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("server-secret-marker", handler.AuthorizationParameter);
        Assert.DoesNotContain("server-secret-marker", handler.Body, StringComparison.Ordinal);
        Assert.Contains("\"store\":false", handler.Body, StringComparison.Ordinal);
    }

    private static AgentDecisionRequest CreateRequest(ProviderProfile? profile = null)
    {
        var context = CreateContext(profile);
        return new AgentDecisionRequest(
            "operation-test",
            1,
            context.Self.AgentId,
            context.Observation.LegalActions,
            new ActionId("wait"),
            "previous-memory",
            new AgentId("agent-b"),
            context);
    }

    private static AgentTurnContext CreateContext(ProviderProfile? profile = null)
    {
        profile ??= ProviderProfiles.Fake;
        var agentId = new AgentId("agent-a");
        var legalActions = new LegalActionSet(
            agentId,
            [
                new LegalAction(new ActionId("wait"), LegalActionKind.Wait, RuleTarget.None),
                new LegalAction(
                    new ActionId("move:room-b"),
                    LegalActionKind.Move,
                    new RuleTarget(RuleTargetKind.Room, "room-b")),
            ]);
        var observation = new PrivateObservation(
            agentId,
            1,
            "pre-state-hash",
            new SelfObservation(
                2,
                AgentStatus.Active,
                new RoomId("room-a"),
                null,
                new ModuleState(SupportModule.None, 0),
                "previous-memory",
                false),
            [new ObservedExit(new ConnectionId("link-a"), new RoomId("room-b"), HazardObservation.Unknown)],
            [],
            [],
            [],
            [],
            legalActions);
        return new AgentTurnContext(
            "agent-turn-context-v1",
            new RunId("run-test"),
            1,
            new AgentIdentityView(agentId, "Agent A"),
            "rules",
            "allowed-doctrine-marker",
            "role",
            [new BriefingCardView("card-a", "Assigned", "allowed-card-marker")],
            new AgentCapabilityView(["move"]),
            null,
            observation,
            [],
            "previous-memory",
            legalActions.Actions.Select(
                value => new LegalActionView(value.ActionId, value.Kind.ToString(), value.Target.Value)).ToArray(),
            new RuntimeLimits(120, 240, 180, profile.MaximumOutputTokens, profile.MaximumResponseBytes));
    }

    private sealed class RecordingTransport(IReadOnlyList<FakeProviderBehavior> behaviors)
        : IProviderTransport
    {
        private readonly FakeProviderTransport inner = new(behaviors);

        public List<PromptEnvelope> Prompts { get; } = [];

        public Task<ProviderTransportResponse> SendAsync(
            ProviderTransportRequest request,
            CancellationToken cancellationToken)
        {
            Prompts.Add(request.Prompt);
            return inner.SendAsync(request, cancellationToken);
        }
    }

    private sealed class StubHandler(string responseJson) : HttpMessageHandler
    {
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }
        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            };
        }
    }
}
