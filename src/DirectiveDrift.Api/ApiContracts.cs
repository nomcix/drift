using System.Text.Json;

namespace DirectiveDrift.Api;

public sealed record StartRunRequest(string BuildId, int BuildVersion, string VariantId);

public sealed record BuildVersionRequest(JsonElement Build);

public sealed record RuntimeResponse(string ApiVersion, string ProviderMode, string StateSchemaVersion);

public sealed record OperationAcceptedResponse(string OperationId, string Status);
