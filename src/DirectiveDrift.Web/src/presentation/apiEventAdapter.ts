import type { ApiEvent } from "../api/gameApi";
import type { AgentId, CanonicalPresentationEvent } from "./model";

const eventNames = [
  "RunStarted", "TurnStarted", "MessageDelivered", "AgentDecisionAccepted",
  "AgentDecisionFallback", "MessageQueued", "MessageRejected", "AgentMoved",
  "RoomScanned", "HazardSensed", "HazardTraversed", "RepairStarted",
  "RepairContinued", "RepairInterrupted", "PowerRestored", "ConsoleRepaired",
  "ConsoleActivated", "ConsoleSyncFailed", "ArchiveOpened", "RecorderPickedUp",
  "RecorderDropped", "DroneMoved", "AgentDamaged", "AgentDisabled", "ModuleConsumed",
  "ObjectiveAdvanced", "MissionSucceeded", "MissionFailed", "RunSuspended", "TurnEnded",
] as const;

function value(input: unknown): string {
  if (typeof input === "string") return input;
  if (typeof input === "object" && input !== null && "value" in input) {
    return String((input as { readonly value: unknown }).value);
  }
  return "";
}

function numberValue(input: unknown): number {
  return typeof input === "number" ? input : Number(input ?? 0);
}

export function toPresentationEvent(event: ApiEvent): CanonicalPresentationEvent | null {
  const type = typeof event.type === "number" ? eventNames[event.type] : event.type;
  const payload = event.payload;
  const common = { sequence: event.sequence, turn: event.turn };
  switch (type) {
    case "TurnStarted":
    case "TurnEnded":
    case "ConsoleSyncFailed":
    case "ArchiveOpened":
    case "MissionFailed":
      return { ...common, type, payload: {} };
    case "AgentMoved":
      return { ...common, type, payload: { agentId: value(payload["agentId"]) as AgentId, fromRoomId: value(payload["fromRoomId"]), toRoomId: value(payload["toRoomId"]), connectionId: value(payload["connectionId"]) } };
    case "RoomScanned":
      return { ...common, type, payload: { agentId: value(payload["agentId"]) as AgentId, roomId: value(payload["roomId"]), discoveredRoomIds: [value(payload["roomId"])] } };
    case "HazardSensed":
      return { ...common, type, payload: { agentId: value(payload["agentId"]) as AgentId, connectionId: value(payload["connectionId"]) } };
    case "RepairStarted":
    case "RepairContinued":
      return { ...common, type, payload: { agentId: value(payload["agentId"]) as AgentId, deviceId: value(payload["deviceId"]), roomId: value(payload["roomId"]) || "auxiliary-power" } };
    case "PowerRestored":
      return { ...common, type, payload: { deviceId: value(payload["deviceId"]) } };
    case "ConsoleActivated":
      return { ...common, type, payload: { agentId: value(payload["agentId"]) as AgentId, deviceId: value(payload["deviceId"]) } };
    case "DroneMoved":
      return { ...common, type, payload: { toRoomId: value(payload["toRoomId"]) } };
    case "AgentDamaged":
      return { ...common, type, payload: { agentId: value(payload["agentId"]) as AgentId, source: value(payload["source"]), remainingHealth: numberValue(payload["remainingHealth"]) } };
    case "RecorderPickedUp":
      return { ...common, type, payload: { agentId: value(payload["agentId"]) as AgentId, itemId: value(payload["itemId"]), roomId: value(payload["roomId"]) } };
    case "MissionSucceeded":
      return { ...common, type, payload: { score: numberValue(payload["score"]) } };
    default:
      return null;
  }
}

export function diagnosticSignals(events: readonly ApiEvent[]) {
  const types = new Set(events.map((event) => typeof event.type === "number" ? eventNames[event.type] : event.type));
  return types.has("ConsoleSyncFailed")
    ? ["Missing sync contract: Wren reached a powered console but did not know the shared activation window."]
    : [];
}
