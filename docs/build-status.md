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
| P7+ | Not started | Scripted browser-to-server game, live providers, practice/certification, comparison, and launch hardening remain deferred. |

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
