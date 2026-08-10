using System.Text.Json;
using DirectiveDrift.Core.Decisions;

namespace DirectiveDrift.AI;

public enum FakeProviderBehavior
{
    Valid,
    MalformedJson,
    MissingField,
    ExtraProperty,
    IllegalAction,
    WrongRecipient,
    OversizedMessage,
    OversizedMemory,
    OversizedResponse,
    Timeout,
    TransportError,
}

public sealed class FakeProviderTransport(
    IReadOnlyList<FakeProviderBehavior>? behaviors = null,
    TimeSpan? latency = null) : IProviderTransport
{
    private readonly IReadOnlyList<FakeProviderBehavior> configuredBehaviors =
        behaviors ?? [FakeProviderBehavior.Valid];
    private int calls;

    public int CallCount => Volatile.Read(ref calls);

    public async Task<ProviderTransportResponse> SendAsync(
        ProviderTransportRequest request,
        CancellationToken cancellationToken)
    {
        var call = Interlocked.Increment(ref calls) - 1;
        var behavior = configuredBehaviors[Math.Min(call, configuredBehaviors.Count - 1)];
        if (latency is not null)
        {
            await Task.Delay(latency.Value, cancellationToken);
        }

        if (behavior == FakeProviderBehavior.Timeout)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        if (behavior == FakeProviderBehavior.TransportError)
        {
            throw new ProviderTransportException("fake-transport-error");
        }

        using var context = JsonDocument.Parse(request.Prompt.ContextJson);
        var root = context.RootElement;
        var legal = GetValue(root.GetProperty("legalActions")[0].GetProperty("actionId"));
        var self = GetValue(root.GetProperty("self").GetProperty("agentId"));
        var messageLimit = root.GetProperty("limits").GetProperty("messageCharacters").GetInt32();
        var memoryLimit = root.GetProperty("limits").GetProperty("memoryCharacters").GetInt32();
        const string recipient = "other-agent";
        object decision = new
        {
            schemaVersion = "1",
            actionId = behavior == FakeProviderBehavior.IllegalAction ? "move:forbidden" : legal,
            message = behavior == FakeProviderBehavior.WrongRecipient
                ? new { toAgentId = self, text = "bad" }
                : null,
            rationale = "Selected from the supplied legal actions.",
            memory = behavior == FakeProviderBehavior.OversizedMemory
                ? new string('m', memoryLimit + 1)
                : string.Empty,
        };

        var content = behavior switch
        {
            FakeProviderBehavior.MalformedJson => "{",
            FakeProviderBehavior.MissingField => "{\"schemaVersion\":\"1\"}",
            FakeProviderBehavior.ExtraProperty => JsonSerializer.Serialize(
                new
                {
                    schemaVersion = "1",
                    actionId = legal,
                    message = (object?)null,
                    rationale = "valid",
                    memory = string.Empty,
                    extra = true,
                }),
            FakeProviderBehavior.OversizedMessage => JsonSerializer.Serialize(
                new
                {
                    schemaVersion = "1",
                    actionId = legal,
                    message = new { toAgentId = recipient, text = new string('x', messageLimit + 1) },
                    rationale = "valid",
                    memory = string.Empty,
                }),
            FakeProviderBehavior.OversizedResponse => new string('x', request.Profile.MaximumResponseBytes + 1),
            _ => JsonSerializer.Serialize(decision),
        };
        return new ProviderTransportResponse(content, 100, 20, $"fake-{call + 1}");
    }

    private static string GetValue(JsonElement element) =>
        element.ValueKind == JsonValueKind.String
            ? element.GetString()!
            : element.GetProperty("value").GetString()!;
}
