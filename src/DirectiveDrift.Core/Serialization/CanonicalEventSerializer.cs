using System.Collections.Immutable;
using System.Text.Json;
using DirectiveDrift.Core.Events;

namespace DirectiveDrift.Core.Serialization;

public static class CanonicalEventSerializer
{
    public static byte[] Serialize(ImmutableArray<CanonicalEvent> events) =>
        JsonSerializer.SerializeToUtf8Bytes(events, CanonicalJson.Options);

    public static ImmutableArray<CanonicalEvent> Deserialize(ReadOnlySpan<byte> utf8Json) =>
        JsonSerializer.Deserialize<ImmutableArray<CanonicalEvent>>(utf8Json, CanonicalJson.Options);
}
