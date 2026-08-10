using System.Diagnostics;
using System.Text;
using DirectiveDrift.Application.Models;
using DirectiveDrift.Application.Ports;
using DirectiveDrift.Core.Decisions;
using DirectiveDrift.Core.Model;

namespace DirectiveDrift.AI;

public sealed record ProviderTransportRequest(
    ProviderProfile Profile,
    PromptEnvelope Prompt,
    string? RepairDiagnostic);

public sealed record ProviderTransportResponse(
    string Content,
    int? InputTokens,
    int? OutputTokens,
    string? RequestId);

public interface IProviderTransport
{
    Task<ProviderTransportResponse> SendAsync(
        ProviderTransportRequest request,
        CancellationToken cancellationToken);
}

public sealed class ProviderTransportException(string diagnosticCode, Exception? inner = null)
    : Exception(diagnosticCode, inner)
{
    public string DiagnosticCode { get; } = diagnosticCode;
}

public sealed class StructuredDecisionProvider(
    ProviderProfile profile,
    IProviderTransport transport) : IAgentDecisionProvider
{
    public string ProfileId => profile.ProfileId;

    public ProviderProfile Profile => profile;

    public async Task<ProviderDecisionResult> DecideAsync(
        AgentDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var prompt = PromptAssembler.Assemble(request.Context, profile);
        var assembledInput = string.Concat(
            prompt.SystemText,
            "\n",
            prompt.ContextJson,
            "\n",
            prompt.OutputInstruction);
        var approximateInputTokens = Encoding.UTF8.GetByteCount(assembledInput) / 4 + 1;
        if (approximateInputTokens > profile.MaximumInputTokens)
        {
            return Fallback(
                request,
                prompt,
                ProviderAttemptStatus.ResponseTooLarge,
                "context-token-cap",
                0,
                0,
                false,
                0);
        }

        var totalInput = 0;
        var totalOutput = 0;
        var totalLatency = 0;
        string? requestId = null;
        DecisionValidationResult? validation = null;
        var diagnostics = new List<ProviderAttemptDiagnostic>();
        for (var attempt = 0; attempt <= profile.MaximumRepairRetries; attempt++)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(profile.AttemptTimeout);
                var response = await transport.SendAsync(
                    new ProviderTransportRequest(
                        profile,
                        prompt,
                        attempt == 0 ? null : validation?.DiagnosticCode),
                    timeout.Token);
                stopwatch.Stop();
                totalLatency += checked((int)stopwatch.ElapsedMilliseconds);
                requestId = response.RequestId ?? requestId;
                var bytes = Encoding.UTF8.GetByteCount(response.Content);
                totalInput += response.InputTokens ?? approximateInputTokens;
                totalOutput += response.OutputTokens ?? Math.Max(1, bytes / 4);
                if (bytes > profile.MaximumResponseBytes
                    || totalOutput > profile.MaximumOutputTokens * (attempt + 1))
                {
                    validation = new DecisionValidationResult(
                        null,
                        ProviderAttemptStatus.ResponseTooLarge,
                        "response-cap",
                        false);
                }
                else
                {
                    validation = DecisionValidation.Validate(
                        response.Content,
                        request.Context,
                        request.OtherAgentId);
                }

                diagnostics.Add(
                    new ProviderAttemptDiagnostic(
                        attempt + 1,
                        validation.Status,
                        validation.DiagnosticCode,
                        response.InputTokens ?? approximateInputTokens,
                        response.OutputTokens ?? Math.Max(1, bytes / 4),
                        checked((int)stopwatch.ElapsedMilliseconds),
                        response.RequestId));

                if (validation.Decision is not null)
                {
                    return Result(
                        validation.Decision,
                        validation.Status,
                        validation.DiagnosticCode,
                        prompt,
                        totalInput,
                        totalOutput,
                        totalLatency,
                        requestId,
                        attempt > 0,
                        attempt + 1) with
                    { AttemptDiagnostics = diagnostics.ToArray() };
                }

                if (!validation.Repairable || attempt == profile.MaximumRepairRetries)
                {
                    return Fallback(
                        request,
                        prompt,
                        validation.Status,
                        validation.DiagnosticCode,
                        totalInput,
                        totalOutput,
                        attempt > 0,
                        attempt + 1,
                        totalLatency,
                        requestId) with
                    { AttemptDiagnostics = diagnostics.ToArray() };
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                stopwatch.Stop();
                totalInput += profile.MaximumInputTokens;
                totalOutput += profile.MaximumOutputTokens;
                diagnostics.Add(
                    new ProviderAttemptDiagnostic(
                        attempt + 1,
                        ProviderAttemptStatus.Timeout,
                        "provider-timeout",
                        profile.MaximumInputTokens,
                        profile.MaximumOutputTokens,
                        checked((int)stopwatch.ElapsedMilliseconds),
                        requestId));
                return Fallback(
                    request,
                    prompt,
                    ProviderAttemptStatus.Timeout,
                    "provider-timeout",
                    totalInput,
                    totalOutput,
                    attempt > 0,
                    attempt + 1,
                    totalLatency + checked((int)stopwatch.ElapsedMilliseconds),
                    requestId) with
                { AttemptDiagnostics = diagnostics.ToArray() };
            }
            catch (ProviderTransportException exception)
            {
                stopwatch.Stop();
                totalInput += profile.MaximumInputTokens;
                totalOutput += profile.MaximumOutputTokens;
                diagnostics.Add(
                    new ProviderAttemptDiagnostic(
                        attempt + 1,
                        ProviderAttemptStatus.TransportError,
                        exception.DiagnosticCode,
                        profile.MaximumInputTokens,
                        profile.MaximumOutputTokens,
                        checked((int)stopwatch.ElapsedMilliseconds),
                        requestId));
                return Fallback(
                    request,
                    prompt,
                    ProviderAttemptStatus.TransportError,
                    exception.DiagnosticCode,
                    totalInput,
                    totalOutput,
                    attempt > 0,
                    attempt + 1,
                    totalLatency + checked((int)stopwatch.ElapsedMilliseconds),
                    requestId) with
                { AttemptDiagnostics = diagnostics.ToArray() };
            }
        }

        throw new InvalidOperationException("Provider retry loop exited unexpectedly.");
    }

    private ProviderDecisionResult Fallback(
        AgentDecisionRequest request,
        PromptEnvelope prompt,
        ProviderAttemptStatus status,
        string diagnostic,
        int inputTokens,
        int outputTokens,
        bool repaired,
        int attempts,
        int latency = 0,
        string? requestId = null) => Result(
            new ProposedDecision(
                new ActionId("wait"),
                null,
                string.Empty,
                request.CurrentMemory,
                DecisionFallbackReason.Missing),
            status,
            diagnostic,
            prompt,
            inputTokens,
            outputTokens,
            latency,
            requestId,
            repaired,
            attempts);

    private ProviderDecisionResult Result(
        ProposedDecision decision,
        ProviderAttemptStatus status,
        string diagnostic,
        PromptEnvelope prompt,
        int inputTokens,
        int outputTokens,
        int latency,
        string? requestId,
        bool repaired,
        int attempts)
    {
        var cost = CostMicros(inputTokens, profile.InputPriceMicrosPerMillionTokens)
            + CostMicros(outputTokens, profile.OutputPriceMicrosPerMillionTokens);
        return new ProviderDecisionResult(
            decision,
            status,
            new ProviderUsage(inputTokens, outputTokens, cost, false),
            latency,
            requestId,
            profile.PriceTableVersion,
            prompt.TemplateHash,
            prompt.ContextHash,
            diagnostic,
            repaired,
            attempts,
            prompt.ContextJson);
    }

    private static int CostMicros(int tokens, int priceMicrosPerMillionTokens) =>
        checked((int)(((long)tokens * priceMicrosPerMillionTokens + 999_999) / 1_000_000));
}
