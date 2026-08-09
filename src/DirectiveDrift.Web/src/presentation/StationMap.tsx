import { useMemo, useState } from "react";
import type { KeyboardEvent } from "react";
import type { AgentId, ConnectionPresentation, Lens, PresentationState, RoomPresentation, StationPresentation } from "./model";

type StationMapProps = {
  readonly presentation: StationPresentation;
  readonly state: PresentationState;
  readonly lens: Lens;
  readonly readable: boolean;
};

function roomKnown(state: PresentationState, lens: Lens, roomId: string) {
  return lens === "command" || lens === "truth" || state.discovered[lens].includes(roomId);
}

function connectionKnown(state: PresentationState, lens: Lens, connection: ConnectionPresentation) {
  return lens === "command" || lens === "truth" || (roomKnown(state, lens, connection.fromRoomId) && roomKnown(state, lens, connection.toRoomId));
}

function points(connection: ConnectionPresentation) {
  return connection.waypoints.map(([x, y]) => `${String(x)},${String(y)}`).join(" ");
}

function roomStateLabel(room: RoomPresentation, state: PresentationState, known: boolean) {
  if (!known) return `${room.label}: undiscovered in this lens`;
  const occupants = (Object.entries(state.agents) as [AgentId, PresentationState["agents"][AgentId]][])
    .filter(([, agent]) => agent.roomId === room.roomId)
    .map(([agentId]) => agentId);
  const details = [state.powerOnline ? "powered" : "unpowered"];
  if (occupants.length > 0) details.push(`occupied by ${occupants.join(" and ")}`);
  if (state.droneRoomId === room.roomId) details.push("security drone present");
  if (state.damagedRooms.includes(room.roomId)) details.push("recent damage");
  return `${room.label}: ${details.join(", ")}`;
}

export function StationMap({ presentation, state, lens, readable }: StationMapProps) {
  const [selectedRoomId, setSelectedRoomId] = useState("junction");
  const roomById = useMemo(() => new Map(presentation.rooms.map((room) => [room.roomId, room])), [presentation.rooms]);

  function moveFocus(roomId: string, key: string) {
    const current = roomById.get(roomId);
    if (current === undefined) return;
    const candidates = presentation.connections.flatMap((connection) => {
      if (connection.fromRoomId === roomId) return [roomById.get(connection.toRoomId)];
      if (connection.toRoomId === roomId) return [roomById.get(connection.fromRoomId)];
      return [];
    }).filter((room): room is RoomPresentation => room !== undefined && roomKnown(state, lens, room.roomId));
    const directional = candidates.filter((room) => {
      const dx = room.anchor.x - current.anchor.x;
      const dy = room.anchor.y - current.anchor.y;
      if (key === "ArrowLeft") return dx < 0 && Math.abs(dx) >= Math.abs(dy) * .35;
      if (key === "ArrowRight") return dx > 0 && Math.abs(dx) >= Math.abs(dy) * .35;
      if (key === "ArrowUp") return dy < 0 && Math.abs(dy) >= Math.abs(dx) * .35;
      return dy > 0 && Math.abs(dy) >= Math.abs(dx) * .35;
    });
    const target = directional.sort((a, b) => {
      const distanceA = Math.hypot(a.anchor.x - current.anchor.x, a.anchor.y - current.anchor.y);
      const distanceB = Math.hypot(b.anchor.x - current.anchor.x, b.anchor.y - current.anchor.y);
      return distanceA - distanceB || a.roomId.localeCompare(b.roomId);
    })[0];
    if (target !== undefined) document.getElementById(`station-room-${target.roomId}`)?.focus();
  }

  function handleRoomKey(event: KeyboardEvent<SVGGElement>, roomId: string) {
    if (["ArrowLeft", "ArrowRight", "ArrowUp", "ArrowDown"].includes(event.key)) {
      event.preventDefault();
      moveFocus(roomId, event.key);
    } else if (event.key === "Enter" || event.key === " ") {
      event.preventDefault();
      setSelectedRoomId(roomId);
    }
  }

  const showHazard = lens === "truth" || state.sensedConnections.includes("service-junction");
  const selected = roomById.get(selectedRoomId);

  return <div className={`station-map-frame${state.powerOnline ? " station-powered" : ""}${readable ? " station-readable" : ""}`}>
    <svg className="station-map" viewBox="0 0 1210 700" role="img" aria-labelledby="station-map-title" aria-describedby="station-map-description" data-visual-fixture={state.powerOnline ? "powered" : "unpowered"}>
      <title id="station-map-title">Cold Start station operations map</title>
      <desc id="station-map-description">Eleven function-shaped rooms connected by conduits, with agent, hazard, drone, power, and objective state.</desc>
      <defs>
        <pattern id="map-grid" width="32" height="32" patternUnits="userSpaceOnUse"><path d="M32 0H0V32" /></pattern>
        <pattern id="hazard-hatch" width="9" height="9" patternUnits="userSpaceOnUse" patternTransform="rotate(25)"><rect width="3" height="9" /></pattern>
        <radialGradient id="map-halo"><stop offset="0" stopColor="#123149" stopOpacity=".38"/><stop offset="1" stopColor="#03070e" stopOpacity="0"/></radialGradient>
      </defs>

      <g data-layer="10-field" aria-hidden="true"><rect className="map-field" width="1210" height="700"/><rect className="map-halo" width="1210" height="700"/><rect className="map-grid" width="1210" height="700"/></g>
      <g data-layer="20-structure" aria-label="Station conduits">
        {presentation.connections.map((connection) => connectionKnown(state, lens, connection) ? <g key={connection.connectionId} data-connection-id={connection.connectionId}>
          <polyline className="conduit-base" points={points(connection)} />
          <polyline className="conduit-rail" points={points(connection)} />
          <polyline className={`conduit-state${state.powerOnline ? " powered" : ""}${connection.kiteOnly ? " kite-only" : ""}`} points={points(connection)} />
        </g> : null)}
      </g>
      <g data-layer="30-network" aria-hidden="true">
        {state.powerOnline ? <><polyline className="power-pulse power-main" points="580,595 580,475 575,350"/><polyline className="power-pulse power-alpha" points="575,350 680,300 790,250"/><polyline className="power-pulse power-beta" points="575,350 680,405 790,455"/></> : null}
      </g>
      <g data-layer="40-rooms" aria-label="Station rooms">
        {presentation.rooms.map((room) => {
          const known = roomKnown(state, lens, room.roomId);
          const isSelected = selectedRoomId === room.roomId;
          return <g
            key={room.roomId}
            id={`station-room-${room.roomId}`}
            className={`station-room${known ? "" : " unknown"}${isSelected ? " selected" : ""}`}
            transform={`translate(${String(room.anchor.x)} ${String(room.anchor.y)})`}
            role="button"
            tabIndex={known ? 0 : -1}
            aria-label={roomStateLabel(room, state, known)}
            aria-pressed={isSelected}
            onClick={() => { if (known) setSelectedRoomId(room.roomId); }}
            onKeyDown={(event) => { handleRoomKey(event, room.roomId); }}
          >
            <TrustedRoomShape room={room} powered={state.powerOnline} open={room.roomId === "archive-threshold" && state.archiveOpen} />
            <text className="room-label" y={room.size.h / 2 + 18}>{known ? room.shortLabel : "UNKNOWN"}</text>
            <text className="room-id" y={room.size.h / 2 + 32}>{known ? room.roomId : "NO SIGNAL"}</text>
          </g>;
        })}
      </g>
      <g data-layer="50-hazards" aria-label="Threats">
        {showHazard ? <g className="radiation-marker" aria-label="Radiation confirmed on Service Spine to Relay Nexus"><path d="M463 457l9-9 9 9-9 9zM478 442l9-9 9 9-9 9zM493 427l9-9 9 9-9 9z"/><path className="hazard-bracket" d="M444 476v-22h24"/></g> : null}
        {(lens === "command" || lens === "truth" || state.discovered[lens].includes(state.droneRoomId)) ? <DroneToken room={roomById.get(state.droneRoomId)} /> : null}
      </g>
      <g data-layer="60-objectives" aria-label="Objectives">
        <ObjectiveRing room={roomById.get("auxiliary-power")} complete={state.objectives.power === "complete"} label="Power objective" />
        <ObjectiveRing room={roomById.get("console-alpha")} complete={state.activatedConsoles.includes("console-alpha")} label="Alpha console objective" />
        <ObjectiveRing room={roomById.get("console-beta")} complete={state.activatedConsoles.includes("console-beta")} label="Beta console objective" />
        {state.archiveOpen ? <g className="archive-shard" transform="translate(1110 350)" aria-label="Flight recorder available"><path d="M0-18l14 10v16L0 18l-14-10V-8z"/><path d="M-4-8h8v16h-8z"/></g> : null}
      </g>
      <g data-layer="70-agents" aria-label="Autonomous agents">
        {(lens === "command" || lens === "truth" || lens === "kite") ? <AgentToken agentId="kite" state={state} room={roomById.get(state.agents.kite.roomId)} /> : null}
        {(lens === "command" || lens === "truth" || lens === "wren") ? <AgentToken agentId="wren" state={state} room={roomById.get(state.agents.wren.roomId)} /> : null}
      </g>
      <g data-layer="80-events" aria-hidden="true" className={`event-${state.lastEvent ?? "idle"}`}>
        {state.lastEvent === "PowerRestored" ? <circle className="event-wave" cx="575" cy="350" r="70" /> : null}
        {state.lastEvent === "ArchiveOpened" ? <path className="sync-wave" d="M790 250Q900 300 975 350M790 455Q900 405 975 350"/> : null}
        {state.lastEvent === "MissionSucceeded" ? <path className="success-route" d="M1110 350H975H575Q330 300 105 355"/> : null}
      </g>
      <g data-layer="90-focus" aria-hidden="true">
        {selected === undefined ? null : <rect className="selection-reticle" x={selected.anchor.x - selected.size.w / 2 - 10} y={selected.anchor.y - selected.size.h / 2 - 10} width={selected.size.w + 20} height={selected.size.h + 20} />}
      </g>
    </svg>
    <p className="map-caption" aria-live="polite"><span>INSPECTING</span>{selected === undefined ? "No room" : roomStateLabel(selected, state, true)}</p>
  </div>;
}

function TrustedRoomShape({ room, powered, open }: { readonly room: RoomPresentation; readonly powered: boolean; readonly open: boolean }) {
  const w = room.size.w;
  const h = room.size.h;
  const roomClass = `room-shape${powered ? " powered" : ""}`;
  switch (room.shape) {
    case "docking-crescent": return <><path className={roomClass} d={`M${String(-w / 2 + 20)} ${String(-h / 2)}Q${String(w / 2)} ${String(-h / 2 - 4)} ${String(w / 2)} 0Q${String(w / 2)} ${String(h / 2 + 4)} ${String(-w / 2 + 20)} ${String(h / 2)}L${String(-w / 2)} ${String(h / 2 - 20)}L${String(-w / 2 + 14)} 0L${String(-w / 2)} ${String(-h / 2 + 20)}Z`} /><path className="room-inner" d={`M${String(-w / 2 + 24)} ${String(-h / 2 + 16)}Q${String(w / 2 - 18)} ${String(-h / 2 + 8)} ${String(w / 2 - 18)} 0Q${String(w / 2 - 18)} ${String(h / 2 - 8)} ${String(-w / 2 + 24)} ${String(h / 2 - 16)}`} /></>;
    case "transit-capsule": return <><path className={roomClass} d={`M${String(-w / 2 + 16)} ${String(-h / 2)}H${String(w / 2 - 16)}L${String(w / 2)} 0L${String(w / 2 - 16)} ${String(h / 2)}H${String(-w / 2 + 16)}L${String(-w / 2)} 0Z`} /><path className="room-inner" d={`M${String(-w / 2 + 18)} 0H${String(w / 2 - 18)}`} /></>;
    case "service-hex": return <><path className={roomClass} d={`M${String(-w / 2 + 20)} ${String(-h / 2)}H${String(w / 2 - 20)}L${String(w / 2)} ${String(-h / 2 + 20)}V${String(h / 2 - 20)}L${String(w / 2 - 20)} ${String(h / 2)}H${String(-w / 2 + 20)}L${String(-w / 2)} ${String(h / 2 - 20)}V${String(-h / 2 + 20)}Z`} /><path className="room-inner" d="M-24-22h48v44h-48z" /></>;
    case "service-spine": return <><path className={roomClass} d={`M${String(-w / 2)} -18L${String(-w / 2 + 28)} ${String(-h / 2)}H${String(w / 2 - 12)}L${String(w / 2)} 14L${String(w / 2 - 34)} ${String(h / 2)}H${String(-w / 2 + 10)}Z`} /><path className="room-inner" d="M-36 8L-10-14 34 4" /></>;
    case "relay-hub": return <><circle className={roomClass} r={w / 2}/><circle className="room-inner" r={w / 2 - 13}/><path className="relay-cross" d={`M${String(-w / 2 - 10)} 0H${String(w / 2 + 10)}M0 ${String(-h / 2 - 10)}V${String(h / 2 + 10)}`} /></>;
    case "radar-octagon": return <><path className={roomClass} d={`M-32 ${String(-h / 2)}H32L${String(w / 2)} -32V32L32 ${String(h / 2)}H-32L${String(-w / 2)} 32V-32Z`} /><circle className="room-inner radar-sweep" r="28"/><path className="room-inner" d="M0 0L22-22A32 32 0 0 1 30 12Z" /></>;
    case "reactor-ring": return <><path className={roomClass} fillRule="evenodd" d={`M0 ${String(-h / 2)}A${String(w / 2)} ${String(h / 2)} 0 1 1 -1 ${String(-h / 2)}ZM0-31A33 31 0 1 0 0 31A33 31 0 1 0 0-31Z`} /><path className="reactor-core" d="M0-23L18-7 10 18-14 20-22-2Z"/><path className="reactor-break" d="M-9-59L8-38" /></>;
    case "console-alpha": return <><path className={roomClass} d={`M${String(-w / 2)} -12L${String(-w / 2 + 25)} ${String(-h / 2)}L${String(w / 2 - 5)} ${String(-h / 2 + 8)}L${String(w / 2)} ${String(h / 2 - 18)}L${String(-w / 2 + 8)} ${String(h / 2)}Z`} /><path className="console-wedge" d="M-24-13H34L24 13H-34Z" /></>;
    case "console-beta": return <><path className={roomClass} d={`M${String(-w / 2 + 8)} ${String(-h / 2)}L${String(w / 2)} ${String(-h / 2 + 18)}L${String(w / 2 - 5)} ${String(h / 2 - 8)}L${String(-w / 2 + 25)} ${String(h / 2)}L${String(-w / 2)} 12Z`} /><path className="console-wedge" d="M-34-13H24L34 13H-24Z" /></>;
    case "archive-iris": return <><path className={`${roomClass}${open ? " open" : ""}`} d={`M-30 ${String(-h / 2)}H30L${String(w / 2)} ${String(-h / 2 + 14)}V${String(h / 2 - 14)}L30 ${String(h / 2)}H-30L${String(-w / 2)} ${String(h / 2 - 14)}V${String(-h / 2 + 14)}Z`} /><path className="iris" d={open ? "M-20-39L-7 0-20 39M20-39L7 0 20 39" : "M-16-39L0 0-16 39M16-39L0 0 16 39"} /></>;
    case "archive-shield": return <><path className={roomClass} d={`M${String(-w / 2 + 18)} ${String(-h / 2)}H${String(w / 2 - 10)}L${String(w / 2)} -12L${String(w / 2 - 17)} ${String(h / 2)}H${String(-w / 2 + 8)}L${String(-w / 2)} 8Z`} /><path className="room-inner" d="M-30-28H30V21L0 36-30 21Z" /></>;
  }
}

function AgentToken({ agentId, state, room }: { readonly agentId: AgentId; readonly state: PresentationState; readonly room: RoomPresentation | undefined }) {
  if (room === undefined) return null;
  const offset = agentId === "kite" ? -16 : 16;
  return <g className={`map-agent agent-${agentId}`} transform={`translate(${String(room.anchor.x + offset)} ${String(room.anchor.y - room.size.h / 2 - 18)})`} aria-label={`${agentId}, ${String(state.agents[agentId].health)} health${state.agents[agentId].carryingRecorder ? ", carrying recorder" : ""}`}>
    <circle r="18"/><path d={agentId === "kite" ? "M0-11L10 9 0 5-10 9Z" : "M-9-7L0-12 9-7 9 7 0 12-9 7Z"}/><text y="4">{agentId === "kite" ? "K" : "W"}</text>
    {state.agents[agentId].carryingRecorder ? <path className="carried-recorder" d="M-5 17h10l4 7-9 5-9-5z"/> : null}
  </g>;
}

function DroneToken({ room }: { readonly room: RoomPresentation | undefined }) {
  if (room === undefined) return null;
  return <g className="drone-token" transform={`translate(${String(room.anchor.x)} ${String(room.anchor.y - room.size.h / 2 - 18)})`} aria-label="Security drone"><circle className="drone-sweep" r="29"/><path d="M0-13L13 0 0 13-13 0Z"/><circle r="3"/></g>;
}

function ObjectiveRing({ room, complete, label }: { readonly room: RoomPresentation | undefined; readonly complete: boolean; readonly label: string }) {
  if (room === undefined) return null;
  return <circle className={`objective-ring${complete ? " complete" : ""}`} cx={room.anchor.x} cy={room.anchor.y} r={Math.max(room.size.w, room.size.h) / 2 + 9} aria-label={`${label}: ${complete ? "complete" : "available"}`} />;
}

export function AccessibleStationState({ presentation, state, lens }: Omit<StationMapProps, "readable">) {
  return <section className="accessible-station-state" aria-labelledby="station-state-heading">
    <h3 id="station-state-heading">Accessible station state</h3>
    <ul>{presentation.rooms.map((room) => <li key={room.roomId}><strong>{room.label}</strong><span>{roomStateLabel(room, state, roomKnown(state, lens, room.roomId))}</span></li>)}</ul>
  </section>;
}
