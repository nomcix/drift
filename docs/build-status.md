# Directive Drift build status

Last updated: 2026-08-09

| Packet | Status | Evidence |
|---|---|---|
| P0 — Repository and guardrails | Complete | Solution/web/container acceptance commands pass. |
| P1 — Contracts and content boundary | Complete | Strict schemas, DTOs, loaders, stable validation errors, and content CLI pass. |
| P2 — Deterministic core simulator | Complete | Core rules, PCG32, observation, determinism, replay, terminal, and architecture tests pass. |
| P3 — Cold Start materializer, solver, and evaluation harness | Complete | Eleven fixed variants have ≤17-turn interchangeable-loadout and no-damage proofs; three named policy families and scripted onboarding fail/success gates pass. |
| P3.5 — Bounded roster extensibility | Complete | Build JSON and C# use an opaque `AgentId`-keyed two-entry map; mission-relative validation requires the exact authored roster without identity-name branching. |
| P4 — Application, persistence, and API spine | Complete | A scripted run completes through durable HTTP turn operations; SQLite migration/WAL, ownership, idempotency, lease recovery, atomic commits, pagination, replay, OpenAPI, and generated TypeScript client gates pass. |
| P5 — Web shell and briefing workbench | Complete | Fixture-backed mission briefing, objective chain, accepted opaque two-agent roster, four-slot loadouts, modules, overlap accounting, prediction, keyboard controls, and schema-shaped save pass 1280/1024 presentation and component gates. |
| P6 — SVG map and presentation reducer | Complete | Semantic station SVG, trusted silhouettes, typed presentation fixtures, canonical-event reducer, ordered playback, gated lenses, accessible state, responsive context drawer, and powered/unpowered visual gates pass. |
| P7 — Scripted end-to-end game | Complete | Generated-client guest bootstrap, immutable build revisions, durable operation polling/resume, paged canonical events, guided fail/revise/succeed onboarding, replay truth/diagnostics, and Playwright smoke gates pass. |
| P8 — AI runtime, fake provider, and one live adapter | Complete | Private context/prompt assembly, strict decision validation/repair/fallback, fake failure modes, pinned OpenAI Responses adapter, concurrent calls, pre-dispatch SQLite budgets, durable checkpoints, usage/cost diagnostics, and leakage/cap/restart gates pass. |
| P9+ | Not started | Practice/certification, comparison, sharing, and launch hardening remain deferred. |

## P8 implementation status

P8 adds provider-neutral `AgentTurnContext` assembly from the immutable pre-turn
snapshot and immutable build version. Each serialized request contains one
agent's identity, ordered assigned cards, capabilities/module, doctrine and
private role order, exact private observation, delivered messages, replacement
memory, engine-generated legal action IDs, and response limits. The actual
OpenAI request serializer is marker-tested against complete mission text,
unassigned cards, partner configuration/identity, and hidden variant values.

The versioned prompt assembler emits stable context/template hashes and labels
all player-authored content as untrusted mission data. Structured decisions are
validated for byte/shape/schema/version/action membership/recipient/text/memory
limits. Only malformed or structurally invalid output receives one repair with
the identical context. Transport, timeout, cap, and final validation failures
produce a deterministic engine-visible wait fallback that preserves memory.
Fake transport covers valid, malformed, missing/extra field, illegal action,
wrong recipient, oversize, timeout, latency, and transport-error paths.

The official live adapter uses the OpenAI Responses API with strict
`text.format` JSON Schema, no tools, `store=false`, a bounded response stream,
and pinned profile `openai-gpt-5-mini-2025-08-07-v1`. Scripted remains the
secret-free default; fake and live modes are server configuration only. Provider
keys never enter context, persistence, OpenAPI, or browser responses.

SQLite reserves the maximum projected two-agent/retry amount before concurrent
dispatch and enforces operation, run, 40-attempt run, guest-day,
deployment-day, and concurrency caps. Each finalized provider result stores
the exact private context, prompt/context hashes, sanitized per-attempt
diagnostics, usage/cost/latency, and profile/state integrity fields. Lease
recovery reuses a matching checkpoint, while failed abandoned operations stop
counting stale reservations. Turn state, canonical events, resolved decisions,
and usage settlement still commit atomically.

### P8 schema, migration, and telemetry impact

- Authored JSON Schemas, build contract version `1`, accepted ADR 0001 roster,
  HTTP/OpenAPI shapes, and generated TypeScript files are unchanged.
- EF migration `20260810010923_P8AiRuntime` advances persistence metadata to
  version `2` and adds `ProviderDecisionCheckpoints`; migration details are in
  `docs/migrations/0002-p8-ai-runtime.md`.
- The existing usage ledger now records durable reservations before dispatch
  and actual settlement in the authoritative turn transaction. Stored provider
  checkpoints supply internal context/profile/attempt/token/cost/latency
  diagnostics; no raw wire logs, auth headers, or keys are retained.
- `docs/p8-cost-report.md` records the pinned price table and bounded projected
  operation/run costs.

### P8 acceptance evidence

Run on 2026-08-09:

- `dotnet build --no-restore --disable-build-servers --verbosity minimal -m:1`
  — passed with 0 warnings and 0 errors.
- `dotnet ef migrations has-pending-model-changes ... --no-build` — passed; no
  model changes remain outside migrations.
- `dotnet test --no-build --no-restore --disable-build-servers --verbosity minimal -m:1`
  — 138 passed, 0 failed, 0 skipped.
- P8 focused suites — Application 3 passed, AI 14 passed, Persistence 8 passed,
  API 6 passed, and Architecture 7 passed. These include concurrent same-state
  dispatch, zero-call checkpoint resume, actual-serializer leakage markers,
  all fake failure categories, adapter smoke, cap-before-dispatch, and durable
  reservation/checkpoint tests.
- `./scripts/generate-api-client.sh` — passed with no generated-client drift.
- `npm run lint --prefix src/DirectiveDrift.Web` — passed with 0 warnings.
- `npm run test --prefix src/DirectiveDrift.Web` — 22 passed, 0 failed.
- `npm run build --prefix src/DirectiveDrift.Web` — passed.
- `npm run test:e2e --prefix src/DirectiveDrift.Web` — both Chromium P7
  fail/revise/succeed/replay and refresh/resume regression scenarios passed.

The live-adapter smoke uses a bounded stubbed Responses endpoint in default CI.
No external provider or credential is required by a pull request; a real-model
behavior/cost smoke remains an explicitly configured private-live operation as
required by the QA test-mode matrix.

### P8 deliberate omissions

- No P9 practice/certification/comparison/sharing behavior was added.
- No provider-selection UI, BYOK, second live provider, fine-tuning, tools,
  agent framework, or TypeScript rule resolution was added.
- No generic/designed 40-run live evaluation was started; that remains the P10
  public-prototype evaluation gate.

## P7 implementation status

P7 connects the P5 workbench and P6 semantic map to the P4 HTTP API through
the checked-in generated client. A guest can load the deliberately generic
tutorial build, lock version 1, run the durable scripted operation loop, and
observe a factual console-sync failure. The guided revision assigns Wren the
missing sync contract as immutable build version 2; the same server-owned
simulation then succeeds with score 1480.

The browser stores only opaque run/operation IDs and the submitted build
snapshot needed to restore presentation after refresh. On reload it polls the
existing durable operation and never submits a replacement turn. Canonical
events are fetched in bounded ordered pages and adapted into the P6 reducer;
the client does not generate legal actions or resolve rules. Terminal replay
uses the stored API replay and its local presentation queue, so replay controls
make no decision requests. Truth remains gated until the authoritative run is
terminal.

The shared `ScriptedKnowledgePlan` is now used by both the evaluation harness
and API run preparation. It applies the existing knowledge-boundary behavior
to the authoritative scripted plan: an agent lacking the sync contract cannot
perform its activation and waits through the remaining run. This keeps the
generic “act optimally” build weak in the actual browser path as well as the
evaluation CLI.

### P7 schema, migration, and telemetry impact

- No authored JSON Schema, persistence schema, migration, or telemetry
  contract changed.
- The HTTP/OpenAPI surface is unchanged. The existing generated client is
  consumed directly and no generated file was hand-edited.
- Build contract version `1` and ADR 0001's opaque exact two-agent roster are
  unchanged. The browser submits immutable persisted build versions 1 and 2.
- No provider credential, live call, new server, or rule implementation in
  TypeScript was added. Playwright is a pinned development-only dependency.

### P7 acceptance evidence

Run on 2026-08-09:

- `dotnet build --no-restore --disable-build-servers --verbosity minimal -m:1`
  — passed with 0 warnings and 0 errors.
- `dotnet test --no-build --no-restore --disable-build-servers --verbosity minimal -m:1`
  — 120 passed, 0 failed, 0 skipped.
- `npm run lint --prefix src/DirectiveDrift.Web` — passed with 0 warnings.
- `npm run test --prefix src/DirectiveDrift.Web` — 22 passed, 0 failed.
- `npm run build --prefix src/DirectiveDrift.Web` — passed; strict TypeScript
  and Vite production build completed.
- `npm run test:e2e --prefix src/DirectiveDrift.Web` — 2 Chromium smoke tests
  passed: fail/revise/succeed/replay and refresh-during-operation resume.

The designed-build API gate pins score 1330 and proves the terminal run hash
equals the final canonical `TurnEnded` hash. The guided browser revision pins
its authored module/loadout result at score 1480. Replay interaction is
instrumented to prove it sends no additional turn requests.

### P7 deliberate omissions

- No P8 live provider, prompt assembly, token budget, provider retry, or SDK
  adapter was implemented.
- No P9 certification, comparison, or Emergency Burst client flow was added.
- No API route, persistence migration, authored contract, or visual geometry
  changed.

## P6 implementation status

P6 adds a fixture-backed station operations screen without connecting the web
client to simulation or server state. Mission presentation coordinates and
trusted shape names live only in the web fixture. Canonical showcase events
contain opaque IDs and accepted facts; a pure reducer applies them in sequence
to stable presentation state. The animated queue and resolve-instantly path use
the same reducer and produce identical final state.

The responsive semantic SVG has eleven function-shaped rooms, twelve routed
double-line conduits, non-color Kite/Wren identity, radiation and drone threat
cues, objective rings, archive state, and the whole-station power transition.
Command, Kite, Wren, and post-run Truth lenses enforce fixture discovery rules;
Truth remains unavailable while the replay state is live. Rooms expose labels,
topological arrow-key focus, inspection state, and an adjacent structured state
list. Pause, 1x/2x speed, instant, reset, readable, and reduced-motion paths are
present. At the 1024 breakpoint the context rail becomes an operable drawer.

### P6 schema, migration, and telemetry impact

- No JSON Schema, C# boundary, OpenAPI, generated client, persistence, or
  telemetry contract changed.
- No simulation rule is implemented in TypeScript. Presentation coordinates
  remain in `src/DirectiveDrift.Web/src/fixtures/stationShowcase.ts` and do not
  appear in canonical showcase events or any C# rule package.
- P5 continues to materialize the accepted ADR 0001 opaque two-entry roster;
  P6 consumes its authored Kite/Wren fixture labels only for this mission.

### P6 acceptance evidence

Run on 2026-08-09:

- `dotnet build --no-restore --disable-build-servers --verbosity minimal -m:1`
  — passed with 0 warnings and 0 errors.
- `dotnet test --no-build --no-restore --disable-build-servers --verbosity minimal -m:1`
  — 119 passed, 0 failed, 0 skipped.
- `npm run lint --prefix src/DirectiveDrift.Web` — passed with 0 warnings.
- `npm run test --prefix src/DirectiveDrift.Web` — 18 passed, 0 failed.
- `npm run build --prefix src/DirectiveDrift.Web` — passed; strict TypeScript
  and Vite production build completed.
- In-app browser checks at 1280x720 and 1024x768 passed for unpowered, powered,
  instant completion, responsive context drawer, and accessible DOM state with
  no console warnings or errors.
- The map uses 144 SVG nodes and 322 total page nodes. The active animation
  surface is 4 elements at rest and 19 after power restoration, below the
  specification caps of 900 DOM nodes and 25 simultaneous animated elements.

The browser verification surface did not expose frame-timing APIs, so the 60
fps target is supported by the bounded node/animation profile and transform,
opacity, and stroke-based effects rather than a recorded numeric FPS trace.
No visual acceptance criterion was weakened.

### P6 deliberate omissions

- No server integration, autonomous decision loop, API polling, provider,
  certification flow, or P7 work was added.
- No raster art, canvas, WebGL, audio, or new framework dependency was added.

## P4 implementation status

P4 adds application ports for storage, decision providers, and usage
reservation, with a turn processor that claims a leased operation, obtains both
scripted decisions concurrently, resolves exactly once through Core, and asks
persistence to commit the snapshot, decisions, canonical events, usage
settlement, run status, and operation status in one transaction.

EF Core SQLite supplies the initial schema and WAL-backed queue. Idempotency
keys are unique per run so a retry after a fast commit returns the original
operation instead of advancing another turn. A partial unique index allows at
most one queued or processing operation per run. Expired integer-timestamp
leases can be reclaimed by a fresh repository/worker instance without making
a duplicate provider call for the scripted mode.

Guest IDs are random opaque server-mapped cookies; unknown presented IDs are
replaced rather than adopted. Mutations use a SameSite double-submit CSRF
token, and all build, run, event, operation, and replay reads apply ownership
filters. Certification variants remain server-held and cannot be selected by
the P4 start-run endpoint.

The replay response contains the immutable build version, initial canonical
snapshot, ordered canonical events, resolved decisions, and terminal run
summary. It performs no provider calls. OpenAPI is served by the API and the
checked-in TypeScript fetch client is regenerated and drift-checked in CI.

### P4 schema and migration impact

- No authored JSON Schema version changed; build contract version `1` retains
  the accepted opaque `AgentId`-keyed two-agent roster.
- The API/OpenAPI surface is new and has no earlier client compatibility
  obligation. Generated client files are never hand-edited.
- Migration `InitialCreate` establishes the P4 database. P0–P3 had no
  persisted database, so there is no earlier data fixture to transform or
  incompatible run to preserve.
- Scripted usage reservations and settlements are zero-cost ledger entries.
  Live token, price, daily, and concurrency budget enforcement remains later
  provider scope.

### P4 acceptance evidence

Run on 2026-08-09:

- `dotnet build --no-restore` — passed with 0 warnings and 0 errors.
- `dotnet test --no-build --no-restore` — 119 passed, 0 failed, 0 skipped.
- P4 persistence suite — 6 passed against real temporary SQLite files.
- P4 API suite — 4 passed, including a complete scripted HTTP run.
- `./scripts/generate-api-client.sh` — generated OpenAPI and TypeScript client.
- `npm run lint --prefix src/DirectiveDrift.Web` — passed with 0 warnings.
- `npm run test --prefix src/DirectiveDrift.Web` — 1 passed.
- `npm run build --prefix src/DirectiveDrift.Web` — passed.
- `npm audit --prefix src/DirectiveDrift.Web --audit-level=moderate` — 0 vulnerabilities.
- `docker build -t directive-drift:p4 .` — passed; image
  `sha256:f7a585daba4b46e408ab92cec647bbafce1ca58801067281f400f1456da542ca`.
- Container `/health/ready` returned `Healthy`; `/api/v1/runtime` returned the
  scripted `v1` runtime and `dd-state-1` state schema.

### P4 deliberate omissions

- No live or fake provider SDK, certification workflow, emergency burst,
  abort, comparison, styled client, or P5 work was added.
- Hosted deployments do not migrate automatically unless the explicit startup
  migration setting is enabled; local Development and integration tests do.

## P3.5 implementation status

Build contract version `1` remains limited to exactly two active autonomous
agents. The JSON wire shape is an object keyed by opaque mission-authored agent
IDs, and the C# boundary now preserves those keys as `AgentId` values instead
of reverting to strings. Mission-relative validation independently enforces
the two-entry invariant and exact roster equality before materialization.

Agent labels, capabilities, module definitions, and the briefing-card
catalogue remain mission content. Production C# contains no literal `kite` or
`wren` identity branches; Cold Start JSON fixtures and reference prose retain
their authored names. The reversed key order in the generic build example
also demonstrates that JSON property order has no role semantics.

This is a pre-persistence contract correction. Schema version `1` and the JSON
wire representation are unchanged, no database migration exists, and no
generated TypeScript client is present to regenerate.

## P3 implementation status

P3 maps validated authoring records into immutable Core `RunDefinition`s,
rejects unsafe mutations before run creation, keeps certification mutations in
a server-held fixture, and chooses seeded random practice content only from the
solver-proven catalogue. Golden start-state hashes pin every fixed
materialization.

The reference solver selects only engine-generated legal action IDs and sends
every simultaneous pair through `TurnResolver`; it does not duplicate rule
resolution. The content validator additionally checks graph reachability, two
console role assignments, two or more patrol-safe sync windows, briefing-card
slots, presentation bounds, two disjoint module loadouts, completion by turn
17, and zero-damage proofs.

The evaluation CLI accepts build, provider mode, matrix, and repetition
arguments. Its only P3 provider mode is deterministic `scripted`. The generic
tutorial fixture fails with `unknown-required-contract` because Wren lacks the
sync contract; the designed fixture succeeds without decision fallback. No
live provider is called.

## P3 content-fairness correction

The initial authored patrol phases for `cs-practice-03`, `cs-practice-05`,
`cs-cert-01`, `cs-cert-03`, and `cs-cert-05` could not satisfy the combined
17-turn, zero-damage, and interchangeable-module invariants under the P2 Core.
P3 adjusts only their patrol routes/phases, using rooms and mechanics already
taught by Cold Start. The root mission example is versioned `2.0.1`, the
server-held pool is versioned `cold-start-cert-2`, and both are updated
together; the original workpack remains unchanged as the source handoff
artifact.

There is no schema-version or persistence migration. A new internal
`certification-variants.schema.json` contract is version `1`. No generated web
client exists yet, so there is no generated TypeScript impact.

## Deliberate omissions

- No roster-extensibility correction was implemented in P3.
- No P4 application, persistence, API, operation worker, or OpenAPI work was
  started.
- No live provider, provider SDK, certification UX, or broad procedural
  generation was added.

## Acceptance evidence

Run on 2026-08-09:

- `dotnet build` — passed with 0 warnings and 0 errors.
- `dotnet test` — 112 passed, 0 failed, 0 skipped.
- `npm ci --prefix src/DirectiveDrift.Web` — passed.
- `npm run lint --prefix src/DirectiveDrift.Web` — passed with 0 warnings.
- `npm run test --prefix src/DirectiveDrift.Web` — 1 passed.
- `npm run build --prefix src/DirectiveDrift.Web` — passed.
- `docker build .` — passed; image
  `sha256:8e1251050f25972c00cbd3c3ba4b25593c0e3b2ef8ceff04f63a2c7f6a0589c2`.
- Cold Start content CLI — passed; content `2.0.1`, 11 variants proven.
- Generic scripted tutorial — failed as intended with
  `unknown-required-contract` and 0 decision fallbacks.
- Designed scripted tutorial — succeeded with 0 decision fallbacks.
- Designed pinned scripted matrix — 8/8 succeeded with 0 decision fallbacks.

P3-touched C# paths pass `dotnet format --verify-no-changes`. A repository-wide
format check still reports the pre-existing import order in
`tests/DirectiveDrift.Core.Tests/RunStartFactoryTests.cs`; P3 does not modify
that P2 file.
