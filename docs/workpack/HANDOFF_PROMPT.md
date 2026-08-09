# DIRECTIVE DRIFT — Agent Handoff Prompts

## 1. Fresh-repository lead prompt

Copy the prompt below into the lead coding-agent session after placing this
workpack in the new repository under `docs/workpack/`.

---

You are the lead implementation agent for a fresh repository named
`directive-drift`.

Build the Directive Drift vertical slice from the complete specification in
`docs/workpack/`.

Before changing code:

1. Read `docs/workpack/README.md` and `docs/workpack/AGENTS.md` completely.
2. Read `docs/workpack/07_AGENT_BUILD_PLAN.md`.
3. Read every file named by Packet P0.
4. Inspect the repository and report any existing state that conflicts with a
   fresh start.
5. Write a short implementation plan for P0 only.

Execute **Packet P0 — Repository and guardrails**. Do not start gameplay,
persistence, AI-provider, or visual-map implementation.

Non-negotiable architecture:

- .NET 10 LTS and C# own the authoritative Core, Content, Application, AI,
  Persistence, and ASP.NET Core API.
- React, strict TypeScript, and Vite own the browser client.
- The primary map will use semantic SVG/CSS.
- Unity, Godot, Blazor, Node backend, Python services, Redis, Kafka, GraphQL,
  and WebSockets are out of scope.
- The pure Core may not reference framework, I/O, wall clock, provider SDK,
  database, UI, or nondeterministic random APIs.
- Default build/test requires no model credential or network after
  dependencies are installed.
- Warnings are errors and nullable reference types are enabled.
- Pin SDK and dependencies; commit lockfiles.

Use the repository’s root `AGENTS.md` instructions. Preserve the workpack
verbatim under `docs/workpack/`.

P0 is complete only when its named clean-clone commands pass. Run them. If a
dependency or environment prevents a command, report the exact blocker rather
than weakening the gate.

At handoff, provide:

- outcome;
- files changed;
- project/reference diagram;
- commands run and exact results;
- deliberate omissions;
- decisions requiring an ADR;
- risks/blockers;
- the exact next recommended packet.

Do not claim completion from code inspection alone.

---

## 2. Standard packet implementation prompt

Use this for P1–P11. Replace bracketed fields.

---

Implement **[PACKET ID AND NAME]** in the Directive Drift repository.

Base commit: `[COMMIT]`

Before editing:

1. Read the root `AGENTS.md`.
2. Read Packet `[ID]` completely in
   `docs/workpack/07_AGENT_BUILD_PLAN.md`.
3. Read every document listed under that packet’s Required reading.
4. Inspect current implementation and tests in the permitted scope.
5. Restate the packet goal, dependencies, non-goals, and acceptance commands.
6. Report any contradiction before choosing a new product behavior.

Scope:

- permitted paths: `[PATHS]`
- integration paths requiring lead ownership: `[PATHS OR NONE]`
- forbidden adjacent work: everything in later packets unless a minimal
  compile/test seam is explicitly required.

Requirements:

- implement the smallest complete vertical behavior described by the packet;
- write or update tests before declaring completion;
- update contracts/examples/docs when a boundary changes;
- preserve deterministic Core and the agent information boundary;
- do not duplicate authoritative rules in TypeScript;
- do not introduce a new service/framework without an accepted ADR;
- do not require a live provider in default CI;
- keep the repository buildable at every handoff.

Acceptance:

`[PASTE PACKET ACCEPTANCE]`

At handoff, provide:

- concise outcome;
- changed files grouped by purpose;
- important design decisions;
- tests/commands and exact results;
- screenshots or fixtures when visual;
- schema/migration/telemetry impact;
- known limitations;
- deviations from the packet;
- next integration steps.

If an acceptance criterion cannot be met, stop and report the blocker. Do not
silently lower it.

---

## 3. Integrator review prompt

Run this before merging a packet.

---

Review the implementation of **[PACKET]** against:

- root `AGENTS.md`;
- the exact packet in `docs/workpack/07_AGENT_BUILD_PLAN.md`;
- relevant contracts and acceptance gates.

This is a review, not a broad rewrite.

Inspect the diff and produce findings ordered by severity. For each finding:

- cite the path and concrete behavior;
- explain which specification or invariant it violates;
- give a minimal correction;
- identify a missing test.

Explicitly check:

1. scope creep and speculative abstractions;
2. deterministic ordering/random/time/serialization;
3. private agent knowledge leakage;
4. rules duplicated in the web client;
5. schema/OpenAPI/example drift;
6. idempotency, retries, and atomicity;
7. unreserved external cost;
8. secret or private text logging;
9. error/recovery/accessibility states;
10. whether reported acceptance commands actually cover the behavior.

If no blocking finding exists, say so and list residual risks. Do not approve
based only on green tests.

---

## 4. Determinism audit prompt

---

Audit `DirectiveDrift.Core`, content materialization, and replay for
cross-process determinism.

Do not implement fixes until findings are accepted.

Find:

- use of `System.Random`, GUID generation, wall clock, current culture, or
  process-dependent data in rules;
- unordered dictionary/set iteration affecting results;
- unstable string comparisons;
- floating-point rule arithmetic;
- serializer ordering/version ambiguity;
- mutable shared state;
- platform-dependent overflow or path behavior;
- presentation values used by simulation;
- inconsistent tie-breaks;
- replay that invokes a provider or recomputes hidden materialization.

For each issue, provide a reproducer or precise failing-test design. Also list
the golden/property tests that already protect determinism and any gaps.

---

## 5. Knowledge-boundary audit prompt

---

Trace every field that can reach Kite’s and Wren’s live provider request.

Start at authoritative `RunState`, follow observation/context/prompt
serialization, retries, logs, stored attempts, API responses, and replay.

Compare against `docs/workpack/05_AI_RUNTIME.md`.

Report any path by which an agent can receive:

- unassigned cards or complete mission prose;
- partner role/cards/memory/observation/current decision;
- hidden hazard, patrol, variant, seed, score, or reference solution;
- a message before its delivery turn;
- extra information during retry;
- hidden truth through legal-action labels or public event summaries.

Also check whether the live browser sees unrevealed certification content.

Give an exact marker-based automated test for every uncovered gap. This is an
audit only unless explicitly asked to fix.

---

## 6. Cost-safety audit prompt

---

Prove that no live provider attempt can occur without a valid reservation
against turn-operation, run, guest-day, deployment-day, and concurrency caps.

Trace:

- initial calls;
- concurrent Kite/Wren calls;
- formatting retry;
- timeout/cancellation;
- operation reclaim after crash;
- usage missing from provider response;
- price-table update;
- suspended run resume;
- duplicate HTTP requests.

Look for double dispatch, reservation leak, negative settlement, browser-only
enforcement, and fail-open behavior. Provide an adversarial integration test
for every finding. Treat any unbounded-spend path as severity 1.

---

## 7. Visual implementation prompt

---

Implement or review the Directive Drift station map using:

- `docs/workpack/03_VISUAL_AND_MAP_SPEC.md`;
- `docs/workpack/visuals/map-style-concept.svg`;
- canonical events from the current API.

The goal is a distinctive SVG operations table, not a node-card grid.

Required:

- function-specific room silhouettes;
- topological double-line conduits;
- power, hazard, lock, message, movement, objective, and threat states;
- Kite/Wren identity without color alone;
- event-driven presentation reducer and ordered animation queue;
- power-restoration and console-sync payoff;
- command/Kite/Wren/truth lenses without live leakage;
- pause, speed, instant, reduced-motion, and readable modes;
- semantic labels and adjacent accessible state list;
- no rule resolution in TypeScript;
- no external/copyrighted art.

Profile the showcase sequence. Do not solve performance by removing essential
state cues. Submit screenshots at 1280×720, 1024×768, reduced motion, and the
unpowered/powered objective states, plus tests showing instant and animated
paths end in identical visual state.

---

## 8. Evaluation prompt

---

Run the anti-shortcut evaluation exactly as specified in
`docs/workpack/06_EVALUATION_AND_QA.md` using:

- `examples/generic-optimal-build.json`;
- `examples/designed-build.json`;
- the pinned official provider profile;
- the declared 8-variant × 5-repetition matrix.

Before spending:

- show projected maximum cost;
- confirm the evaluation budget and profile/version;
- verify all engine/content/leakage tests are green.

Report:

- result per build/variant/repetition;
- aggregate success and confidence interval;
- invalid/fallback rate;
- token, latency, and estimated cost distributions;
- earliest failure signatures;
- whether any variant dominates the result;
- exact hard-gate outcome.

If generic exceeds 25% or designed is below 70%, diagnose mechanics,
information leakage, content, or provider behavior. Do not alter the fixture
wording or gates during the run. Propose changes, then require a fresh full
matrix after approval.

---

## 9. Lead-agent operating cadence

At each packet boundary:

1. merge only from a green base;
2. update ADRs and packet status;
3. run the integrator review;
4. keep contracts/fixtures stable for parallel branches;
5. tag the first scripted playable, first live playable, and public candidate;
6. record measured cost and behavior separately from opinions;
7. stop expansion when a hard product gate fails.

The lead’s job is not to maximize generated code. It is to preserve one
coherent game while reducing uncertainty in the cheapest order.
