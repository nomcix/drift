using System.Collections.Immutable;
using System.Text;
using DirectiveDrift.Core.Decisions;
using DirectiveDrift.Core.Events;
using DirectiveDrift.Core.Model;
using DirectiveDrift.Core.Simulation;

namespace DirectiveDrift.Content.Solving;

public static class ReferenceSolver
{
    public const ulong ScriptedSeed = 0x434f4c442d535441UL;
    public const ulong ScriptedStream = 0x5245462d534f4c56UL;

    public static ReferenceSolution Solve(
        RunDefinition definition,
        ReferencePolicyOptions options)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(definition, options);

        var start = RunStartFactory.Create(
            new RunId($"reference-{definition.Mission.VariantId.Value}"),
            definition,
            ScriptedSeed,
            ScriptedStream);
        var seen = new HashSet<string>(StringComparer.Ordinal) { CreateStateKey(start.State) };
        long enqueueOrder = 0;
        var root = new SearchNode(start.State, null, null);
        var pending = new Stack<SearchNode>();
        pending.Push(root);
        var expanded = 0;

        while (pending.TryPop(out var node) && expanded < options.MaximumExpandedStates)
        {
            if (node.State.Status == RunStatus.Succeeded
                && HasRequiredModuleUse(node.State, definition, options))
            {
                return BuildSolution(start, node, expanded);
            }

            if (node.State.Status != RunStatus.Active
                || node.State.Turn >= options.MaximumTurns)
            {
                continue;
            }

            expanded++;
            var children = new List<RankedNode>();
            foreach (var selections in GenerateDecisionPairs(node.State, definition, options))
            {
                var proposed = selections.ToDictionary(
                    selection => selection.AgentId,
                    selection => new ProposedDecision(
                        selection.ActionId,
                        null,
                        "reference-policy",
                        string.Empty));
                var turn = TurnResolver.ResolveTurn(node.State, proposed);

                if (turn.Decisions.Any(decision => decision.UsedFallback)
                    || turn.State.Status == RunStatus.Failed
                    || options.RequireNoDamage && HasDamage(turn.State))
                {
                    continue;
                }

                var key = CreateStateKey(turn.State);
                if (!seen.Add(key))
                {
                    continue;
                }

                var scriptedTurn = new ScriptedTurn(turn.State.Turn, selections);
                var child = new SearchNode(turn.State, node, scriptedTurn);
                children.Add(
                    new RankedNode(
                        child,
                        CreatePriority(turn.State, definition, options, enqueueOrder++)));
            }

            foreach (var child in children.OrderByDescending(candidate => candidate.Priority))
            {
                pending.Push(child.Node);
            }
        }

        var reason = expanded >= options.MaximumExpandedStates
            ? $"Search limit of {options.MaximumExpandedStates} states was reached."
            : $"No policy proof succeeded within {options.MaximumTurns} turns after expanding {expanded} states.";
        return new ReferenceSolution(false, null, [], [], expanded, reason);
    }

    public static ReferenceSolution Replay(
        RunDefinition definition,
        IEnumerable<ScriptedTurn> scriptedTurns)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(scriptedTurns);

        var start = RunStartFactory.Create(
            new RunId($"reference-{definition.Mission.VariantId.Value}"),
            definition,
            ScriptedSeed,
            ScriptedStream);
        var state = start.State;
        var events = ImmutableArray.CreateBuilder<CanonicalEvent>();
        events.Add(start.Event);
        var turns = scriptedTurns.OrderBy(turn => turn.Turn).ToImmutableArray();

        foreach (var scriptedTurn in turns)
        {
            if (state.Status != RunStatus.Active || scriptedTurn.Turn != state.Turn + 1)
            {
                return new ReferenceSolution(
                    false,
                    state,
                    turns,
                    events.ToImmutable(),
                    0,
                    $"Scripted turn {scriptedTurn.Turn} is not the next active turn; "
                    + $"state is {state.Status} at turn {state.Turn} ({state.FailureReason}).");
            }

            var decisions = scriptedTurn.Decisions.ToDictionary(
                decision => decision.AgentId,
                decision => new ProposedDecision(
                    decision.ActionId,
                    null,
                    "scripted-policy",
                    string.Empty));
            var result = TurnResolver.ResolveTurn(state, decisions);
            events.AddRange(result.Events);
            state = result.State;
        }

        return new ReferenceSolution(
            state.Status == RunStatus.Succeeded,
            state,
            turns,
            events.ToImmutable(),
            0,
            state.Status == RunStatus.Succeeded ? null : $"Script ended with status {state.Status}.");
    }

    private static IEnumerable<ImmutableArray<ScriptedAgentDecision>> GenerateDecisionPairs(
        RunState state,
        RunDefinition definition,
        ReferencePolicyOptions options)
    {
        var agents = state.Agents
            .OrderBy(agent => agent.AgentId.Value, StringComparer.Ordinal)
            .ToArray();
        var firstActions = GetCandidateActions(state, definition, options, agents[0]);
        var secondActions = GetCandidateActions(state, definition, options, agents[1]);

        foreach (var first in firstActions)
        {
            foreach (var second in secondActions)
            {
                yield return
                [
                    new ScriptedAgentDecision(agents[0].AgentId, first.ActionId),
                    new ScriptedAgentDecision(agents[1].AgentId, second.ActionId),
                ];
            }
        }
    }

    private static LegalAction[] GetCandidateActions(
        RunState state,
        RunDefinition definition,
        ReferencePolicyOptions options,
        AgentState agent)
    {
        var candidates = LegalActionGenerator.GetLegalActions(state, agent.AgentId).Actions
            .Where(action => IsUsefulAction(state, definition, options, agent, action))
            .OrderBy(ActionOrder)
            .ThenBy(action => action.ActionId.Value, StringComparer.Ordinal)
            .ToArray();

        var mandatory = candidates.FirstOrDefault(action => action.Kind is
            LegalActionKind.ContinueGeneratorRepair
            or LegalActionKind.RepairGenerator
            or LegalActionKind.RepairConsole
            or LegalActionKind.PickupRecorder);
        if (mandatory is not null)
        {
            return [mandatory];
        }

        var moves = candidates.Where(action => action.Kind == LegalActionKind.Move).ToArray();
        var waitCanMatter = GetCurrentTargets(state, options, agent).Contains(agent.RoomId)
            || candidates.Any(action => action.Kind == LegalActionKind.ActivateConsole)
            || moves.Length == 0
            || moves.Any(action => IsNextDroneDestination(state, new RoomId(action.Target.Value)));

        return waitCanMatter
            ? candidates
            : candidates.Where(action => action.Kind != LegalActionKind.Wait).ToArray();
    }

    private static bool IsUsefulAction(
        RunState state,
        RunDefinition definition,
        ReferencePolicyOptions options,
        AgentState agent,
        LegalAction action)
    {
        if (action.Kind == LegalActionKind.Scan)
        {
            return false;
        }

        if (action.Kind == LegalActionKind.ActivateConsole)
        {
            var alphaAgent = options.AlphaAgentId;
            var expectedAgent = new DeviceId(action.Target.Value) == definition.ConsoleAlpha.DeviceId
                ? alphaAgent
                : definition.Agents.Single(candidate => candidate.AgentId != alphaAgent).AgentId;

            if (agent.AgentId != expectedAgent || state.Turn + 1 < options.MinimumSyncTurn)
            {
                return false;
            }

            var partner = state.Agents.Single(candidate => candidate.AgentId != agent.AgentId);
            var partnerDeviceId = agent.AgentId == alphaAgent
                ? definition.ConsoleBeta.DeviceId
                : definition.ConsoleAlpha.DeviceId;

            return LegalActionGenerator.GetLegalActions(state, partner.AgentId).Actions.Any(
                partnerAction => partnerAction.Kind == LegalActionKind.ActivateConsole
                    && new DeviceId(partnerAction.Target.Value) == partnerDeviceId);
        }

        if (action.Kind == LegalActionKind.PickupRecorder)
        {
            return agent.AgentId == options.RecorderCarrierAgentId;
        }

        if (action.Kind == LegalActionKind.DeployDecoyBeacon)
        {
            return options.RequiredConsumedModule == SupportModule.DecoyBeacon;
        }

        if (action.Kind == LegalActionKind.Move)
        {
            var destination = new RoomId(action.Target.Value);
            var connection = state.Connections.Single(candidate =>
                candidate.RoomA == agent.RoomId && candidate.RoomB == destination
                || candidate.RoomB == agent.RoomId && candidate.RoomA == destination);
            var shielded = agent.Module.Module == SupportModule.HazardShield
                && agent.Module.ChargesRemaining > 0;

            if (options.RequireNoDamage && connection.HasRadiation && !shielded)
            {
                return false;
            }

            if (agent.Health <= 1 && IsNextDroneDestination(state, destination))
            {
                return false;
            }

            if (!IsProgressOrEvasionMove(state, options, agent, destination))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsNextDroneDestination(RunState state, RoomId roomId)
    {
        return state.Drone.BeaconStepsRemaining == 0
            && state.Drone.PatrolRoute[(state.Drone.PatrolIndex + 1) % state.Drone.PatrolRoute.Length]
            == roomId;
    }

    private static bool IsProgressOrEvasionMove(
        RunState state,
        ReferencePolicyOptions options,
        AgentState agent,
        RoomId destination)
    {
        var nextDroneRoom = state.Drone.BeaconStepsRemaining == 0
            ? state.Drone.PatrolRoute[(state.Drone.PatrolIndex + 1) % state.Drone.PatrolRoute.Length]
            : (RoomId?)null;
        if (nextDroneRoom == agent.RoomId)
        {
            return destination != nextDroneRoom;
        }

        var targets = GetCurrentTargets(state, options, agent);
        return targets.Any(target =>
        {
            var currentDistance = NavigationDistance(
                state,
                agent,
                agent.RoomId,
                target,
                options.RequireNoDamage);
            var destinationDistance = NavigationDistance(
                state,
                agent,
                destination,
                target,
                options.RequireNoDamage);
            return destinationDistance <= currentDistance;
        });
    }

    private static RoomId[] GetCurrentTargets(
        RunState state,
        ReferencePolicyOptions options,
        AgentState agent)
    {
        if (state.ArchiveGateOpen)
        {
            if (state.Recorder.Condition == RecorderCondition.Extracted)
            {
                return [state.ExtractionRoomId];
            }

            if (agent.AgentId != options.RecorderCarrierAgentId)
            {
                return [state.ExtractionRoomId];
            }

            return state.Recorder.Condition switch
            {
                RecorderCondition.Available or RecorderCondition.Secured =>
                    [state.Recorder.ArchiveRoomId],
                RecorderCondition.Dropped when state.Recorder.DroppedRoomId is { } roomId => [roomId],
                _ => [state.ExtractionRoomId],
            };
        }

        var assignedConsole = agent.AgentId == options.AlphaAgentId
            ? state.ConsoleAlpha
            : state.ConsoleBeta;
        if (agent.Archetype != AgentArchetype.Engineer)
        {
            return [assignedConsole.RoomId];
        }

        var damagedConsole = new[] { state.ConsoleAlpha, state.ConsoleBeta }
            .SingleOrDefault(console => console.Condition == ConsoleCondition.Damaged);
        if (state.Generator.Condition != GeneratorCondition.Online && damagedConsole is not null)
        {
            return [state.Generator.RoomId, damagedConsole.RoomId];
        }

        if (state.Generator.Condition != GeneratorCondition.Online)
        {
            return [state.Generator.RoomId];
        }

        return damagedConsole is null
            ? [assignedConsole.RoomId]
            : [damagedConsole.RoomId];
    }

    private static int NavigationDistance(
        RunState state,
        AgentState agent,
        RoomId from,
        RoomId to,
        bool avoidDamage)
    {
        if (from == to)
        {
            return 0;
        }

        var pending = new Queue<(RoomId RoomId, int Distance)>();
        var visited = new HashSet<RoomId> { from };
        pending.Enqueue((from, 0));

        while (pending.TryDequeue(out var current))
        {
            foreach (var connection in state.Connections.Where(connection =>
                         connection.RoomA == current.RoomId || connection.RoomB == current.RoomId))
            {
                if (!LegalActionGenerator.CanTraverse(state, agent, connection)
                    || avoidDamage
                    && connection.HasRadiation
                    && !(agent.Module.Module == SupportModule.HazardShield
                         && agent.Module.ChargesRemaining > 0))
                {
                    continue;
                }

                var next = connection.RoomA == current.RoomId
                    ? connection.RoomB
                    : connection.RoomA;
                if (next == to)
                {
                    return current.Distance + 1;
                }

                if (visited.Add(next))
                {
                    pending.Enqueue((next, current.Distance + 1));
                }
            }
        }

        return state.Rules.TurnLimit + 1;
    }

    private static int ActionOrder(LegalAction action) => action.Kind switch
    {
        LegalActionKind.ContinueGeneratorRepair => 0,
        LegalActionKind.RepairGenerator => 1,
        LegalActionKind.RepairConsole => 2,
        LegalActionKind.ActivateConsole => 3,
        LegalActionKind.PickupRecorder => 4,
        LegalActionKind.DeployDecoyBeacon => 5,
        LegalActionKind.Move => 6,
        LegalActionKind.Wait => 7,
        _ => 8,
    };

    private static (int Estimate, int Turn, long Order) CreatePriority(
        RunState state,
        RunDefinition definition,
        ReferencePolicyOptions options,
        long order)
    {
        return (
            state.Turn + EstimateRemainingTurns(state, definition, options),
            state.Turn,
            order);
    }

    private static int EstimateRemainingTurns(
        RunState state,
        RunDefinition definition,
        ReferencePolicyOptions options)
    {
        if (state.Status == RunStatus.Succeeded)
        {
            return 0;
        }

        var alphaAgent = state.Agents.Single(agent => agent.AgentId == options.AlphaAgentId);
        var betaAgent = state.Agents.Single(agent => agent.AgentId != options.AlphaAgentId);

        if (!state.ArchiveGateOpen)
        {
            var alphaDistance = Distance(definition, alphaAgent, alphaAgent.RoomId, state.ConsoleAlpha.RoomId);
            var betaDistance = Distance(definition, betaAgent, betaAgent.RoomId, state.ConsoleBeta.RoomId);
            var engineer = state.Agents.Single(
                agent => agent.Archetype == AgentArchetype.Engineer);
            var engineeringWork = 0;

            if (state.Generator.Condition != GeneratorCondition.Online)
            {
                engineeringWork += Distance(
                    definition,
                    engineer,
                    engineer.RoomId,
                    state.Generator.RoomId);
                engineeringWork += state.Generator.Condition == GeneratorCondition.Repairing ? 1 : 2;
            }

            var damagedConsole = new[] { state.ConsoleAlpha, state.ConsoleBeta }
                .SingleOrDefault(console => console.Condition == ConsoleCondition.Damaged);
            if (damagedConsole is not null)
            {
                engineeringWork += Distance(
                    definition,
                    engineer,
                    state.Generator.RoomId,
                    damagedConsole.RoomId) + 1;
            }

            return Math.Max(Math.Max(alphaDistance, betaDistance), engineeringWork) + 1;
        }

        var carrier = state.Agents.Single(agent => agent.AgentId == options.RecorderCarrierAgentId);
        var partner = state.Agents.Single(agent => agent.AgentId != options.RecorderCarrierAgentId);
        var partnerDistance = Distance(
            definition,
            partner,
            partner.RoomId,
            state.ExtractionRoomId);
        var carrierDistance = state.Recorder.Condition switch
        {
            RecorderCondition.Secured or RecorderCondition.Available =>
                Distance(definition, carrier, carrier.RoomId, state.Recorder.ArchiveRoomId)
                + 1
                + Distance(
                    definition,
                    carrier,
                    state.Recorder.ArchiveRoomId,
                    state.ExtractionRoomId),
            RecorderCondition.Carried => Distance(
                definition,
                carrier,
                carrier.RoomId,
                state.ExtractionRoomId),
            RecorderCondition.Dropped when state.Recorder.DroppedRoomId is { } droppedRoom =>
                Distance(definition, carrier, carrier.RoomId, droppedRoom)
                + 1
                + Distance(definition, carrier, droppedRoom, state.ExtractionRoomId),
            RecorderCondition.Extracted => 0,
            _ => options.MaximumTurns + 1,
        };

        return Math.Max(carrierDistance, partnerDistance);
    }

    private static int Distance(
        RunDefinition definition,
        AgentState agent,
        RoomId from,
        RoomId to)
    {
        if (from == to)
        {
            return 0;
        }

        var pending = new Queue<(RoomId RoomId, int Distance)>();
        var visited = new HashSet<RoomId> { from };
        pending.Enqueue((from, 0));

        while (pending.TryDequeue(out var current))
        {
            foreach (var connection in definition.Connections.Where(connection =>
                         connection.RoomA == current.RoomId || connection.RoomB == current.RoomId))
            {
                if (connection.Access == ConnectionAccess.ReconCrawlspace
                    && !agent.Capabilities.HasFlag(AgentCapabilities.UseCrawlspace))
                {
                    continue;
                }

                var next = connection.RoomA == current.RoomId
                    ? connection.RoomB
                    : connection.RoomA;
                if (next == to)
                {
                    return current.Distance + 1;
                }

                if (visited.Add(next))
                {
                    pending.Enqueue((next, current.Distance + 1));
                }
            }
        }

        return definition.Rules.TurnLimit + 1;
    }

    private static bool HasDamage(RunState state) => state.Agents.Any(
        agent => agent.Health != agent.MaxHealth);

    private static bool HasRequiredModuleUse(
        RunState state,
        RunDefinition definition,
        ReferencePolicyOptions options)
    {
        if (options.RequiredConsumedModule is not { } required)
        {
            return true;
        }

        var assignedAgentIds = definition.Agents
            .Where(agent => agent.Module == required)
            .Select(agent => agent.AgentId)
            .ToHashSet();

        return assignedAgentIds.Count > 0
            && state.Agents.Any(agent =>
                assignedAgentIds.Contains(agent.AgentId)
                && agent.Module.ChargesRemaining == 0);
    }

    private static string CreateStateKey(RunState state)
    {
        var builder = new StringBuilder(512);
        builder.Append(state.Turn).Append('|').Append((int)state.Status).Append('|');

        foreach (var agent in state.Agents.OrderBy(
                     agent => agent.AgentId.Value,
                     StringComparer.Ordinal))
        {
            builder.Append(agent.AgentId.Value).Append(':')
                .Append(agent.RoomId.Value).Append(':')
                .Append(agent.Health).Append(':')
                .Append((int)agent.Status).Append(':')
                .Append(agent.CarriedItemId?.Value).Append(':')
                .Append((int)agent.Module.Module).Append(':')
                .Append(agent.Module.ChargesRemaining).Append(':');
            foreach (var connectionId in agent.DiscoveredConnections.OrderBy(
                         connectionId => connectionId.Value,
                         StringComparer.Ordinal))
            {
                builder.Append(connectionId.Value).Append(',');
            }

            builder.Append('|');
        }

        builder.Append((int)state.Generator.Condition).Append(':')
            .Append(state.Generator.RepairingAgentId?.Value).Append('|')
            .Append((int)state.ConsoleAlpha.Condition).Append(':')
            .Append((int)state.ConsoleBeta.Condition).Append('|')
            .Append(state.ArchiveGateOpen).Append('|')
            .Append((int)state.Recorder.Condition).Append(':')
            .Append(state.Recorder.CarrierAgentId?.Value).Append(':')
            .Append(state.Recorder.DroppedRoomId?.Value).Append('|')
            .Append(state.Drone.PatrolIndex).Append(':')
            .Append(state.Drone.CurrentRoomId.Value).Append(':')
            .Append(state.Drone.BeaconRoomId?.Value).Append(':')
            .Append(state.Drone.BeaconStepsRemaining).Append('|')
            .Append(state.Score.FailedConsoleActivations).Append(':')
            .Append(state.Score.InterruptedMajorRepairs);

        return builder.ToString();
    }

    private static ReferenceSolution BuildSolution(
        RunStartResult start,
        SearchNode terminal,
        int expanded)
    {
        var turns = new Stack<ScriptedTurn>();
        for (var current = terminal; current.Turn is not null; current = current.Parent!)
        {
            turns.Push(current.Turn);
        }

        var replay = ReplayDefinition(start, turns.ToImmutableArray());
        return new ReferenceSolution(
            true,
            replay.FinalState,
            replay.Turns,
            replay.Events,
            expanded,
            null);
    }

    private static ReferenceSolution ReplayDefinition(
        RunStartResult start,
        ImmutableArray<ScriptedTurn> turns)
    {
        var state = start.State;
        var events = ImmutableArray.CreateBuilder<CanonicalEvent>();
        events.Add(start.Event);

        foreach (var turn in turns)
        {
            var proposed = turn.Decisions.ToDictionary(
                decision => decision.AgentId,
                decision => new ProposedDecision(
                    decision.ActionId,
                    null,
                    "reference-policy",
                    string.Empty));
            var result = TurnResolver.ResolveTurn(state, proposed);
            events.AddRange(result.Events);
            state = result.State;
        }

        return new ReferenceSolution(
            state.Status == RunStatus.Succeeded,
            state,
            turns,
            events.ToImmutable(),
            0,
            null);
    }

    private static void ValidateOptions(
        RunDefinition definition,
        ReferencePolicyOptions options)
    {
        var agentIds = definition.Agents.Select(agent => agent.AgentId).ToHashSet();
        if (!agentIds.Contains(options.AlphaAgentId)
            || !agentIds.Contains(options.RecorderCarrierAgentId))
        {
            throw new ArgumentException("Reference policy agents must belong to the run definition.");
        }

        if (options.MaximumTurns <= 0
            || options.MaximumTurns > definition.Rules.TurnLimit
            || options.MinimumSyncTurn <= 0
            || options.MinimumSyncTurn > options.MaximumTurns
            || options.MaximumExpandedStates <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }
    }

    private sealed record SearchNode(
        RunState State,
        SearchNode? Parent,
        ScriptedTurn? Turn);

    private sealed record RankedNode(
        SearchNode Node,
        (int Estimate, int Turn, long Order) Priority);
}
