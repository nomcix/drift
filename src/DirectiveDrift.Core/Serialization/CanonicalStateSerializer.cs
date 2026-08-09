using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using DirectiveDrift.Core.Model;

namespace DirectiveDrift.Core.Serialization;

public static class CanonicalStateSerializer
{
    public const string Version = "dd-state-1";

    public static byte[] Serialize(RunState state) =>
        JsonSerializer.SerializeToUtf8Bytes(Normalize(state), CanonicalJson.Options);

    public static RunState Deserialize(ReadOnlySpan<byte> utf8Json)
    {
        var state = JsonSerializer.Deserialize<RunState>(utf8Json, CanonicalJson.Options)
            ?? throw new JsonException("Canonical run state was null.");

        return Normalize(state);
    }

    public static string Hash(RunState state) =>
        Convert.ToHexString(SHA256.HashData(Serialize(state))).ToLowerInvariant();

    public static RunState Normalize(RunState state)
    {
        return state with
        {
            Rooms = state.Rooms.OrderBy(room => room.Value, StringComparer.Ordinal).ToImmutableArray(),
            Agents = state.Agents
                .Select(agent => agent with
                {
                    DiscoveredConnections = agent.DiscoveredConnections
                        .Distinct()
                        .OrderBy(connection => connection.Value, StringComparer.Ordinal)
                        .ToImmutableArray(),
                    ScannedRooms = agent.ScannedRooms
                        .Distinct()
                        .OrderBy(room => room.Value, StringComparer.Ordinal)
                        .ToImmutableArray(),
                })
                .OrderBy(agent => agent.AgentId.Value, StringComparer.Ordinal)
                .ToImmutableArray(),
            Connections = state.Connections
                .OrderBy(connection => connection.ConnectionId.Value, StringComparer.Ordinal)
                .ToImmutableArray(),
            Communication = state.Communication with
            {
                QueuedMessages = state.Communication.QueuedMessages
                    .OrderBy(message => message.DeliveryTurn)
                    .ThenBy(message => message.SenderAgentId.Value, StringComparer.Ordinal)
                    .ThenBy(message => message.MessageId.Value, StringComparer.Ordinal)
                    .ToImmutableArray(),
                DeliveredMessages = state.Communication.DeliveredMessages
                    .OrderBy(message => message.DeliveryTurn)
                    .ThenBy(message => message.MessageId.Value, StringComparer.Ordinal)
                    .ToImmutableArray(),
            },
            PublicFacts = state.PublicFacts.Distinct().Order().ToImmutableArray(),
        };
    }
}
