import { useState } from "react";
import { initialPresentationState, showcaseEvents, stationPresentation } from "../fixtures/stationShowcase";
import type { Lens, ObjectiveStep } from "./model";
import { AccessibleStationState, StationMap } from "./StationMap";
import { usePresentationPlayback } from "./usePresentationPlayback";

const lensLabels: Readonly<Record<Lens, string>> = { command: "Command", kite: "Kite", wren: "Wren", truth: "Truth" };
const objectiveLabels: Readonly<Record<ObjectiveStep, string>> = {
  power: "Restore auxiliary power",
  sync: "Synchronize consoles",
  recorder: "Recover flight recorder",
  extract: "Extract full team",
};

export function RunScreen({ onReturn }: { readonly onReturn: () => void }) {
  const playback = usePresentationPlayback(initialPresentationState, showcaseEvents);
  const [lens, setLens] = useState<Lens>("command");
  const [readable, setReadable] = useState(false);
  const [contextOpen, setContextOpen] = useState(false);
  const truthAvailable = playback.state.missionStatus !== "running";
  const currentAgent = lens === "kite" || lens === "wren" ? lens : "kite";
  const latestEvents = playback.state.eventLog.slice(-4).reverse();

  return <div className={`run-shell${readable ? " readable-mode" : ""}`}>
    <header className="run-topbar">
      <button className="run-brand" type="button" aria-label="Return to briefing workbench" onClick={onReturn}><span>DD</span><strong>Directive Drift</strong><small>Return to build</small></button>
      <div><p>Cold Start / Showcase replay</p><strong>Station operations</strong></div>
      <div className="turn-readout"><span>Turn</span><strong>{String(playback.state.turn).padStart(2, "0")}</strong><small>/ 18</small></div>
    </header>

    <main className="operations-layout">
      <aside className="command-rail" aria-label="Command build">
        <p className="eyebrow">Command build</p><h2>Split Lantern</h2><span className="rail-version">FIXTURE / V1</span>
        <section><h3>Shared doctrine</h3><p>Protect survival and extraction. Announce commitments and intended sync turns early.</p></section>
        <AgentRail agentId="kite" health={playback.state.agents.kite.health} room={playback.state.agents.kite.roomId} />
        <AgentRail agentId="wren" health={playback.state.agents.wren.health} room={playback.state.agents.wren.roomId} />
        <section className="topology-key"><h3>Information topology</h3><p><i className="key-kite"/> Kite only · 2</p><p><i className="key-wren"/> Wren only · 2</p><p><i className="key-shared"/> Shared · 2</p></section>
      </aside>

      <section className="map-stage" aria-labelledby="map-stage-title">
        <header><div><p className="eyebrow">Orison local / deck 04</p><h1 id="map-stage-title">Station operations map</h1></div><p>Topology 11:12 <span>Lens / {lensLabels[lens]}</span></p><button className="context-toggle" type="button" aria-expanded={contextOpen} aria-controls="replay-context" onClick={() => { setContextOpen((value) => !value); }}>Context</button></header>
        <StationMap presentation={stationPresentation} state={playback.state} lens={lens} readable={readable} />
      </section>

      <aside id="replay-context" className={`context-rail${contextOpen ? " context-open" : ""}`} aria-label="Replay context">
        <p className="eyebrow">Replay context</p>
        <section className={`selected-agent selected-${currentAgent}`}><span className="context-agent-token">{currentAgent === "kite" ? "K" : "W"}</span><div><small>Selected agent</small><strong>{currentAgent}</strong></div><b>{String(playback.state.agents[currentAgent].health)} HP</b></section>
        <fieldset className="lens-switcher"><legend>Information lens</legend>{(Object.keys(lensLabels) as Lens[]).map((value) => <button key={value} type="button" disabled={value === "truth" && !truthAvailable} aria-pressed={lens === value} onClick={() => { setLens(value); }}>{lensLabels[value]}{value === "truth" && !truthAvailable ? <small>post-run</small> : null}</button>)}</fieldset>
        <section className="objective-contracts"><h3>Objective contracts</h3><ol>{(Object.keys(objectiveLabels) as ObjectiveStep[]).map((objective) => <li key={objective} className={playback.state.objectives[objective]}><i/>{objectiveLabels[objective]}</li>)}</ol></section>
        <section className="event-readout" aria-live="polite"><h3>Canonical events</h3>{latestEvents.length === 0 ? <p>Awaiting playback.</p> : <ol>{latestEvents.map((event, index) => <li key={`${event}-${String(index)}`}>{event}</li>)}</ol>}</section>
      </aside>

      <footer className="playback-dock">
        <div><p className="eyebrow">Turn trace</p><div className="trace-line" aria-label={`${String(playback.cursor)} of ${String(showcaseEvents.length)} canonical events resolved`}><span style={{ width: `${String(playback.cursor / showcaseEvents.length * 100)}%` }}/>{showcaseEvents.map((event) => <i key={event.sequence} className={event.sequence <= playback.cursor ? "resolved" : ""} />)}</div></div>
        <div className="playback-controls" aria-label="Playback controls">
          <button type="button" onClick={playback.toggle} aria-pressed={playback.playing}>{playback.playing ? "Pause" : "Play"}</button>
          <button type="button" onClick={() => { playback.setSpeed(playback.speed === 1 ? 2 : 1); }} aria-label={`Playback speed ${String(playback.speed)} times`}>{playback.speed}×</button>
          <button type="button" onClick={playback.resolveInstantly}>Resolve instantly</button>
          <button type="button" onClick={playback.reset}>Reset</button>
          <button type="button" aria-pressed={readable} onClick={() => { setReadable((value) => !value); }}>Readable</button>
        </div>
      </footer>
    </main>
    <AccessibleStationState presentation={stationPresentation} state={playback.state} lens={lens} />
  </div>;
}

function AgentRail({ agentId, health, room }: { readonly agentId: "kite" | "wren"; readonly health: number; readonly room: string }) {
  return <section className={`rail-agent rail-${agentId}`}><header><span>{agentId === "kite" ? "K" : "W"}</span><div><h3>{agentId}</h3><p>{agentId === "kite" ? "Recon / courier" : "Engineer / anchor"}</p></div></header><dl><div><dt>Location</dt><dd>{room.replaceAll("-", " ")}</dd></div><div><dt>Health</dt><dd>{String(health)} / 3</dd></div></dl></section>;
}
