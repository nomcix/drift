export type AgentId = "kite" | "wren";
export type Lens = "command" | AgentId | "truth";
export type RoomShape =
  | "docking-crescent"
  | "transit-capsule"
  | "service-hex"
  | "service-spine"
  | "relay-hub"
  | "radar-octagon"
  | "reactor-ring"
  | "console-alpha"
  | "console-beta"
  | "archive-iris"
  | "archive-shield";

export type RoomPresentation = {
  readonly roomId: string;
  readonly label: string;
  readonly shortLabel: string;
  readonly shape: RoomShape;
  readonly anchor: { readonly x: number; readonly y: number };
  readonly size: { readonly w: number; readonly h: number };
};

export type ConnectionPresentation = {
  readonly connectionId: string;
  readonly fromRoomId: string;
  readonly toRoomId: string;
  readonly waypoints: readonly (readonly [number, number])[];
  readonly kiteOnly?: boolean;
};

export type StationPresentation = {
  readonly rooms: readonly RoomPresentation[];
  readonly connections: readonly ConnectionPresentation[];
};

export type ObjectiveStep = "power" | "sync" | "recorder" | "extract";
export type ObjectiveStatus = "pending" | "active" | "complete";

export type PresentationState = {
  readonly turn: number;
  readonly powerOnline: boolean;
  readonly archiveOpen: boolean;
  readonly missionStatus: "running" | "succeeded" | "failed";
  readonly agents: Readonly<Record<AgentId, {
    readonly roomId: string;
    readonly health: number;
    readonly carryingRecorder: boolean;
  }>>;
  readonly droneRoomId: string;
  readonly discovered: Readonly<Record<AgentId, readonly string[]>>;
  readonly scannedRooms: readonly string[];
  readonly contaminatedConnections: readonly string[];
  readonly sensedConnections: readonly string[];
  readonly damagedRooms: readonly string[];
  readonly repairedDevices: readonly string[];
  readonly activatedConsoles: readonly string[];
  readonly objectives: Readonly<Record<ObjectiveStep, ObjectiveStatus>>;
  readonly lastEvent: CanonicalPresentationEvent["type"] | null;
  readonly eventLog: readonly string[];
};

type EventEnvelope<TType extends string, TPayload> = {
  readonly sequence: number;
  readonly turn: number;
  readonly type: TType;
  readonly payload: TPayload;
};

export type CanonicalPresentationEvent =
  | EventEnvelope<"AgentMoved", { readonly agentId: AgentId; readonly fromRoomId: string; readonly toRoomId: string; readonly connectionId: string }>
  | EventEnvelope<"RoomScanned", { readonly agentId: AgentId; readonly roomId: string; readonly discoveredRoomIds: readonly string[] }>
  | EventEnvelope<"HazardSensed", { readonly agentId: AgentId; readonly connectionId: string }>
  | EventEnvelope<"RepairStarted" | "RepairContinued", { readonly agentId: AgentId; readonly deviceId: string; readonly roomId: string }>
  | EventEnvelope<"PowerRestored", { readonly deviceId: string }>
  | EventEnvelope<"ConsoleActivated", { readonly agentId: AgentId; readonly deviceId: string }>
  | EventEnvelope<"ConsoleSyncFailed", Record<string, never>>
  | EventEnvelope<"ArchiveOpened", Record<string, never>>
  | EventEnvelope<"DroneMoved", { readonly toRoomId: string }>
  | EventEnvelope<"AgentDamaged", { readonly agentId: AgentId; readonly source: string; readonly remainingHealth: number }>
  | EventEnvelope<"RecorderPickedUp", { readonly agentId: AgentId; readonly itemId: string; readonly roomId: string }>
  | EventEnvelope<"MissionSucceeded", { readonly score: number }>
  | EventEnvelope<"MissionFailed", Record<string, never>>
  | EventEnvelope<"TurnStarted" | "TurnEnded", Record<string, never>>;

export type AnimationIntent = {
  readonly sequence: number;
  readonly eventType: CanonicalPresentationEvent["type"];
  readonly durationMs: number;
  readonly label: string;
};

export type PlaybackSpeed = 1 | 2;
