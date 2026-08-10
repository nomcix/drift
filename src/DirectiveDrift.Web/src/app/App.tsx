import { useMemo, useState } from "react";
import type { ChangeEvent } from "react";
import { coldStartFixture } from "../fixtures/coldStart";
import type { BriefingCardFixture } from "../fixtures/coldStart";
import {
  initialDraft,
  onboardingFailureDraft,
  applyGuidedSyncRevision,
  knowledgeSummary,
  toBuildDocument,
  validateDraft,
} from "../workbench/buildModel";
import type { AgentBuild, BuildDocument, BuildDraft } from "../workbench/buildModel";
import { RunScreen } from "../presentation/RunScreen";

const savedBuildKey = "directive-drift:p5-fixture-build";

type SaveBuild = (build: BuildDocument) => void;

function saveFixtureBuild(build: BuildDocument) {
  window.localStorage.setItem(savedBuildKey, JSON.stringify(build));
}

export function App({ onSave = saveFixtureBuild }: { readonly onSave?: SaveBuild }) {
  const [screen, setScreen] = useState<"workbench" | "run">(
    () => window.localStorage.getItem("directive-drift:p7-active-run") === null ? "workbench" : "run",
  );
  const [draft, setDraft] = useState<BuildDraft>(initialDraft);
  const [savedBuild, setSavedBuild] = useState<BuildDocument | null>(null);
  const [practiceVariant, setPracticeVariant] = useState("cs-practice-01");
  const summary = useMemo(() => knowledgeSummary(draft), [draft]);
  const errors = useMemo(() => validateDraft(draft), [draft]);

  if (screen === "run") return <RunScreen
    build={savedBuild ?? toBuildDocument(draft)}
    variantId={practiceVariant}
    onReturn={() => { setScreen("workbench"); }}
    onRevise={() => { setDraft((current) => applyGuidedSyncRevision(current)); setSavedBuild(null); setScreen("workbench"); }}
  />;

  function updateAgent(agentId: string, update: (agent: AgentBuild) => AgentBuild) {
    setDraft((current) => {
      const agent = current.agents[agentId];
      if (agent === undefined) return current;
      return { ...current, agents: { ...current.agents, [agentId]: update(agent) } };
    });
    setSavedBuild(null);
  }

  function assignCard(agentId: string, cardId: string) {
    updateAgent(agentId, (agent) => {
      if (agent.briefingCardIds.includes(cardId) || agent.briefingCardIds.length >= 4) return agent;
      return { ...agent, briefingCardIds: [...agent.briefingCardIds, cardId] };
    });
  }

  function removeCard(agentId: string, cardId: string) {
    updateAgent(agentId, (agent) => ({
      ...agent,
      briefingCardIds: agent.briefingCardIds.filter((value) => value !== cardId),
    }));
  }

  function reorderCard(agentId: string, index: number, offset: -1 | 1) {
    updateAgent(agentId, (agent) => {
      const target = index + offset;
      if (target < 0 || target >= agent.briefingCardIds.length) return agent;
      const cards = [...agent.briefingCardIds];
      const current = cards[index];
      const adjacent = cards[target];
      if (current === undefined || adjacent === undefined) return agent;
      cards[index] = adjacent;
      cards[target] = current;
      return { ...agent, briefingCardIds: cards };
    });
  }

  function duplicateCard(fromAgentId: string, cardId: string) {
    const other = coldStartFixture.agents.find(({ agentId }) => agentId !== fromAgentId);
    if (other !== undefined) assignCard(other.agentId, cardId);
  }

  function submitBuild() {
    if (errors.length > 0) return;
    const build = toBuildDocument(draft);
    onSave(build);
    setSavedBuild(build);
  }

  return (
    <div className="app-shell">
      <header className="topbar">
        <a className="brand" href="#briefing" aria-label="Directive Drift briefing workbench">
          <span className="brand-mark" aria-hidden="true">DD</span>
          <span><strong>Directive Drift</strong><small>Command architecture lab</small></span>
        </a>
        <nav aria-label="Build workflow">
          <a href="#briefing">01 Mission</a>
          <a href="#workbench" aria-current="step">02 Briefing</a>
          <a href="#prediction">03 Predict</a>
          <button type="button" onClick={() => { setScreen("run"); }}>04 Execute</button>
        </nav>
        <div className="build-version"><span>Build state</span><strong>Draft · v1</strong></div>
      </header>

      <main>
        <section className="mission-brief" id="briefing" aria-labelledby="mission-title">
          <div className="mission-copy">
            <p className="eyebrow">Practice mission / {coldStartFixture.contentVersion}</p>
            <h1 id="mission-title">{coldStartFixture.title}</h1>
            <p>{coldStartFixture.brief}</p>
            <div className="mission-facts" aria-label="Mission limits">
              <span><strong>18</strong> turns</span><span><strong>06</strong> messages</span><span><strong>02</strong> agents</span>
            </div>
          </div>
          <ObjectiveTree />
          <div className="station-ghost" aria-hidden="true">
            <svg viewBox="0 0 460 180" role="presentation">
              <path d="M34 92 C95 92 86 42 151 42 S215 93 260 93 S328 35 422 48" />
              <path d="M151 42 C177 90 170 142 234 142 S318 110 395 139" />
              <circle cx="34" cy="92" r="15" /><circle cx="151" cy="42" r="22" />
              <circle cx="260" cy="93" r="27" /><circle cx="422" cy="48" r="18" />
              <circle cx="234" cy="142" r="19" /><circle cx="395" cy="139" r="23" />
            </svg>
          </div>
          <aside className="onboarding-callout" aria-label="Scripted onboarding">
            <strong>First run: expose the knowledge gap</strong>
            <span>Load a valid generic build that omits Wren's sync contract, then diagnose and revise it.</span>
            <button type="button" onClick={() => { setDraft(onboardingFailureDraft); setSavedBuild(null); }}>Load scripted failure</button>
          </aside>
          <label className="practice-selector">Practice variant
            <select value={practiceVariant} onChange={(event) => { setPracticeVariant(event.target.value); }}>
              <option value="cs-practice-01">Split Warning · revealed</option>
              <option value="cs-practice-02">Second Repair · revealed</option>
              <option value="cs-practice-03">Rotated Watch · revealed</option>
              <option value="cs-practice-04">Broken Intake · revealed</option>
              <option value="cs-practice-05">Tight Window · revealed</option>
              <option value="cs-practice-random">Safe random · seed and mutations reveal at start</option>
            </select>
          </label>
        </section>

        <section className="workbench" id="workbench" aria-labelledby="workbench-title">
          <header className="section-heading">
            <div><p className="eyebrow">Briefing workbench</p><h2 id="workbench-title">Design what each agent knows</h2></div>
            <p>Four ordered facts and one module per agent. Shared cards consume one slot on each side.</p>
          </header>

          <section className="doctrine-strip" aria-labelledby="doctrine-label">
            <FieldLabel id="doctrine-label" title="Shared doctrine" detail="Both agents receive this every turn" />
            <textarea
              aria-labelledby="doctrine-label"
              maxLength={240}
              value={draft.sharedDoctrine}
              onChange={(event) => { setDraft({ ...draft, sharedDoctrine: event.target.value }); setSavedBuild(null); }}
            />
            <CharacterCount value={draft.sharedDoctrine} maximum={240} />
          </section>

          <div className="team-grid">
            {coldStartFixture.agents.map((agent, index) => {
              const agentBuild = draft.agents[agent.agentId];
              if (agentBuild === undefined) return null;
              const otherModule = draft.agents[coldStartFixture.agents[index === 0 ? 1 : 0]?.agentId ?? ""]?.moduleId;
              return (
                <AgentPanel
                  key={agent.agentId}
                  agent={agent}
                  build={agentBuild}
                  otherModule={otherModule}
                  onRoleChange={(roleOrder) => { updateAgent(agent.agentId, (current) => ({ ...current, roleOrder })); }}
                  onModuleChange={(moduleId) => { updateAgent(agent.agentId, (current) => ({ ...current, moduleId })); }}
                  onRemove={(cardId) => { removeCard(agent.agentId, cardId); }}
                  onReorder={(cardIndex, offset) => { reorderCard(agent.agentId, cardIndex, offset); }}
                  onDuplicate={(cardId) => { duplicateCard(agent.agentId, cardId); }}
                />
              );
            })}
          </div>

          <KnowledgeMeter summary={summary} />
          <CardCatalogue draft={draft} onAssign={assignCard} />

          <section className="prediction-panel" id="prediction" aria-labelledby="prediction-label">
            <div>
              <FieldLabel id="prediction-label" title="Commit your prediction" detail="Never enters agent context" />
              <p>What will the team do, and where might intent drift?</p>
            </div>
            <textarea
              aria-labelledby="prediction-label"
              maxLength={280}
              value={draft.hypothesis}
              onChange={(event) => { setDraft({ ...draft, hypothesis: event.target.value }); setSavedBuild(null); }}
            />
            <CharacterCount value={draft.hypothesis} maximum={280} />
          </section>

          <footer className="save-dock">
            <label className="build-name">Build name
              <input
                value={draft.name}
                maxLength={48}
                onChange={(event) => { setDraft({ ...draft, name: event.target.value }); setSavedBuild(null); }}
              />
            </label>
            <div className="validation-state" role="status" aria-live="polite">
              {errors.length === 0 ? <><strong>Ready to save</strong><span>Roster and slot contract valid</span></> : <><strong>{errors.length} issue{errors.length === 1 ? "" : "s"}</strong><span>{errors[0]}</span></>}
            </div>
            <button className="save-button" type="button" disabled={errors.length > 0} onClick={submitBuild}>Save build <span aria-hidden="true">→</span></button>
            <button className="run-preview-button" type="button" onClick={() => { setScreen("run"); }}>Execute scripted run</button>
          </footer>
          {savedBuild === null ? null : <p className="save-confirmation" role="status">Saved <strong>{savedBuild.name}</strong> as schema v{savedBuild.schemaVersion} fixture build <code>{savedBuild.buildId}</code>.</p>}
        </section>
      </main>
    </div>
  );
}

function ObjectiveTree() {
  return <section className="objective-tree" aria-labelledby="objectives-title"><p className="eyebrow" id="objectives-title">Objective chain</p><ol>{coldStartFixture.objectives.map((objective, index) => <li key={objective.code}><span>{objective.code}</span><div><strong>{objective.title}</strong><small>{objective.detail}</small></div>{index < coldStartFixture.objectives.length - 1 ? <i aria-hidden="true" /> : null}</li>)}</ol></section>;
}

function AgentPanel({ agent, build, otherModule, onRoleChange, onModuleChange, onRemove, onReorder, onDuplicate }: {
  readonly agent: (typeof coldStartFixture.agents)[number];
  readonly build: AgentBuild;
  readonly otherModule: string | undefined;
  readonly onRoleChange: (value: string) => void;
  readonly onModuleChange: (value: string) => void;
  readonly onRemove: (cardId: string) => void;
  readonly onReorder: (index: number, offset: -1 | 1) => void;
  readonly onDuplicate: (cardId: string) => void;
}) {
  return <article className={`agent-panel agent-${agent.agentId}`} aria-labelledby={`${agent.agentId}-title`}>
    <header><span className="agent-token" aria-hidden="true">{agent.callSign}</span><div><p className="eyebrow">Autonomous field unit</p><h3 id={`${agent.agentId}-title`}>{agent.label}</h3><small>Starts / {agent.start}</small></div></header>
    <ul className="capability-list" aria-label={`${agent.label} capabilities`}>{agent.capabilities.map((capability) => <li key={capability}>{capability}</li>)}</ul>
    <label className="field-label" htmlFor={`${agent.agentId}-role`}><strong>Private role order</strong><span>Visible only to {agent.label}</span></label>
    <textarea id={`${agent.agentId}-role`} maxLength={160} value={build.roleOrder} onChange={(event) => { onRoleChange(event.target.value); }} />
    <CharacterCount value={build.roleOrder} maximum={160} />
    <fieldset className="loadout"><legend>Briefing loadout <span>{build.briefingCardIds.length} / 4</span></legend>
      <ol>{Array.from({ length: 4 }, (_, slot) => {
        const cardId = build.briefingCardIds[slot];
        const card = coldStartFixture.briefingCards.find((candidate) => candidate.cardId === cardId);
        return <li key={cardId ?? `empty-${String(slot)}`} className={card === undefined ? "empty-slot" : "filled-slot"}><span className="slot-number">{String(slot + 1).padStart(2, "0")}</span>{card === undefined ? <span>Empty briefing slot</span> : <><div><strong>{card.title}</strong><small>{card.category}{card.requiredContract ? " · required" : ""}</small></div><div className="slot-actions"><button type="button" disabled={slot === 0} onClick={() => { onReorder(slot, -1); }} aria-label={`Move ${card.title} earlier for ${agent.label}`}>↑</button><button type="button" disabled={slot === build.briefingCardIds.length - 1} onClick={() => { onReorder(slot, 1); }} aria-label={`Move ${card.title} later for ${agent.label}`}>↓</button><button type="button" onClick={() => { onDuplicate(card.cardId); }} aria-label={`Share ${card.title} with the other agent`}>⇄</button><button type="button" onClick={() => { onRemove(card.cardId); }} aria-label={`Remove ${card.title} from ${agent.label}`}>×</button></div></>}</li>;
      })}</ol>
    </fieldset>
    <label className="module-field">Support module<select aria-label={`${agent.label} support module`} value={build.moduleId} onChange={(event: ChangeEvent<HTMLSelectElement>) => { onModuleChange(event.target.value); }}>{coldStartFixture.modules.map((module) => <option key={module.moduleId} value={module.moduleId} disabled={module.moduleId === otherModule}>{module.label}{module.moduleId === otherModule ? " · assigned" : ""}</option>)}</select></label>
    <p className="module-description">{coldStartFixture.modules.find(({ moduleId }) => moduleId === build.moduleId)?.description}</p>
  </article>;
}

function KnowledgeMeter({ summary }: { readonly summary: ReturnType<typeof knowledgeSummary> }) {
  return <section className="knowledge-meter" aria-labelledby="overlap-title"><div><p className="eyebrow" id="overlap-title">Information overlap</p><p>Descriptive only — no allocation is graded.</p></div><dl><div><dt>Specialized</dt><dd>{summary.specialized}</dd></div><div><dt>Shared</dt><dd>{summary.shared}</dd></div><div><dt>Omitted</dt><dd>{summary.omitted}</dd></div></dl>{summary.requiredOmissions.length === 0 ? <p className="contract-clear"><span aria-hidden="true">✓</span> Every required contract reaches at least one agent</p> : <p className="contract-warning"><span aria-hidden="true">!</span> Required but unassigned: {summary.requiredOmissions.join(", ")}. Saving is still allowed.</p>}</section>;
}

function CardCatalogue({ draft, onAssign }: { readonly draft: BuildDraft; readonly onAssign: (agentId: string, cardId: string) => void }) {
  return <section className="catalogue" aria-labelledby="catalogue-title"><header><div><p className="eyebrow">Mission intelligence / 10 records</p><h3 id="catalogue-title">Briefing card catalogue</h3></div><p>Assign, duplicate, omit, or reorder. A card is knowledge—not proof it will be used.</p></header><div className="card-list">{coldStartFixture.briefingCards.map((card, index) => <BriefingCard key={card.cardId} card={card} index={index} draft={draft} onAssign={onAssign} />)}</div></section>;
}

function BriefingCard({ card, index, draft, onAssign }: { readonly card: BriefingCardFixture; readonly index: number; readonly draft: BuildDraft; readonly onAssign: (agentId: string, cardId: string) => void }) {
  const assigned = coldStartFixture.agents.filter(({ agentId }) => draft.agents[agentId]?.briefingCardIds.includes(card.cardId));
  return <article className={`briefing-card card-${card.category}`}><span className="record-index">REC {String(index + 1).padStart(2, "0")}</span><div className="card-copy"><p>{card.requiredContract ? "Required contract" : card.category}</p><h4>{card.title}</h4><span>{card.text}</span></div><div className="assignment-state" aria-label={`${card.title} assignment`}><small>{assigned.length === 0 ? "Unassigned" : assigned.length === 2 ? "Shared" : `Only ${assigned[0]?.label ?? "one agent"}`}</small>{coldStartFixture.agents.map((agent) => { const current = draft.agents[agent.agentId]; const hasCard = current?.briefingCardIds.includes(card.cardId) ?? false; const full = (current?.briefingCardIds.length ?? 0) >= 4; return <button type="button" key={agent.agentId} className={hasCard ? "assigned" : ""} disabled={hasCard || full} onClick={() => { onAssign(agent.agentId, card.cardId); }} aria-label={hasCard ? `${card.title} assigned to ${agent.label}` : `Assign ${card.title} to ${agent.label}`}>{agent.callSign}<span>{hasCard ? "Assigned" : full ? "Full" : "Assign"}</span></button>; })}</div></article>;
}

function FieldLabel({ id, title, detail }: { readonly id: string; readonly title: string; readonly detail: string }) {
  return <div className="field-label" id={id}><strong>{title}</strong><span>{detail}</span></div>;
}

function CharacterCount({ value, maximum }: { readonly value: string; readonly maximum: number }) {
  return <span className="character-count" aria-label={`${String(value.length)} of ${String(maximum)} characters`}>{value.length} / {maximum}</span>;
}
