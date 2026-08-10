using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace DirectiveDrift.AI;

public sealed class OpenAiResponsesTransport(HttpClient httpClient, string apiKey) : IProviderTransport
{
    private const string ResponsesPath = "v1/responses";

    public async Task<ProviderTransportResponse> SendAsync(
        ProviderTransportRequest request,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, ResponsesPath);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        message.Content = new StringContent(
            CreateRequestJson(request),
            Encoding.UTF8,
            "application/json");
        using var response = await httpClient.SendAsync(
            message,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        var body = await ReadBoundedAsync(
            response.Content,
            checked(request.Profile.MaximumResponseBytes + 16_384),
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new ProviderTransportException($"openai-http-{(int)response.StatusCode}");
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            var content = root.GetProperty("output")
                .EnumerateArray()
                .Where(item => item.TryGetProperty("type", out var type)
                    && type.GetString() == "message")
                .SelectMany(item => item.GetProperty("content").EnumerateArray())
                .First(item => item.GetProperty("type").GetString() == "output_text")
                .GetProperty("text")
                .GetString()!;
            var usage = root.TryGetProperty("usage", out var usageElement) ? usageElement : default;
            return new ProviderTransportResponse(
                content,
                usage.ValueKind == JsonValueKind.Object
                    && usage.TryGetProperty("input_tokens", out var input)
                        ? input.GetInt32()
                        : null,
                usage.ValueKind == JsonValueKind.Object
                    && usage.TryGetProperty("output_tokens", out var output)
                        ? output.GetInt32()
                        : null,
                root.TryGetProperty("id", out var id) ? id.GetString() : null);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            throw new ProviderTransportException("openai-response-invalid", exception);
        }
    }

    public static string CreateRequestJson(ProviderTransportRequest request)
    {
        using var schema = JsonDocument.Parse(DecisionSchema);
        var repair = request.RepairDiagnostic is null
            ? request.Prompt.OutputInstruction
            : $"{request.Prompt.OutputInstruction} Repair validation error: {request.RepairDiagnostic}. Use the same supplied context and legal actions.";
        return JsonSerializer.Serialize(
            new
            {
                model = request.Profile.Model,
                input = new object[]
                {
                    new { role = "system", content = request.Prompt.SystemText },
                    new { role = "user", content = $"UNTRUSTED_MISSION_DATA_BEGIN\n{request.Prompt.ContextJson}\nUNTRUSTED_MISSION_DATA_END\n{repair}" },
                },
                max_output_tokens = request.Profile.MaximumOutputTokens,
                text = new
                {
                    format = new
                    {
                        type = "json_schema",
                        name = "agent_decision_v1",
                        strict = true,
                        schema = schema.RootElement,
                    },
                },
                store = false,
            });
    }

    private static async Task<string> ReadBoundedAsync(
        HttpContent content,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream(Math.Min(maximumBytes, 16_384));
        var chunk = new byte[4_096];
        while (true)
        {
            var read = await stream.ReadAsync(chunk, cancellationToken);
            if (read == 0)
            {
                return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, checked((int)buffer.Length));
            }

            if (buffer.Length + read > maximumBytes)
            {
                throw new ProviderTransportException("openai-wire-response-cap");
            }

            buffer.Write(chunk, 0, read);
        }
    }

    private const string DecisionSchema = """
        {
          "type":"object",
          "additionalProperties":false,
          "required":["schemaVersion","actionId","message","rationale","memory"],
          "properties":{
            "schemaVersion":{"type":"string","const":"1"},
            "actionId":{"type":"string","minLength":1,"maxLength":128},
            "message":{"anyOf":[{"type":"null"},{"type":"object","additionalProperties":false,"required":["toAgentId","text"],"properties":{"toAgentId":{"type":"string","minLength":1,"maxLength":64},"text":{"type":"string","minLength":1,"maxLength":120}}}]},
            "rationale":{"type":"string","minLength":1,"maxLength":180},
            "memory":{"type":"string","maxLength":400}
          }
        }
        """;
}
