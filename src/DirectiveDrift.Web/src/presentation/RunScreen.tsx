import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  addBuildVersion,
  bootstrapGuest,
  createBuild,
  enqueueTurn,
  getEvents,
  getOperation,
  getReplay,
  getRun,
  startRun,
} from "../api/gameApi";
import type { ApiEvent, ApiRun } from "../api/gameApi";
import { initialPresentationState, stationPresentation } from "../fixtures/stationShowcase";
import { initialDraft, toBuildDocument } from "../workbench/buildModel";
import type { BuildDocument } from "../workbench/buildModel";
import { AccessibleStationState, StationMap } from "./StationMap";
import { diagnosticSignals, toPresentationEvent } from "./apiEventAdapter";
import type { Lens, ObjectiveStep } from "./model";
import { usePresentationPlayback } from "./usePresentationPlayback";

const lensLabels: Readonly<Record<Lens, string>> = { command: "Command", kite: "Kite", wren: "Wren", truth: "Truth" };
const objectiveLabels: Readonly<Record<ObjectiveStep, string>> = {
  power: "Restore auxiliary power", sync: "Synchronize consoles", recorder: "Recover flight recorder", extract: "Extract full team",
};
const activeRunKey = "directive-drift:p7-active-run";
const activeBuildKey = "directive-drift:p7-active-build";
const buildKey = "directive-drift:p7-build-version";

type SavedRun = { readonly runId: string; readonly operationId?: string };
type SavedBuild = { readonly buildId: string; readonly version: number; readonly hasSync: boolean };

function wait(milliseconds: number) {
  return new Promise((resolve) => window.setTimeout(resolve, milliseconds));
}

function readStored(key: string): unknown {
  const raw = window.localStorage.getItem(key);
  if (raw === null) return null;
  try { return JSON.parse(raw) as unknown; } catch { return null; }
}

export function RunScreen({ build = toBuildDocument(initialDraft), variantId = "cs-practice-01", onReturn, onRevise = () => undefined }: {
  readonly build?: BuildDocument;
  readonly variantId?: string;
  readonly onReturn: () => void;
  readonly onRevise?: () => void;
}) {
  const activeBuild = readStored(activeBuildKey) as BuildDocument | null;
  const displayedBuild = activeBuild ?? build;
  const [run, setRun] = useState<ApiRun | null>(null);
  const [events, setEvents] = useState<readonly ApiEvent[]>([]);
  const [phase, setPhase] = useState<"ready" | "starting" | "running" | "paused" | "complete" | "error">("ready");
  const [progress, setProgress] = useState("Ready to start the credential-free scripted run.");
  const [error, setError] = useState<string | null>(null);
  const [lens, setLens] = useState<Lens>("command");
  const [readable, setReadable] = useState(false);
  const [contextOpen, setContextOpen] = useState(false);
  const processing = useRef(false);
  const pauseRequested = useRef(false);
  const mounted = useRef(true);
  const presentationEvents = useMemo(() => events.map(toPresentationEvent).filter((event) => event !== null), [events]);
  const playback = usePresentationPlayback(initialPresentationState, presentationEvents);
  const diagnostics = useMemo(() => diagnosticSignals(events), [events]);
  const terminal = run !== null && run.status !== 0;
  const truthAvailable = terminal;
  const currentAgent = lens === "kite" || lens === "wren" ? lens : "kite";
  const latestEvents = playback.state.eventLog.slice(-4).reverse();
  const scoreEvent = presentationEvents.findLast((event) => event.type === "MissionSucceeded");
  const score = scoreEvent?.type === "MissionSucceeded" ? scoreEvent.payload.score : null;

  const pageEvents = useCallback(async (runId: string) => {
    let after = -1;
    const collected: ApiEvent[] = [];
    for (;;) {
      const page = await getEvents(runId, after, 40);
      collected.push(...page);
      if (page.length < 40) break;
      after = page.at(-1)?.sequence ?? after;
    }
    if (mounted.current) setEvents(collected);
  }, []);

  const drive = useCallback(async (saved: SavedRun, stopAfterOne = false) => {
    if (processing.current) return;
    processing.current = true;
    setPhase("running");
    setError(null);
    try {
      let operationId = saved.operationId;
      let completedOperations = 0;
      while (mounted.current) {
        if (operationId !== undefined) {
          setProgress("Operation in progress — polling durable queue.");
          const operation = await getOperation(operationId);
          if (operation.status === 3 || operation.status === 4) throw new Error(operation.errorCode ?? "The turn operation stopped safely.");
          if (operation.status < 2) { await wait(80); continue; }
          operationId = undefined;
          completedOperations++;
          window.localStorage.setItem(activeRunKey, JSON.stringify({ runId: saved.runId }));
          await pageEvents(saved.runId);
        }

        const current = await getRun(saved.runId);
        setRun(current);
        if (current.status !== 0) {
          setProgress("Run complete — replay loaded without requesting another decision.");
          const replay = await getReplay(saved.runId);
          setEvents(replay.events);
          setRun(replay.run);
          setPhase("complete");
          return;
        }
        if ((stopAfterOne && completedOperations > 0) || pauseRequested.current) {
          pauseRequested.current = false;
          setProgress(`Turn ${String(current.turn)} committed. Advance again or resume autoplay.`);
          setPhase("paused");
          return;
        }

        setProgress(`Advancing scripted turn ${String(current.turn + 1)} of 18.`);
        const accepted = await enqueueTurn(saved.runId, current.turn + 1);
        operationId = accepted.operationId;
        window.localStorage.setItem(activeRunKey, JSON.stringify({ runId: saved.runId, operationId }));
      }
    } catch (caught) {
      if (mounted.current) {
        setError(caught instanceof Error ? caught.message : "The run could not continue.");
        setPhase("error");
        setProgress("Progress is preserved. Retry resumes the same operation.");
      }
    } finally {
      processing.current = false;
    }
  }, [pageEvents]);

  useEffect(() => {
    mounted.current = true;
    const saved = readStored(activeRunKey) as SavedRun | null;
    if (saved !== null) void drive(saved);
    return () => { mounted.current = false; };
  }, [drive]);

  async function launch() {
    setPhase("starting");
    setProgress("Bootstrapping guest session and locking build version.");
    setError(null);
    try {
      await bootstrapGuest();
      const hasSync = build.agents["wren"]?.briefingCardIds.includes("sync-contract") ?? false;
      const stored = readStored(buildKey) as SavedBuild | null;
      let submitted: BuildDocument;
      if (stored === null) {
        const suffix = crypto.randomUUID().replaceAll("-", "").slice(0, 10);
        submitted = { ...build, buildId: `tutorial-${suffix}`, version: 1 };
        await createBuild(submitted);
      } else if (stored.hasSync !== hasSync) {
        submitted = { ...build, buildId: stored.buildId, version: stored.version + 1 };
        await addBuildVersion(submitted);
      } else {
        submitted = { ...build, buildId: stored.buildId, version: stored.version };
      }
      window.localStorage.setItem(buildKey, JSON.stringify({ buildId: submitted.buildId, version: submitted.version, hasSync } satisfies SavedBuild));
      window.localStorage.setItem(activeBuildKey, JSON.stringify(submitted));
      const created = await startRun(submitted.buildId, submitted.version, variantId);
      setRun(created);
      const saved = { runId: created.runId.value };
      window.localStorage.setItem(activeRunKey, JSON.stringify(saved));
      await drive(saved);
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "The run could not start.");
      setPhase("error");
    }
  }

  function revise() {
    window.localStorage.removeItem(activeRunKey);
    window.localStorage.removeItem(activeBuildKey);
    onRevise();
  }

  return <div className={`run-shell${readable ? " readable-mode" : ""}`}>
    <header className="run-topbar">
      <button className="run-brand" type="button" aria-label="Return to briefing workbench" onClick={onReturn}><span>DD</span><strong>Directive Drift</strong><small>Return to build</small></button>
      <div><p>Cold Start / Scripted onboarding</p><strong>{phase === "complete" ? "Truth replay" : "Station operations"}</strong></div>
      <div className="turn-readout"><span>Turn</span><strong>{String(run?.turn ?? 0).padStart(2, "0")}</strong><small>/ 18</small></div>
    </header>

    <section className={`operation-banner operation-${phase}`} role="status" aria-live="polite">
      <div><strong>{phase === "complete" ? (run?.status === 1 ? "Mission succeeded" : "Mission failed") : "Scripted operation"}</strong><span>{progress}</span></div>
      {phase === "ready" ? <button type="button" onClick={() => { void launch(); }}>Start scripted run</button> : null}
      {phase === "running" ? <button type="button" onClick={() => { pauseRequested.current = true; }}>Pause autoplay</button> : null}
      {phase === "paused" ? <><button type="button" onClick={() => { const saved = readStored(activeRunKey) as SavedRun; void drive(saved, true); }}>Advance one turn</button><button type="button" onClick={() => { const saved = readStored(activeRunKey) as SavedRun; void drive(saved); }}>Resume autoplay</button></> : null}
      {phase === "error" ? <button type="button" onClick={() => { const saved = readStored(activeRunKey) as SavedRun | null; if (saved === null) void launch(); else void drive(saved); }}>Retry safely</button> : null}
      {error === null ? null : <p>{error}</p>}
    </section>

    <main className="operations-layout">
      <aside className="command-rail" aria-label="Command build">
        <p className="eyebrow">Command build</p><h2>{displayedBuild.name}</h2><span className="rail-version">LOCKED / V{String(run?.buildVersion ?? displayedBuild.version)}</span>
        <section><h3>Shared doctrine</h3><p>{displayedBuild.sharedDoctrine}</p></section>
        <AgentRail agentId="kite" health={playback.state.agents.kite.health} room={playback.state.agents.kite.roomId} />
        <AgentRail agentId="wren" health={playback.state.agents.wren.health} room={playback.state.agents.wren.roomId} />
        {terminal ? <><section className="run-summary"><h3>Run summary</h3><p>Result <strong>{run.status === 1 ? "Success" : "Failure"}</strong></p><p>Score <strong>{score ?? "—"}</strong></p><p>State hash <code>{run.stateHash.slice(0, 12)}</code></p></section><MasteryActions runId={run.runId.value} assisted={run.assisted === true} /></> : null}
      </aside>

      <section className="map-stage" aria-labelledby="map-stage-title">
        <header><div><p className="eyebrow">Orison local / deck 04</p><h1 id="map-stage-title">Station operations map</h1></div><p>Events {String(events.length)} <span>Lens / {lensLabels[lens]}</span></p><button className="context-toggle" type="button" aria-expanded={contextOpen} aria-controls="replay-context" onClick={() => { setContextOpen((value) => !value); }}>Context</button></header>
        <StationMap presentation={stationPresentation} state={playback.state} lens={lens} readable={readable} />
      </section>

      <aside id="replay-context" className={`context-rail${contextOpen ? " context-open" : ""}`} aria-label="Replay context">
        <p className="eyebrow">Replay context</p>
        <section className={`selected-agent selected-${currentAgent}`}><span className="context-agent-token">{currentAgent === "kite" ? "K" : "W"}</span><div><small>Selected agent</small><strong>{currentAgent}</strong></div><b>{String(playback.state.agents[currentAgent].health)} HP</b></section>
        <fieldset className="lens-switcher"><legend>Information lens</legend>{(Object.keys(lensLabels) as Lens[]).map((value) => <button key={value} type="button" disabled={value === "truth" && !truthAvailable} aria-pressed={lens === value} onClick={() => { setLens(value); }}>{lensLabels[value]}{value === "truth" && !truthAvailable ? <small>post-run</small> : null}</button>)}</fieldset>
        <section className="objective-contracts"><h3>Objective contracts</h3><ol>{(Object.keys(objectiveLabels) as ObjectiveStep[]).map((objective) => <li key={objective} className={playback.state.objectives[objective]}><i/>{objectiveLabels[objective]}</li>)}</ol></section>
        {terminal ? <section className="diagnostic-signals"><h3>Diagnostic signals</h3>{diagnostics.length === 0 ? <p>No contract divergence detected.</p> : <ul>{diagnostics.map((signal) => <li key={signal}>{signal}</li>)}</ul>}{run.status === 2 ? <button type="button" onClick={revise}>Apply guided sync revision</button> : null}</section> : null}
        <section className="event-readout" aria-live="polite"><h3>Canonical events</h3>{latestEvents.length === 0 ? <p>Awaiting event playback.</p> : <ol>{latestEvents.map((event, index) => <li key={`${event}-${String(index)}`}>{event}</li>)}</ol>}</section>
      </aside>

      <footer className="playback-dock">
        <div><p className="eyebrow">Canonical event queue</p><div className="trace-line" aria-label={`${String(playback.cursor)} of ${String(presentationEvents.length)} canonical events resolved`}><span style={{ width: presentationEvents.length === 0 ? "0%" : `${String(playback.cursor / presentationEvents.length * 100)}%` }}/>{presentationEvents.map((event) => <i key={event.sequence} className={event.sequence <= (presentationEvents[playback.cursor - 1]?.sequence ?? -1) ? "resolved" : ""} />)}</div></div>
        <div className="playback-controls" aria-label="Playback controls">
          <button type="button" disabled={presentationEvents.length === 0} onClick={playback.toggle} aria-pressed={playback.playing}>{playback.playing ? "Pause" : "Play replay"}</button>
          <button type="button" onClick={() => { playback.setSpeed(playback.speed === 1 ? 2 : 1); }}>{playback.speed}×</button>
          <button type="button" disabled={presentationEvents.length === 0} onClick={playback.resolveInstantly}>Resolve instantly</button>
          <button type="button" disabled={presentationEvents.length === 0} onClick={playback.reset}>Replay from start</button>
          <button type="button" aria-pressed={readable} onClick={() => { setReadable((value) => !value); }}>Readable</button>
        </div>
      </footer>
    </main>
    <AccessibleStationState presentation={stationPresentation} state={playback.state} lens={lens} />
  </div>;
}

function MasteryActions({ runId, assisted }: { readonly runId: string; readonly assisted: boolean }) {
  return <section className="mastery-actions" aria-labelledby="mastery-title">
    <p className="eyebrow">Mastery loop</p><h3 id="mastery-title">Revise, compare, certify</h3>
    {assisted ? <p className="mastery-warning">Emergency Burst used · this run cannot count toward certification or comparison.</p> : <p>Successful runs on three distinct practice variants unlock a hidden three-run certification. The exact build and official profile lock for all three.</p>}
    <a href={`/api/v1/runs/${runId}/share-card.svg`} target="_blank" rel="noreferrer">Open safe share card</a>
    <small>Share output excludes role text, messages, provider details, and unrevealed certification truth.</small>
  </section>;
}

function AgentRail({ agentId, health, room }: { readonly agentId: "kite" | "wren"; readonly health: number; readonly room: string }) {
  return <section className={`rail-agent rail-${agentId}`}><header><span>{agentId === "kite" ? "K" : "W"}</span><div><h3>{agentId}</h3><p>{agentId === "kite" ? "Recon / courier" : "Engineer / anchor"}</p></div></header><dl><div><dt>Location</dt><dd>{room.replaceAll("-", " ")}</dd></div><div><dt>Health</dt><dd>{String(health)} / 3</dd></div></dl></section>;
}
