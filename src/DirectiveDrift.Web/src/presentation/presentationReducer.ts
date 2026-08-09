import type { AnimationIntent, CanonicalPresentationEvent, PresentationState } from "./model";

function appendUnique(values: readonly string[], additions: readonly string[]) {
  return [...new Set([...values, ...additions])];
}

function describeEvent(event: CanonicalPresentationEvent) {
  switch (event.type) {
    case "AgentMoved": return `${event.payload.agentId} moved to ${event.payload.toRoomId}`;
    case "RoomScanned": return `${event.payload.agentId} scanned ${event.payload.roomId}`;
    case "HazardSensed": return `${event.payload.agentId} sensed a hazard`;
    case "RepairStarted": return `repair started at ${event.payload.roomId}`;
    case "RepairContinued": return `repair continued at ${event.payload.roomId}`;
    case "PowerRestored": return "auxiliary power restored";
    case "ConsoleActivated": return `${event.payload.deviceId} activated`;
    case "ArchiveOpened": return "archive gate opened";
    case "AgentDamaged": return `${event.payload.agentId} damaged by ${event.payload.source}`;
    case "RecorderPickedUp": return `${event.payload.agentId} secured the recorder`;
    case "MissionSucceeded": return `mission succeeded · score ${String(event.payload.score)}`;
    case "TurnStarted": return `turn ${String(event.turn)} started`;
    case "TurnEnded": return `turn ${String(event.turn)} ended`;
  }
}

export function presentationReducer(state: PresentationState, event: CanonicalPresentationEvent): PresentationState {
  const base = {
    ...state,
    turn: event.turn,
    lastEvent: event.type,
    eventLog: [...state.eventLog, describeEvent(event)],
  };

  switch (event.type) {
    case "AgentMoved":
      return {
        ...base,
        agents: { ...state.agents, [event.payload.agentId]: { ...state.agents[event.payload.agentId], roomId: event.payload.toRoomId } },
        discovered: { ...state.discovered, [event.payload.agentId]: appendUnique(state.discovered[event.payload.agentId], [event.payload.toRoomId]) },
      };
    case "RoomScanned":
      return {
        ...base,
        scannedRooms: appendUnique(state.scannedRooms, [event.payload.roomId]),
        discovered: { ...state.discovered, [event.payload.agentId]: appendUnique(state.discovered[event.payload.agentId], event.payload.discoveredRoomIds) },
      };
    case "HazardSensed":
      return { ...base, sensedConnections: appendUnique(state.sensedConnections, [event.payload.connectionId]) };
    case "RepairStarted":
    case "RepairContinued":
      return { ...base, repairedDevices: appendUnique(state.repairedDevices, [event.payload.deviceId]) };
    case "PowerRestored":
      return { ...base, powerOnline: true, objectives: { ...state.objectives, power: "complete", sync: "active" } };
    case "ConsoleActivated": {
      const activatedConsoles = appendUnique(state.activatedConsoles, [event.payload.deviceId]);
      return { ...base, activatedConsoles };
    }
    case "ArchiveOpened":
      return { ...base, archiveOpen: true, objectives: { ...state.objectives, sync: "complete", recorder: "active" } };
    case "AgentDamaged":
      return {
        ...base,
        agents: { ...state.agents, [event.payload.agentId]: { ...state.agents[event.payload.agentId], health: event.payload.remainingHealth } },
        damagedRooms: appendUnique(state.damagedRooms, [state.agents[event.payload.agentId].roomId]),
      };
    case "RecorderPickedUp":
      return {
        ...base,
        agents: { ...state.agents, [event.payload.agentId]: { ...state.agents[event.payload.agentId], carryingRecorder: true } },
        objectives: { ...state.objectives, recorder: "complete", extract: "active" },
      };
    case "MissionSucceeded":
      return { ...base, missionStatus: "succeeded", objectives: { power: "complete", sync: "complete", recorder: "complete", extract: "complete" } };
    case "TurnStarted":
    case "TurnEnded":
      return base;
  }
}

export function reducePresentation(initial: PresentationState, events: readonly CanonicalPresentationEvent[]) {
  return events.reduce(presentationReducer, initial);
}

const durations: Readonly<Partial<Record<CanonicalPresentationEvent["type"], number>>> = {
  AgentMoved: 650,
  RoomScanned: 800,
  HazardSensed: 450,
  RepairStarted: 550,
  RepairContinued: 550,
  PowerRestored: 1700,
  ConsoleActivated: 600,
  ArchiveOpened: 1300,
  AgentDamaged: 450,
  RecorderPickedUp: 500,
  MissionSucceeded: 1800,
};

export function animationIntent(event: CanonicalPresentationEvent, speed: 1 | 2): AnimationIntent {
  const baseDuration = durations[event.type] ?? 120;
  return {
    sequence: event.sequence,
    eventType: event.type,
    durationMs: Math.max(120, Math.round(baseDuration / speed)),
    label: describeEvent(event),
  };
}

export function buildAnimationQueue(events: readonly CanonicalPresentationEvent[], speed: 1 | 2) {
  return events.map((event) => animationIntent(event, speed));
}
