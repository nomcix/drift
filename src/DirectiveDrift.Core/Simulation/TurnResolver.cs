using System.Collections.Immutable;
using System.Text;
using DirectiveDrift.Core.Decisions;
using DirectiveDrift.Core.Events;
using DirectiveDrift.Core.Model;
using DirectiveDrift.Core.Observations;
using DirectiveDrift.Core.Scoring;
using DirectiveDrift.Core.Serialization;

namespace DirectiveDrift.Core.Simulation;

public sealed record TurnResult(
    RunState State,
    ImmutableArray<CanonicalEvent> Events,
    ImmutableArray<PrivateObservation> Observations,
    ImmutableArray<ResolvedDecision> Decisions,
    string StateHash);

public static class TurnResolver
{
    private const int MaxRationaleLength = 180;

    public static TurnResult ResolveTurn(
        RunState preTurnState,
        IReadOnlyDictionary<AgentId, ProposedDecision> proposedDecisions)
    {
        ArgumentNullException.ThrowIfNull(proposedDecisions);

        if (preTurnState.Status != RunStatus.Active)
        {
            throw new InvalidOperationException("Only an active run can advance.");
        }

        if (preTurnState.Turn >= preTurnState.Rules.TurnLimit)
        {
            throw new InvalidOperationException("The run deadline has already been reached.");
        }

        var context = new ResolutionContext(
            preTurnState with { Turn = preTurnState.Turn + 1 },
            preTurnState.NextEventSequence);

        context.Emit(
            TurnPhase.Deliver,
            CanonicalEventType.TurnStarted,
            new TurnStartedPayload(CanonicalStateSerializer.Hash(preTurnState)));

        DeliverMessages(context);
        var observations = BuildObservations(context);
        var decisions = ValidateDecisions(context, observations, proposedDecisions);
        QueueMessages(context, decisions);
        var traversals = ResolveMovement(context, decisions);
        var effects = ResolveInteractions(context, decisions);
        ResolveThreats(context, traversals, effects);
        ResolveObjectives(context, preTurnState, decisions, effects);

        var finalState = CanonicalStateSerializer.Normalize(
            context.State with { NextEventSequence = context.NextSequence + 1 });
        var stateHash = CanonicalStateSerializer.Hash(finalState);
        context.AddTurnEnded(finalState, stateHash);

        return new TurnResult(
            finalState,
            context.Events.ToImmutableArray(),
            observations,
            decisions,
            stateHash);
    }

    private static void DeliverMessages(ResolutionContext context)
    {
        var due = context.State.Communication.QueuedMessages
            .Where(message => message.DeliveryTurn <= context.State.Turn)
            .OrderBy(message => message.DeliveryTurn)
            .ThenBy(message => message.SenderAgentId.Value, StringComparer.Ordinal)
            .ThenBy(message => message.MessageId.Value, StringComparer.Ordinal)
            .ToImmutableArray();

        if (due.Length == 0)
        {
            return;
        }

        var dueIds = due.Select(message => message.MessageId).ToHashSet();
        context.State = context.State with
        {
            Communication = context.State.Communication with
            {
                QueuedMessages = context.State.Communication.QueuedMessages
                    .Where(message => !dueIds.Contains(message.MessageId))
                    .ToImmutableArray(),
                DeliveredMessages = context.State.Communication.DeliveredMessages
                    .AddRange(due),
            },
        };

        foreach (var message in due)
        {
            context.Emit(
                TurnPhase.Deliver,
                CanonicalEventType.MessageDelivered,
                ToMessagePayload(message, null));
        }
    }

    private static ImmutableArray<PrivateObservation> BuildObservations(ResolutionContext context)
    {
        var observations = context.State.Agents
            .Where(agent => agent.Status == AgentStatus.Active)
            .OrderBy(agent => agent.AgentId.Value, StringComparer.Ordinal)
            .Select(agent => PrivateObservationBuilder.Build(context.State, agent.AgentId))
            .ToImmutableArray();

        foreach (var observation in observations)
        {
            foreach (var exit in observation.Exits.Where(
                         exit => exit.Hazard == HazardObservation.Radiation))
            {
                context.Emit(
                    TurnPhase.Observe,
                    CanonicalEventType.HazardSensed,
                    new HazardPayload(observation.AgentId, exit.ConnectionId, false));
            }
        }

        return observations;
    }

    private static ImmutableArray<ResolvedDecision> ValidateDecisions(
        ResolutionContext context,
        ImmutableArray<PrivateObservation> observations,
        IReadOnlyDictionary<AgentId, ProposedDecision> proposedDecisions)
    {
        var resolved = new List<ResolvedDecision>(observations.Length);

        foreach (var observation in observations)
        {
            var agent = context.State.Agents.Single(
                candidate => candidate.AgentId == observation.AgentId);
            proposedDecisions.TryGetValue(agent.AgentId, out var proposed);
            var fallbackReason = GetFallbackReason(context.State, agent, observation, proposed);

            ResolvedDecision decision;
            if (fallbackReason is null && proposed is not null)
            {
                var action = observation.LegalActions.Find(proposed.ActionId)
                    ?? throw new InvalidOperationException("Validated action disappeared.");
                decision = new ResolvedDecision(
                    agent.AgentId,
                    action,
                    NormalizeOptionalText(proposed.Message),
                    proposed.Rationale,
                    proposed.Memory,
                    null);
                context.State = UpdateAgent(
                    context.State,
                    agent.AgentId,
                    current => current with { Memory = proposed.Memory });
                context.Emit(
                    TurnPhase.Validate,
                    CanonicalEventType.AgentDecisionAccepted,
                    new DecisionPayload(agent.AgentId, action.ActionId, null));
            }
            else
            {
                var wait = observation.LegalActions.Find(new ActionId("wait"))
                    ?? throw new InvalidOperationException("An active agent must have a wait action.");
                decision = new ResolvedDecision(
                    agent.AgentId,
                    wait,
                    null,
                    string.Empty,
                    agent.Memory,
                    fallbackReason ?? DecisionFallbackReason.Missing);
                context.Emit(
                    TurnPhase.Validate,
                    CanonicalEventType.AgentDecisionFallback,
                    new DecisionPayload(agent.AgentId, wait.ActionId, decision.FallbackReason));
            }

            resolved.Add(decision);
        }

        return resolved.ToImmutableArray();
    }

    private static DecisionFallbackReason? GetFallbackReason(
        RunState state,
        AgentState agent,
        PrivateObservation observation,
        ProposedDecision? proposed)
    {
        if (proposed is null)
        {
            return DecisionFallbackReason.Missing;
        }

        if (observation.LegalActions.Find(proposed.ActionId) is null)
        {
            return DecisionFallbackReason.IllegalAction;
        }

        if (proposed.Message is not null
            && CountCharacters(proposed.Message) > state.Rules.MaxMessageLength)
        {
            return DecisionFallbackReason.MessageTooLong;
        }

        if (CountCharacters(proposed.Rationale) > MaxRationaleLength)
        {
            return DecisionFallbackReason.RationaleTooLong;
        }

        var memoryLimit = agent.Module.Module == SupportModule.MemoryBuffer
            ? state.Rules.MemoryBufferLength
            : state.Rules.BaseMemoryLength;

        return CountCharacters(proposed.Memory) > memoryLimit
            ? DecisionFallbackReason.MemoryTooLong
            : null;
    }

    private static void QueueMessages(
        ResolutionContext context,
        ImmutableArray<ResolvedDecision> decisions)
    {
        foreach (var decision in decisions
                     .Where(decision => decision.Message is not null)
                     .OrderBy(decision => decision.AgentId.Value, StringComparer.Ordinal))
        {
            var recipient = context.State.Agents
                .Single(agent => agent.AgentId != decision.AgentId)
                .AgentId;

            if (context.State.Communication.RemainingMessages == 0)
            {
                var rejected = new AgentMessage(
                    new MessageId($"{context.State.RunId.Value}:{context.State.Turn}:{decision.AgentId.Value}"),
                    decision.AgentId,
                    recipient,
                    context.State.Turn,
                    context.State.Turn + context.State.Rules.MessageDelayTurns,
                    decision.Message!);
                context.Emit(
                    TurnPhase.Communicate,
                    CanonicalEventType.MessageRejected,
                    ToMessagePayload(rejected, "message-budget-exhausted"));
                continue;
            }

            var message = new AgentMessage(
                new MessageId($"{context.State.RunId.Value}:{context.State.Turn}:{decision.AgentId.Value}"),
                decision.AgentId,
                recipient,
                context.State.Turn,
                context.State.Turn + context.State.Rules.MessageDelayTurns,
                decision.Message!);
            context.State = context.State with
            {
                Communication = context.State.Communication with
                {
                    RemainingMessages = context.State.Communication.RemainingMessages - 1,
                    QueuedMessages = context.State.Communication.QueuedMessages.Add(message),
                },
            };
            context.Emit(
                TurnPhase.Communicate,
                CanonicalEventType.MessageQueued,
                ToMessagePayload(message, null));
        }
    }

    private static ImmutableArray<Traversal> ResolveMovement(
        ResolutionContext context,
        ImmutableArray<ResolvedDecision> decisions)
    {
        var traversals = new List<Traversal>();

        foreach (var decision in decisions
                     .Where(decision => decision.Action.Kind == LegalActionKind.Move)
                     .OrderBy(decision => decision.AgentId.Value, StringComparer.Ordinal))
        {
            var agent = context.State.Agents.Single(
                candidate => candidate.AgentId == decision.AgentId);
            var destination = new RoomId(decision.Action.Target.Value);
            var connection = context.State.Connections.Single(candidate =>
                LegalActionGenerator.IsIncident(candidate, agent.RoomId)
                && LegalActionGenerator.OtherRoom(candidate, agent.RoomId) == destination);
            var discoveredAtDestination = context.State.Connections
                .Where(candidate => LegalActionGenerator.IsIncident(candidate, destination))
                .Select(candidate => candidate.ConnectionId);
            var from = agent.RoomId;

            context.State = UpdateAgent(
                context.State,
                agent.AgentId,
                current => current with
                {
                    RoomId = destination,
                    DiscoveredConnections = current.DiscoveredConnections
                        .AddRange(discoveredAtDestination)
                        .Distinct()
                        .OrderBy(connectionId => connectionId.Value, StringComparer.Ordinal)
                        .ToImmutableArray(),
                });
            traversals.Add(new Traversal(agent.AgentId, connection));
            context.Emit(
                TurnPhase.Move,
                CanonicalEventType.AgentMoved,
                new AgentMovedPayload(agent.AgentId, from, destination, connection.ConnectionId));
        }

        return traversals.ToImmutableArray();
    }

    private static TurnEffects ResolveInteractions(
        ResolutionContext context,
        ImmutableArray<ResolvedDecision> decisions)
    {
        var effects = new TurnEffects();

        foreach (var decision in decisions.OrderBy(
                     decision => decision.AgentId.Value,
                     StringComparer.Ordinal))
        {
            switch (decision.Action.Kind)
            {
                case LegalActionKind.Scan:
                    ResolveScan(context, decision);
                    break;
                case LegalActionKind.RepairGenerator:
                    ResolveGeneratorStart(context, decision, effects);
                    break;
                case LegalActionKind.ContinueGeneratorRepair:
                    effects.GeneratorContinuationAgentId = decision.AgentId;
                    context.Emit(
                        TurnPhase.Interact,
                        CanonicalEventType.RepairContinued,
                        new RepairPayload(decision.AgentId, context.State.Generator.DeviceId));
                    break;
                case LegalActionKind.RepairConsole:
                    ResolveConsoleRepair(context, decision);
                    break;
                case LegalActionKind.ActivateConsole:
                    effects.ConsoleActivations.Add(
                        new ConsoleActivation(
                            decision.AgentId,
                            new DeviceId(decision.Action.Target.Value)));
                    context.Emit(
                        TurnPhase.Interact,
                        CanonicalEventType.ConsoleActivated,
                        new ConsolePayload(
                            decision.AgentId,
                            new DeviceId(decision.Action.Target.Value)));
                    break;
                case LegalActionKind.PickupRecorder:
                    ResolveRecorderPickup(context, decision);
                    break;
                case LegalActionKind.DeployDecoyBeacon:
                    ResolveDecoyDeployment(context, decision);
                    break;
                case LegalActionKind.Move:
                case LegalActionKind.Wait:
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unsupported action kind '{decision.Action.Kind}'.");
            }
        }

        return effects;
    }

    private static void ResolveScan(ResolutionContext context, ResolvedDecision decision)
    {
        var roomId = new RoomId(decision.Action.Target.Value);
        context.State = UpdateAgent(
            context.State,
            decision.AgentId,
            agent => agent with
            {
                ScannedRooms = agent.ScannedRooms
                    .Add(roomId)
                    .Distinct()
                    .OrderBy(room => room.Value, StringComparer.Ordinal)
                    .ToImmutableArray(),
            });
        context.Emit(
            TurnPhase.Interact,
            CanonicalEventType.RoomScanned,
            new RoomScannedPayload(decision.AgentId, roomId));
    }

    private static void ResolveGeneratorStart(
        ResolutionContext context,
        ResolvedDecision decision,
        TurnEffects effects)
    {
        effects.GeneratorStartAgentId = decision.AgentId;
        context.Emit(
            TurnPhase.Interact,
            CanonicalEventType.RepairStarted,
            new RepairPayload(decision.AgentId, context.State.Generator.DeviceId));

        var agent = context.State.Agents.Single(candidate => candidate.AgentId == decision.AgentId);
        if (agent.Module.Module != SupportModule.RapidRepairKit
            || agent.Module.ChargesRemaining == 0)
        {
            return;
        }

        ConsumeModule(context, decision.AgentId);
        effects.RapidRepairCompleted = true;
    }

    private static void ResolveConsoleRepair(
        ResolutionContext context,
        ResolvedDecision decision)
    {
        var deviceId = new DeviceId(decision.Action.Target.Value);
        if (context.State.ConsoleAlpha.DeviceId == deviceId)
        {
            context.State = context.State with
            {
                ConsoleAlpha = context.State.ConsoleAlpha with
                {
                    Condition = ConsoleCondition.Operational,
                },
            };
        }
        else
        {
            context.State = context.State with
            {
                ConsoleBeta = context.State.ConsoleBeta with
                {
                    Condition = ConsoleCondition.Operational,
                },
            };
        }

        context.Emit(
            TurnPhase.Interact,
            CanonicalEventType.ConsoleRepaired,
            new ConsolePayload(decision.AgentId, deviceId));
    }

    private static void ResolveRecorderPickup(
        ResolutionContext context,
        ResolvedDecision decision)
    {
        if (context.State.Recorder.Condition is not (
                RecorderCondition.Available or RecorderCondition.Dropped))
        {
            return;
        }

        var agent = context.State.Agents.Single(candidate => candidate.AgentId == decision.AgentId);
        var pickupRoom = agent.RoomId;
        context.State = UpdateAgent(
            context.State,
            agent.AgentId,
            current => current with { CarriedItemId = context.State.Recorder.ItemId });
        context.State = context.State with
        {
            Recorder = context.State.Recorder with
            {
                Condition = RecorderCondition.Carried,
                CarrierAgentId = agent.AgentId,
                DroppedRoomId = null,
            },
        };
        context.Emit(
            TurnPhase.Interact,
            CanonicalEventType.RecorderPickedUp,
            new RecorderPayload(context.State.Recorder.ItemId, agent.AgentId, pickupRoom));
    }

    private static void ResolveDecoyDeployment(
        ResolutionContext context,
        ResolvedDecision decision)
    {
        var agent = context.State.Agents.Single(candidate => candidate.AgentId == decision.AgentId);
        ConsumeModule(context, decision.AgentId);
        context.State = context.State with
        {
            Drone = context.State.Drone with
            {
                BeaconRoomId = agent.RoomId,
                BeaconStepsRemaining = 2,
            },
        };
    }

    private static void ResolveThreats(
        ResolutionContext context,
        ImmutableArray<Traversal> traversals,
        TurnEffects effects)
    {
        foreach (var traversal in traversals
                     .Where(traversal => traversal.Connection.HasRadiation)
                     .OrderBy(traversal => traversal.AgentId.Value, StringComparer.Ordinal))
        {
            var agent = context.State.Agents.Single(
                candidate => candidate.AgentId == traversal.AgentId);
            var prevented = agent.Module.Module == SupportModule.HazardShield
                && agent.Module.ChargesRemaining > 0;
            context.Emit(
                TurnPhase.Threat,
                CanonicalEventType.HazardTraversed,
                new HazardPayload(agent.AgentId, traversal.Connection.ConnectionId, prevented));

            if (prevented)
            {
                ConsumeModule(context, agent.AgentId);
            }
            else
            {
                DamageAgent(context, agent.AgentId, "radiation", effects);
            }
        }

        var followedBeacon = MoveDrone(context);
        if (followedBeacon)
        {
            return;
        }

        foreach (var agentId in context.State.Agents
                     .Where(agent =>
                         agent.Status == AgentStatus.Active
                         && agent.RoomId == context.State.Drone.CurrentRoomId)
                     .OrderBy(agent => agent.AgentId.Value, StringComparer.Ordinal)
                     .Select(agent => agent.AgentId)
                     .ToArray())
        {
            DamageAgent(context, agentId, "drone", effects);
        }
    }

    private static bool MoveDrone(ResolutionContext context)
    {
        var drone = context.State.Drone;
        var from = drone.CurrentRoomId;
        RoomId destination;
        var followedBeacon = drone.BeaconStepsRemaining > 0 && drone.BeaconRoomId is not null;

        if (followedBeacon)
        {
            destination = drone.BeaconRoomId!.Value;
            var remaining = drone.BeaconStepsRemaining - 1;
            drone = drone with
            {
                CurrentRoomId = destination,
                BeaconStepsRemaining = remaining,
                BeaconRoomId = remaining == 0 ? null : drone.BeaconRoomId,
            };
        }
        else
        {
            var nextIndex = (drone.PatrolIndex + 1) % drone.PatrolRoute.Length;
            destination = drone.PatrolRoute[nextIndex];
            drone = drone with
            {
                PatrolIndex = nextIndex,
                CurrentRoomId = destination,
            };
        }

        context.State = context.State with { Drone = drone };
        context.Emit(
            TurnPhase.Threat,
            CanonicalEventType.DroneMoved,
            new DroneMovedPayload(drone.EntityId, from, destination, followedBeacon));

        return followedBeacon;
    }

    private static void DamageAgent(
        ResolutionContext context,
        AgentId agentId,
        string source,
        TurnEffects effects)
    {
        var agent = context.State.Agents.Single(candidate => candidate.AgentId == agentId);
        if (agent.Status != AgentStatus.Active)
        {
            return;
        }

        var remainingHealth = Math.Max(0, agent.Health - 1);
        var status = remainingHealth == 0 ? AgentStatus.Disabled : AgentStatus.Active;
        context.State = UpdateAgent(
            context.State,
            agentId,
            current => current with { Health = remainingHealth, Status = status });
        effects.DamagedAgents.Add(agentId);
        context.Emit(
            TurnPhase.Threat,
            CanonicalEventType.AgentDamaged,
            new AgentDamagedPayload(agentId, source, remainingHealth));

        DropRecorderUnlessClamped(context, agentId);

        if (status == AgentStatus.Disabled)
        {
            context.State = context.State with
            {
                PublicFacts = context.State.PublicFacts.Add(PublicFact.AgentDisabled),
            };
            context.Emit(
                TurnPhase.Threat,
                CanonicalEventType.AgentDisabled,
                new AgentDisabledPayload(agentId));
        }
    }

    private static void DropRecorderUnlessClamped(ResolutionContext context, AgentId agentId)
    {
        var agent = context.State.Agents.Single(candidate => candidate.AgentId == agentId);
        if (agent.CarriedItemId != context.State.Recorder.ItemId)
        {
            return;
        }

        if (agent.Module.Module == SupportModule.CargoClamp
            && agent.Module.ChargesRemaining > 0)
        {
            ConsumeModule(context, agentId);
            return;
        }

        context.State = UpdateAgent(
            context.State,
            agentId,
            current => current with { CarriedItemId = null });
        context.State = context.State with
        {
            Recorder = context.State.Recorder with
            {
                Condition = RecorderCondition.Dropped,
                CarrierAgentId = null,
                DroppedRoomId = agent.RoomId,
            },
        };
        context.Emit(
            TurnPhase.Threat,
            CanonicalEventType.RecorderDropped,
            new RecorderPayload(context.State.Recorder.ItemId, agentId, agent.RoomId));
    }

    private static void ResolveObjectives(
        ResolutionContext context,
        RunState preTurnState,
        ImmutableArray<ResolvedDecision> decisions,
        TurnEffects effects)
    {
        ResolveGeneratorCommitment(context, preTurnState, decisions, effects);
        ResolveConsoleSync(context, effects);
        ResolveRecorderExtraction(context);

        if (context.State.Agents.Any(agent => agent.Status == AgentStatus.Disabled))
        {
            FailMission(context, MissionFailureReason.AgentDisabled);
            return;
        }

        var allAgentsExtracted = context.State.Agents.All(agent =>
            agent.Status == AgentStatus.Active
            && agent.RoomId == context.State.ExtractionRoomId);
        if (allAgentsExtracted
            && context.State.Recorder.Condition == RecorderCondition.Extracted)
        {
            context.State = context.State with { Status = RunStatus.Succeeded };
            var score = ScoreCalculator.Calculate(context.State).Score;
            context.Emit(
                TurnPhase.Objective,
                CanonicalEventType.MissionSucceeded,
                new MissionTerminalPayload(RunStatus.Succeeded, null, score));
            return;
        }

        if (context.State.Turn >= context.State.Rules.TurnLimit)
        {
            FailMission(context, MissionFailureReason.Deadline);
        }
    }

    private static void ResolveGeneratorCommitment(
        ResolutionContext context,
        RunState preTurnState,
        ImmutableArray<ResolvedDecision> decisions,
        TurnEffects effects)
    {
        if (effects.RapidRepairCompleted)
        {
            RestorePower(context);
            return;
        }

        if (preTurnState.Generator.Condition == GeneratorCondition.Online)
        {
            return;
        }

        if (preTurnState.Generator.Condition == GeneratorCondition.Damaged
            && effects.GeneratorStartAgentId is { } startingAgentId)
        {
            if (effects.DamagedAgents.Contains(startingAgentId))
            {
                InterruptRepair(context, startingAgentId);
            }
            else
            {
                context.State = context.State with
                {
                    Generator = context.State.Generator with
                    {
                        Condition = GeneratorCondition.Repairing,
                        RepairingAgentId = startingAgentId,
                    },
                };
            }

            return;
        }

        if (preTurnState.Generator.Condition != GeneratorCondition.Repairing
            || preTurnState.Generator.RepairingAgentId is not { } repairingAgentId)
        {
            return;
        }

        var decision = decisions.Single(candidate => candidate.AgentId == repairingAgentId);
        var continued = decision.Action.Kind == LegalActionKind.ContinueGeneratorRepair
            && effects.GeneratorContinuationAgentId == repairingAgentId
            && !effects.DamagedAgents.Contains(repairingAgentId)
            && context.State.Agents.Single(agent => agent.AgentId == repairingAgentId).Status
            == AgentStatus.Active;

        if (continued)
        {
            RestorePower(context);
        }
        else
        {
            InterruptRepair(context, repairingAgentId);
        }
    }

    private static void ResolveConsoleSync(ResolutionContext context, TurnEffects effects)
    {
        if (context.State.ArchiveGateOpen || effects.ConsoleActivations.Count == 0)
        {
            return;
        }

        var activeActivations = effects.ConsoleActivations
            .Where(activation => context.State.Agents.Any(agent =>
                agent.AgentId == activation.AgentId && agent.Status == AgentStatus.Active))
            .ToArray();
        var successful = activeActivations.Length == 2
            && activeActivations.Select(activation => activation.AgentId).Distinct().Count() == 2
            && activeActivations.Select(activation => activation.DeviceId).Distinct().Count() == 2;

        if (!successful)
        {
            context.State = context.State with
            {
                Score = context.State.Score with
                {
                    FailedConsoleActivations = context.State.Score.FailedConsoleActivations + 1,
                },
            };
            context.Emit(
                TurnPhase.Objective,
                CanonicalEventType.ConsoleSyncFailed,
                new ConsolePayload(null, effects.ConsoleActivations[0].DeviceId));
            return;
        }

        context.State = context.State with
        {
            ArchiveGateOpen = true,
            Recorder = context.State.Recorder with { Condition = RecorderCondition.Available },
            PublicFacts = context.State.PublicFacts.Add(PublicFact.ArchiveOpened),
        };
        context.Emit(
            TurnPhase.Objective,
            CanonicalEventType.ArchiveOpened,
            new ArchiveOpenedPayload());
        context.Emit(
            TurnPhase.Objective,
            CanonicalEventType.ObjectiveAdvanced,
            new ObjectiveAdvancedPayload("archive", "open"));
    }

    private static void ResolveRecorderExtraction(ResolutionContext context)
    {
        AgentId? carrierId = null;
        var atExtraction = context.State.Recorder.Condition switch
        {
            RecorderCondition.Carried when context.State.Recorder.CarrierAgentId is { } agentId =>
                context.State.Agents.Single(agent => agent.AgentId == agentId).RoomId
                == context.State.ExtractionRoomId,
            RecorderCondition.Dropped =>
                context.State.Recorder.DroppedRoomId == context.State.ExtractionRoomId,
            _ => false,
        };

        if (!atExtraction)
        {
            return;
        }

        carrierId = context.State.Recorder.CarrierAgentId;
        if (carrierId is { } value)
        {
            context.State = UpdateAgent(
                context.State,
                value,
                agent => agent with { CarriedItemId = null });
        }

        context.State = context.State with
        {
            Recorder = context.State.Recorder with
            {
                Condition = RecorderCondition.Extracted,
                CarrierAgentId = null,
                DroppedRoomId = null,
            },
        };
        context.Emit(
            TurnPhase.Objective,
            CanonicalEventType.ObjectiveAdvanced,
            new ObjectiveAdvancedPayload("flight-recorder", "extracted"));
    }

    private static void RestorePower(ResolutionContext context)
    {
        context.State = context.State with
        {
            Generator = context.State.Generator with
            {
                Condition = GeneratorCondition.Online,
                RepairingAgentId = null,
            },
            PublicFacts = context.State.PublicFacts.Add(PublicFact.PowerRestored),
        };
        context.Emit(
            TurnPhase.Objective,
            CanonicalEventType.PowerRestored,
            new PowerRestoredPayload(context.State.Generator.DeviceId));
        context.Emit(
            TurnPhase.Objective,
            CanonicalEventType.ObjectiveAdvanced,
            new ObjectiveAdvancedPayload("auxiliary-power", "online"));
    }

    private static void InterruptRepair(ResolutionContext context, AgentId agentId)
    {
        context.State = context.State with
        {
            Generator = context.State.Generator with
            {
                Condition = GeneratorCondition.Damaged,
                RepairingAgentId = null,
            },
            Score = context.State.Score with
            {
                InterruptedMajorRepairs = context.State.Score.InterruptedMajorRepairs + 1,
            },
        };
        context.Emit(
            TurnPhase.Objective,
            CanonicalEventType.RepairInterrupted,
            new RepairPayload(agentId, context.State.Generator.DeviceId));
    }

    private static void FailMission(
        ResolutionContext context,
        MissionFailureReason failureReason)
    {
        context.State = context.State with
        {
            Status = RunStatus.Failed,
            FailureReason = failureReason,
        };
        context.Emit(
            TurnPhase.Objective,
            CanonicalEventType.MissionFailed,
            new MissionTerminalPayload(RunStatus.Failed, failureReason, null));
    }

    private static void ConsumeModule(ResolutionContext context, AgentId agentId)
    {
        var module = context.State.Agents.Single(agent => agent.AgentId == agentId).Module;
        if (module.ChargesRemaining <= 0)
        {
            throw new InvalidOperationException("A spent module cannot be consumed.");
        }

        context.State = UpdateAgent(
            context.State,
            agentId,
            agent => agent with
            {
                Module = agent.Module with
                {
                    ChargesRemaining = agent.Module.ChargesRemaining - 1,
                },
            });
        context.Emit(
            TurnPhase.Interact,
            CanonicalEventType.ModuleConsumed,
            new ModuleConsumedPayload(agentId, module.Module));
    }

    private static RunState UpdateAgent(
        RunState state,
        AgentId agentId,
        Func<AgentState, AgentState> update)
    {
        return state with
        {
            Agents = state.Agents
                .Select(agent => agent.AgentId == agentId ? update(agent) : agent)
                .ToImmutableArray(),
        };
    }

    private static MessagePayload ToMessagePayload(AgentMessage message, string? rejectionReason) =>
        new(
            message.MessageId,
            message.SenderAgentId,
            message.RecipientAgentId,
            message.SentTurn,
            message.DeliveryTurn,
            message.Text,
            rejectionReason);

    private static string? NormalizeOptionalText(string? text) =>
        string.IsNullOrEmpty(text) ? null : text;

    private static int CountCharacters(string value) => value.EnumerateRunes().Count();

    private sealed record Traversal(AgentId AgentId, ConnectionState Connection);

    private sealed record ConsoleActivation(AgentId AgentId, DeviceId DeviceId);

    private sealed class TurnEffects
    {
        public AgentId? GeneratorStartAgentId { get; set; }

        public AgentId? GeneratorContinuationAgentId { get; set; }

        public bool RapidRepairCompleted { get; set; }

        public List<ConsoleActivation> ConsoleActivations { get; } = [];

        public HashSet<AgentId> DamagedAgents { get; } = [];
    }

    private sealed class ResolutionContext(RunState state, long nextSequence)
    {
        public RunState State { get; set; } = state;

        public long NextSequence { get; private set; } = nextSequence;

        public List<CanonicalEvent> Events { get; } = [];

        public void Emit(
            TurnPhase phase,
            CanonicalEventType type,
            ICanonicalEventPayload payload)
        {
            Events.Add(
                new CanonicalEvent(
                    new EventId($"{State.RunId.Value}:{NextSequence}"),
                    NextSequence,
                    State.Turn,
                    phase,
                    type,
                    payload,
                    "1",
                    null));
            NextSequence++;
        }

        public void AddTurnEnded(RunState finalState, string stateHash)
        {
            State = finalState;
            Events.Add(
                new CanonicalEvent(
                    new EventId($"{State.RunId.Value}:{NextSequence}"),
                    NextSequence,
                    State.Turn,
                    TurnPhase.Record,
                    CanonicalEventType.TurnEnded,
                    new TurnEndedPayload(stateHash),
                    "1",
                    stateHash));
            NextSequence++;
        }
    }
}
