# DIRECTIVE DRIFT — AI-Assisted Build Plan

## 1. Recommended method

Build the vertical slice as a sequence of narrow work packets with hard
acceptance tests. Give each coding agent one packet, one branch/worktree, and
one explicit handoff. Merge only a green, reviewed packet.

Do not ask an agent to interpret all product, visual, backend, and AI concerns
at once. The workpack is complete context; the packet limits the active scope.

Recommended roles:

- **lead/integrator:** owns decisions, merges, scope, and end-to-end quality;
- **domain agent:** Core, Content, solver, evaluation;
- **platform agent:** Application, Persistence, API, deployment;
- **client agent:** React workbench, SVG map, replay;
- **quality agent:** adversarial review, leakage, accessibility, performance.

One person can perform every role serially. For a small AI-assisted build, use
at most two implementation branches concurrently until contracts stabilize.

## 2. Integration principles

### 2.1 Build a walking skeleton first

The first milestone is not a polished prompt editor. It is:

```text
load fixture
-> start scripted run
-> resolve deterministic turns
-> persist events
-> display map state
-> reach scripted result
-> replay without calls
```

### 2.2 Contract before implementation

An agent touching a boundary must first update:

- schema/OpenAPI;
- fixture;
- contract test;
- then implementation.

### 2.3 Fake expensive dependencies

Use scripted decisions through the complete product before adding a live
provider. A model should be the last uncertain runtime dependency, not the
foundation of every debugging session.

### 2.4 Review adversarially

After each packet, a separate review pass asks:

- Did this leak truth?
- Did this duplicate rules in the client?
- Did this add non-determinism?
- Can retries double charge or double advance?
- Did the packet weaken a product gate?
- Is there a simpler implementation?

## 3. Dependency map

```text
P0 Repository
 ├─> P1 Contracts + content loader
 │    └─> P2 Core simulator
 │         └─> P3 Cold Start + solver
 │              └─> P4 Application + persistence + API
 │                   ├─> P7 End-to-end scripted game
 │                   └─> P8 AI runtime + live provider
 │
 └─> P5 Web shell + workbench fixtures
      └─> P6 SVG map + presentation reducer
           └─> P7 End-to-end scripted game

P7 + P8 -> P9 Practice/certification/comparison
P9 -> P10 Hardening/evaluation/accessibility
P10 -> P11 Deployment + private launch
```

P5 may start after P0 using checked-in fixtures. It must not invent endpoint or
rule behavior while P1–P4 are in progress.

## 4. Packet template

Every assigned packet includes:

- goal;
- prerequisites/commit hash;
- permitted directories;
- required reading;
- exact deliverables;
- non-goals;
- tests/commands;
- acceptance evidence;
- handoff format.

Agent output must state:

- changed files;
- decisions made;
- tests and results;
- screenshots/artifacts where relevant;
- deviations or blockers;
- next packet assumptions.

## 5. P0 — Repository and guardrails

### Goal

Create a clean, reproducible .NET/React repository with CI and enforced
boundaries. No gameplay implementation.

### Required reading

- `README.md`;
- `AGENTS.md`;
- sections 1–4 and 17 of `04_TECHNICAL_ARCHITECTURE.md`.

### Deliverables

- repository layout from architecture;
- `DirectiveDrift.sln`;
- .NET 10 SDK pinned in `global.json`;
- central NuGet package management;
- nullable/warnings-as-errors;
- strict TypeScript and npm lockfile;
- empty projects and references following dependency table;
- initial architecture tests;
- Vite shell;
- `/health/live` and `/health/ready`;
- Dockerfile and local compose with persistent SQLite volume;
- CI for build, C# tests, web tests/build;
- workpack copied under `docs/workpack/`.

### Non-goals

- domain objects;
- EF model;
- provider SDK;
- visual polish.

### Acceptance

```text
dotnet build
dotnet test
npm ci --prefix src/DirectiveDrift.Web
npm run test --prefix src/DirectiveDrift.Web
npm run build --prefix src/DirectiveDrift.Web
docker build .
```

All pass from a clean clone. Architecture tests reject an intentional forbidden
reference during test development.

## 6. P1 — Contracts and content boundary

### Goal

Load and reject authored mission/build/decision JSON correctly.

### Required reading

- `02_COLD_START_MISSION.md`;
- contract files and examples;
- sections 5, 8, and 18 of architecture.

### Deliverables

- checked-in schemas and fixtures;
- schema-validation adapter;
- strict C# authoring DTOs;
- content ID/cross-reference validation;
- typed opaque identifiers;
- content CLI:
  `validate content/missions/cold-start/mission.json`;
- fixture and malformed-case tests;
- contract version constants.

### Non-goals

- variant solver;
- turn rules;
- database/API.

### Acceptance

- canonical example validates;
- every prepared invalid fixture fails with stable code;
- unknown properties fail where schema says so;
- duplicate/unresolved IDs fail;
- no raw authored SVG/CSS is accepted;
- JSON examples round-trip without semantic loss.

## 7. P2 — Deterministic core simulator

### Goal

Implement pure run state, observations, legal actions, turn resolution, events,
and scoring independent of Cold Start JSON loading.

### Required reading

- `01_GAME_DESIGN.md`;
- mission sections 6–9 and 14;
- architecture sections 5–7.

### Deliverables

- immutable domain records/value objects;
- PCG32 with golden vectors;
- run start state factory;
- private observation builder;
- legal action generator;
- nine-phase `ResolveTurn`;
- generator/console/cargo/threat/module state machines;
- message delay and memory state;
- terminal status and score;
- canonical event envelopes;
- canonical state serializer/hash;
- exhaustive unit/property/metamorphic tests.

### Non-goals

- LLM calls;
- database;
- React;
- content solver.

### Acceptance

- all core cases in QA section 3 pass;
- same state and decisions always produce identical events/hashes;
- coordinates and labels are absent from rule code;
- no I/O/framework dependency;
- coverage targets focus on rule branches, not arbitrary percentage.

## 8. P3 — Cold Start materializer, solver, and evaluation harness

### Goal

Turn the authored mission into proven playable variants.

### Required reading

- all of `02_COLD_START_MISSION.md`;
- QA sections 5 and 7.

### Deliverables

- mission definition mapping;
- safe mutation materializer;
- practice variants;
- server-held certification fixture set;
- topology/invariant validator;
- deterministic reference solver;
- at least three reference policy families;
- scripted decisions for onboarding and golden cases;
- evaluation CLI accepting build, provider mode, matrix, repetitions;
- generic/designed fixture loading;
- content golden tests.

### Non-goals

- live provider;
- player certification UX;
- broad procedural generation.

### Acceptance

- all fixed variants solve within 17 turns;
- no module is required;
- no-damage proof exists;
- reference families pass named applicable variants;
- invalid mutation combinations fail before run;
- scripted generic tutorial fails for the intended missing knowledge;
- scripted corrected tutorial succeeds.

## 9. P4 — Application, persistence, and API spine

### Goal

Persist immutable builds and run one turn safely through a durable operation.

### Required reading

- architecture sections 9–15;
- AI runtime sections 5–6 and 9;
- API/persistence QA.

### Deliverables

- application ports/use cases;
- EF Core SQLite model and initial migration;
- guest-profile ownership;
- build/version endpoints;
- start-run endpoint;
- turn operation queue and hosted worker;
- scripted provider wiring only;
- event pagination and replay endpoint;
- usage reservation abstractions;
- OpenAPI;
- generated TypeScript client in CI;
- API/persistence integration tests.

### Non-goals

- live provider SDK;
- certification;
- styled client.

### Acceptance

- complete scripted run through HTTP;
- duplicate advance cannot double-turn;
- process restart reclaims a stale operation in integration test;
- state/events/decisions commit atomically;
- ownership checks;
- replay endpoint returns all necessary data;
- real temporary SQLite used in integration tests.

## 10. P5 — Web shell and briefing workbench

### Goal

Make build creation understandable and satisfying against fixtures.

### Required reading

- product brief;
- game-design sections 2–5 and 18–20;
- visual sections 3, 13–15.

### Deliverables

- route/screen shell;
- design tokens and base components;
- mission briefing/objective tree;
- Kite/Wren capability panels;
- doctrine and private role editors;
- card assignment, duplication, reorder, remove;
- module assignment;
- information-overlap summary;
- prediction field;
- client-side character feedback;
- keyboard operation;
- fixture-backed build save;
- component tests and Storybook only if already justified.

### Non-goals

- simulation rules in client;
- live run;
- full SVG map;
- account UI.

### Acceptance

- a tester can produce a schema-valid build without explanation;
- exact four-slot rule and one module per agent;
- required cards may be omitted but are visibly accounted for;
- full keyboard path;
- 1280 and 1024 screenshots;
- no generic grid of identical cards dominates the run screen.

## 11. P6 — SVG map and presentation reducer

### Goal

Meet the visual concept using static fixtures and canonical scripted events.

### Required reading

- all of `03_VISUAL_AND_MAP_SPEC.md`;
- `visuals/map-style-concept.svg`;
- mission topology and event catalogue.

### Deliverables

- semantic responsive `StationMap`;
- trusted room shape components;
- conduit layer and waypoints;
- Kite/Wren/drone/hazard/objective tokens;
- command/Kite/Wren/truth lenses with fixture access rules;
- presentation reducer;
- ordered animation queue;
- event choreography for move, scan, repair, power, sync, damage, pickup,
  success;
- pause/speed/instant controls;
- reduced-motion and readable modes;
- adjacent accessible state list;
- visual regression fixtures.

### Non-goals

- custom raster art;
- canvas/WebGL;
- server integration;
- simulation decisions.

### Acceptance

- visual gate in visual spec section 18;
- final state identical with animation and instant mode;
- coordinates do not enter any rule package;
- 60 fps target profile for showcase sequence;
- keyboard focus and accessible labels;
- unpowered-to-powered transformation matches or improves concept.

## 12. P7 — Scripted end-to-end game

### Goal

Connect workbench, API, operation polling, map, run, replay, and onboarding with
no external provider.

### Required reading

- onboarding and replay sections of game design;
- API contract;
- relevant Playwright plan.

### Deliverables

- generated API client integration;
- guest bootstrap;
- immutable build version submission;
- start/advance/autoplay;
- operation progress and recoverable error states;
- event pagination into presentation queue;
- complete scripted onboarding failure;
- guided revision;
- scripted success;
- replay and truth lens;
- run summary and diagnostic signals;
- browser smoke tests.

### Acceptance

- clean browser completes both onboarding runs;
- refresh during active operation resumes safely;
- replay triggers no decision request;
- missing-sync cause is legible;
- scripted success reaches correct score/hash;
- all default CI remains secret-free.

This is the first internal playable milestone.

## 13. P8 — AI runtime, fake provider, and one live adapter

### Goal

Add real autonomy without changing the game or safety boundary.

### Required reading

- all of `05_AI_RUNTIME.md`;
- QA leakage and cost sections.

### Deliverables

- exact `AgentTurnContext`;
- versioned prompt assembler;
- decision-schema validation;
- retry and deterministic fallback;
- fake policy/error provider;
- one current structured-output live adapter;
- provider profile configuration;
- concurrent two-agent calls;
- budget reservation/settlement and usage ledger;
- timeouts/cancellation;
- sanitized decision diagnostics;
- leakage, invalid-output, crash/resume, and cap tests;
- internal cost report.

### Non-goals

- multiple live providers;
- model selection UI;
- BYOK;
- fine-tuning;
- agent tools.

### Acceptance

- serialized-context leakage suite passes;
- fake provider covers every failure category;
- budget prevents calls before dispatch;
- no retry adds knowledge;
- live smoke run completes under cap;
- timeout/restart cannot double call when a valid stored result exists;
- browser never receives key.

## 14. P9 — Practice, certification, comparison, and sharing

### Goal

Create the mastery loop after one run.

### Deliverables

- practice variant selection/reveal;
- practice-random safe materialization;
- build history and version diff;
- run comparison;
- three-run hidden certification;
- certification resume and reveal;
- badges;
- share-card image/metadata;
- run cost shown in internal diagnostics;
- player-facing usage allowance;
- tests for secrecy and eligibility.

### Acceptance

- certification locks exact build/profile;
- no hidden variant content reaches browser before reveal;
- two of three is enforced;
- assisted run cannot certify;
- historical certificate survives content/profile change;
- comparison identifies first differing decision;
- share output excludes secret/hidden data.

## 15. P10 — Hardening and product validation

### Goal

Pass the full public-prototype gate.

### Deliverables

- 40-run generic and 40-run designed evaluation;
- mechanics/content iteration if gates fail;
- accessibility audit and fixes;
- visual regression and performance pass;
- security/ownership/spend review;
- backup/restore drill;
- telemetry privacy review;
- 15–20 player usability study;
- prioritized findings and ship/no-ship memo.

### Acceptance

- every release gate in `06_EVALUATION_AND_QA.md`;
- generic ≤25%, designed ≥70%;
- no open S1/S2 defects;
- map visual acceptance;
- hard spending circuit breaker tested;
- evidence that players revise builds, not only watch.

## 16. P11 — Deployment and private launch

### Goal

Deploy one monitored, budget-capped artifact and recruit the first useful
cohort.

### Deliverables

- production container and persistent volume;
- TLS/domain;
- migration and rollback procedure;
- daily encrypted/offsite database backup;
- provider secret configuration;
- global budget alert and kill switch;
- privacy/usage copy;
- landing page, 45-second clip, five screenshots;
- private invite/feedback flow;
- analytics events from launch plan;
- runbook for outage, spend spike, and provider change.

### Acceptance

- clean production health/readiness;
- scripted mode still usable if live provider disabled;
- restore backup into staging and replay a run;
- global cap stops new live operations;
- first tester can play from a link with no install;
- feedback joins run/build version without exposing private prompt text.

## 17. Suggested schedule

For one experienced developer using coding agents:

| Week | Target |
|---|---|
| 1 | P0–P1 |
| 2 | P2 and P5 start |
| 3 | P3, P4, P6 |
| 4 | P7 internal playable |
| 5 | P8 live autonomy |
| 6 | P9 mastery loop |
| 7 | P10 evaluation and iteration |
| 8 | P11 private launch |

This is an aggressive focus schedule, not a promise. Protect P2, P7, and P10;
cut optional polish before cutting determinism or evaluation.

## 18. Parallel work guidance

Safe early parallelism:

- P1 contract/content and P5 fixture UI after P0;
- P3 solver and P6 map after P2 event shapes stabilize;
- visual polish and fake-provider resilience once P7 works.

Unsafe parallelism:

- two agents editing Core rules;
- API and client inventing separate contracts;
- live provider before context boundary tests;
- certification before variant solver;
- broad refactors during P10 evaluation.

Use a short-lived branch per packet and rebase before handoff. The integrator
owns migrations, shared package files, schemas, and cross-packet decisions.

## 19. Agent review prompts

After implementation, run targeted independent reviews:

- **Determinism review:** find wall-clock, unordered iteration, random, float,
  or serialization instability.
- **Knowledge-boundary review:** trace every field from engine state to each
  provider request.
- **Cost review:** prove every external attempt reserves and settles against
  all limits.
- **Client-authority review:** find any rules or hidden truth duplicated in
  TypeScript.
- **Visual review:** compare screenshots to concept and accessibility modes.
- **Scope review:** list additions not required by the packet and remove or
  justify them.

Reviews should report evidence and exact paths, not rewrite large areas
unprompted.
