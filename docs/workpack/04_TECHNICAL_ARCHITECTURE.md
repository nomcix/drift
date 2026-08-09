# DIRECTIVE DRIFT — Technical Architecture

## 1. Architecture decision

Use:

- **C# on .NET 10 LTS** for the authoritative simulation, application
  orchestration, content, AI-provider adapters, persistence, and HTTP API;
- **React + TypeScript + Vite** for the browser workbench, map, replay, and
  presentation;
- **SVG + CSS** for the primary playfield;
- **SQLite + EF Core** for the vertical slice;
- one container/process for initial hosted play.

This restores C# as the core technology without taking on Unity.

### Why C# does not imply Unity

C# is the language and .NET is the runtime/platform. Unity is one optional game
engine. Directive Drift v1 is mostly:

- forms and constrained text editing;
- cards and loadout allocation;
- a topological deterministic simulator;
- model orchestration;
- an animated 2D operations map;
- replay and diagnostics.

React/SVG implements those surfaces faster, ships as a normal browser page,
and is easier to instrument and share. Unity adds a separate editor, asset
serialization, browser build size, integration boundaries, and UI testing
costs before the game needs spatial physics or 3D.

Revisit Unity only after the hook is validated and the product needs 3D,
physics, console delivery, dense character animation, or direct control.

### Version baseline

.NET 10 is an active LTS release through November 2028 according to the
[official .NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy).
At Packet P0, install the current supported .NET 10 SDK patch and pin it in
`global.json`. Pin all direct NuGet and npm dependencies in lockfiles. Do not
put the word `latest` in reproducible setup.

## 2. Architectural goals

- deterministic, testable rules;
- complete separation of agent knowledge from engine truth;
- replaceable decision providers;
- replay without model calls;
- cheap single-instance hosting;
- one-command local development;
- an attractive browser map with no asset build pipeline;
- safe recovery from timeouts and budget exhaustion;
- a clean path from SQLite to PostgreSQL if real traffic requires it.

Non-goals:

- horizontal multi-region scale;
- real-time multiplayer;
- user code execution;
- generalized autonomous-agent infrastructure;
- event sourcing as a company-wide platform;
- offline live-model inference;
- native mobile or console clients.

## 3. Repository layout

```text
directive-drift/
  AGENTS.md
  README.md
  DirectiveDrift.sln
  Directory.Build.props
  Directory.Packages.props
  global.json
  .editorconfig
  .gitignore
  docker-compose.yml
  Dockerfile
  src/
    DirectiveDrift.Core/
      Actions/
      Decisions/
      Events/
      Model/
      Observation/
      Random/
      Rules/
      Scoring/
      Simulation/
    DirectiveDrift.Content/
      Authoring/
      Loading/
      Materialization/
      Validation/
    DirectiveDrift.Application/
      Builds/
      Certifications/
      Operations/
      Replays/
      Runs/
      Usage/
    DirectiveDrift.AI/
      Budget/
      Context/
      Providers/
      Runtime/
      Validation/
    DirectiveDrift.Persistence/
      Configurations/
      Entities/
      Migrations/
      Repositories/
    DirectiveDrift.Api/
      Endpoints/
      Middleware/
      OpenApi/
      Program.cs
    DirectiveDrift.Web/
      package.json
      package-lock.json
      vite.config.ts
      src/
        api/
        app/
        components/
        features/
          briefing/
          comparison/
          replay/
          run/
        map/
        presentation/
        styles/
  content/
    missions/
      cold-start/
        mission.json
        certification/
  contracts/
    agent-decision.schema.json
    build.schema.json
    mission.schema.json
  tests/
    DirectiveDrift.Core.Tests/
    DirectiveDrift.Content.Tests/
    DirectiveDrift.Application.Tests/
    DirectiveDrift.AI.Tests/
    DirectiveDrift.Persistence.Tests/
    DirectiveDrift.Api.Tests/
    DirectiveDrift.Architecture.Tests/
    DirectiveDrift.Evaluation/
    web/
    e2e/
  tools/
    DirectiveDrift.ContentCli/
    DirectiveDrift.ReplayCli/
  docs/
    adr/
    workpack/
```

Use one npm workspace only for the web client unless another JavaScript package
is proven necessary.

## 4. Project boundaries

| Project | Responsibility | May reference |
|---|---|---|
| `Core` | pure domain, observations, legal actions, turn resolution, score | BCL only |
| `Content` | schema loading, authored content, variant materialization, invariants | Core |
| `Application` | use cases, orchestration ports, transactions | Core |
| `AI` | context construction, providers, budgets, decision validation | Core, Application abstractions |
| `Persistence` | EF Core entities, migrations, repositories | Core, Application abstractions |
| `Api` | auth, endpoints, composition root, OpenAPI, static client hosting | Application, AI, Persistence, Content |
| `Web` | interaction and presentation only | generated HTTP types |

`Core` must never reference:

- ASP.NET;
- EF Core;
- provider SDKs;
- configuration files;
- network or file I/O;
- system clock;
- non-deterministic `Random`;
- presentation coordinates.

Architecture tests enforce the boundary.

## 5. Core API

The essential pure functions are conceptually:

```csharp
MaterializedMission Materialize(
    MissionDefinition definition,
    VariantSpec variant,
    Seed seed);

Observation BuildObservation(
    RunState state,
    AgentId agentId,
    AgentBuild agentBuild);

LegalActionSet GetLegalActions(
    RunState state,
    AgentId agentId);

TurnResult ResolveTurn(
    RunState preTurnState,
    IReadOnlyDictionary<AgentId, ValidatedDecision> decisions);

ScoreResult CalculateScore(
    RunState terminalState,
    ScoreDefinition definition);
```

Inputs and outputs are immutable records. A `TurnResult` contains next state
and ordered canonical events.

## 6. Determinism

### 6.1 Randomness

Do not use `System.Random` in domain rules. Implement and test one small,
versioned deterministic generator such as PCG32:

- explicit 64-bit state;
- seed and stream recorded;
- integer operations with documented overflow behavior;
- golden vectors in tests;
- state serialized in `RunState`.

Materialize all variant choices before turn one when possible.

### 6.2 Stable resolution

- sort agents and entities by opaque ID before tie-breaks;
- use integers for health, turn, resources, and score;
- use ordinal string comparisons;
- never iterate an unordered collection to produce rule order;
- do not use local time, culture, process ID, or hash randomization;
- canonicalize JSON before hashing;
- version rules, content, score, and serializers.

### 6.3 State hash

After every resolved turn:

1. convert authoritative state to a versioned canonical DTO;
2. serialize with fixed property and collection ordering;
3. compute SHA-256;
4. store the hash on `TurnEnded`.

A replay test starts from the same snapshot and accepted decisions, then checks
every resulting event and hash.

## 7. Domain model

Representative aggregate:

```csharp
public sealed record RunState(
    RunId RunId,
    MissionIdentity Mission,
    int Turn,
    RunStatus Status,
    ImmutableDictionary<AgentId, AgentState> Agents,
    ImmutableDictionary<EntityId, EntityState> Entities,
    ImmutableDictionary<ConnectionId, ConnectionState> Connections,
    ObjectiveState Objectives,
    CommunicationState Communication,
    ScoreState Score,
    DeterministicRandomState Random);
```

It contains no provider request, database object, UI state, animation, secret,
or wall-clock timestamp.

Commands ask the application to:

- create/version a build;
- start a run;
- enqueue the next turn;
- apply a practice Emergency Burst;
- abort a run;
- start/resume certification.

Decisions are untrusted provider inputs validated into domain values.

Events are accepted facts. Store event envelopes with:

- event ID;
- sequence;
- turn;
- phase;
- event type;
- typed payload;
- schema version;
- optional post-event state hash.

## 8. Content and contracts

Three machine contracts ship in `/contracts`.

- mission schema validates authored content;
- build schema validates persisted/imported builds;
- decision schema validates provider output.

The C# domain does not dynamically execute JSON Schema. Content loading:

1. validates raw JSON against Draft 2020-12 at the boundary;
2. deserializes into strict C# authoring DTOs;
3. validates cross-reference and solver invariants;
4. maps into domain definitions.

Contract tests validate the same fixtures with the chosen schema library.

The API publishes OpenAPI. Generate the TypeScript client during the build.
Do not manually duplicate endpoint types in the web project.

## 9. Application orchestration

Long provider latency should not hold gameplay correctness inside one browser
connection. Use a durable, single-process operation queue:

1. `POST /runs/{id}/turns` creates an idempotent `TurnOperation`;
2. API returns `202 Accepted` and operation ID;
3. a hosted `BackgroundService` claims queued operations;
4. it loads the exact pre-turn snapshot and build;
5. it requests both decisions concurrently;
6. it validates, resolves, and persists one transaction;
7. client polls operation state and then fetches new events.

SQLite is the queue for v1. Enforce:

- at most one active turn operation per run;
- lease and heartbeat fields;
- retryable claim after process restart;
- idempotency key unique per run/turn;
- no simulation advancement until both decisions are finalized;
- budget reservation before provider calls;
- atomic decision/event/state persistence.

Do not add Redis or a message broker for the vertical slice.

## 10. HTTP API

Version under `/api/v1`.

### Runtime and content

```text
GET  /runtime
GET  /missions
GET  /missions/{missionId}
GET  /missions/{missionId}/practice-variants
```

### Builds

```text
POST /builds
GET  /builds
GET  /builds/{buildId}
POST /builds/{buildId}/versions
GET  /builds/{buildId}/versions
```

### Runs

```text
POST /runs
GET  /runs/{runId}
POST /runs/{runId}/turns
GET  /operations/{operationId}
GET  /runs/{runId}/events?afterSequence={n}
POST /runs/{runId}/emergency-burst
POST /runs/{runId}/abort
GET  /runs/{runId}/replay
```

### Certification and comparison

```text
POST /certifications
GET  /certifications/{certificationId}
GET  /comparisons?leftRunId={a}&rightRunId={b}
```

Errors use RFC 9457 Problem Details plus stable game error codes. The client
must be able to distinguish:

- validation;
- conflict/idempotency;
- budget;
- provider unavailable;
- suspended operation;
- terminal run;
- internal content invariant.

## 11. Persistence

Use EF Core with SQLite in WAL mode for local/private playtest deployment.

Logical tables:

- `GuestProfiles`;
- `Builds`;
- `BuildVersions`;
- `Runs`;
- `RunSnapshots`;
- `TurnOperations`;
- `DecisionRecords`;
- `DomainEvents`;
- `Certifications`;
- `CertificationRuns`;
- `UsageLedger`;
- `SchemaMetadata`.

Store:

- structured searchable columns for IDs, status, versions, turn, and cost;
- canonical JSON for immutable build snapshots, state snapshots, decisions,
  and event payloads;
- provider/model profile ID, not secret;
- timestamps outside rule state through injected `TimeProvider`.

Snapshot at start and after every turn in v1. Data volume is trivial and
debuggability matters more than optimization.

Migrations:

- run automatically only in local development;
- explicit startup migration step in hosted deployment;
- test upgrade from previous checked-in database fixture;
- never delete incompatible runs silently.

If concurrency or traffic outgrows one process, move persistence and operation
claiming to PostgreSQL before adding more app instances.

## 12. Web architecture

Use:

- React with strict TypeScript;
- Vite;
- React Router;
- generated OpenAPI client;
- TanStack Query or an equivalently small server-state layer;
- local feature reducers for workbench and replay;
- CSS variables/modules;
- native SVG components;
- Vitest, Testing Library, and Playwright.

Do not replicate simulation rules in TypeScript. The client may:

- reduce canonical events into visual state;
- compute layout and animation;
- perform optimistic local build editing before submission;
- show descriptive card-overlap counts;
- validate simple character limits for feedback.

The server remains authoritative on all accepted builds and runs.

### Presentation reducer

```text
Canonical replay snapshot
  + ordered domain events
  -> PresentationReducer
  -> stable visual state + animation intents
  -> SVG components
```

Animations consume intents in order. “Resolve instantly” applies the same
reducer without transient timing.

## 13. Authentication and privacy

Vertical slice:

- create a random guest profile ID;
- store it in a secure, SameSite cookie;
- sign or server-map the cookie; never trust a client-provided owner ID;
- allow export/import of build JSON;
- optional account linking is later scope.

Public deployment:

- TLS only;
- server-side provider keys;
- rate limits by guest, IP range, and global budget;
- CSRF protection for cookie-authenticated mutations;
- strict content security policy;
- output encoding for all authored/model text;
- request-body limits;
- no provider prompt or raw text in routine request logs.

Build text is player data. Add deletion/export before collecting accounts.

## 14. Provider configuration

All live model access stays behind `IAgentDecisionProvider`.

Application configuration selects:

- `scripted`: deterministic fixtures for tests/tutorial;
- `fake`: locally varied decisions with no external calls;
- `live`: structured-output provider adapter.

Provider profile contains:

- provider and model identifier;
- sampling settings;
- structured-output mode;
- timeout;
- retry policy;
- token caps;
- price-table version;
- prompt-template version.

It never enters `Core`.

## 15. Deployment shape

Initial deployment is one container:

```text
Browser
  -> ASP.NET Core
       -> serves compiled React assets
       -> JSON API
       -> background turn worker
       -> SQLite on persistent volume
       -> external model API when enabled
```

Benefits:

- one origin;
- no CORS configuration;
- one deployable artifact;
- one cheap instance;
- simple backups;
- no always-on database bill;
- local and hosted topology remain similar.

The app must also run entirely in scripted mode without provider credentials.

## 16. Observability

Use structured logs and OpenTelemetry-compatible traces for:

- HTTP request;
- operation ID;
- run and turn ID;
- provider attempt;
- validation result;
- simulation resolution;
- persistence transaction;
- cost reservation/settlement.

Never attach full doctrine, messages, provider response, or API key to normal
telemetry.

Metrics:

- runs started/completed;
- turn operation latency;
- provider latency/error/invalid rate;
- input/output tokens;
- estimated spend;
- fallback decisions;
- certification pass rate by build/provider profile;
- replay and revision behavior.

## 17. Development commands

The implementation repository must expose stable commands:

```text
dotnet build
dotnet test
dotnet run --project src/DirectiveDrift.Api
npm ci --prefix src/DirectiveDrift.Web
npm run test --prefix src/DirectiveDrift.Web
npm run build --prefix src/DirectiveDrift.Web
npm run e2e --prefix src/DirectiveDrift.Web
```

Add `./scripts/dev`, `./scripts/validate`, and PowerShell equivalents only if
they improve cross-platform onboarding. Scripts call the canonical commands;
they do not hide failures.

## 18. Architecture acceptance

- solution builds from a clean clone;
- scripted mode needs no secret or network;
- architecture tests enforce project boundaries;
- same seed and decisions replay to identical hashes;
- process restart safely resumes a claimed turn operation;
- duplicate advance request cannot create a second turn;
- browser bundle contains no provider secret;
- API OpenAPI and generated client agree in CI;
- SQLite backup restores builds and complete replays;
- presentation coordinates can change without changing a core test;
- container starts with a health endpoint and read-only static assets;
- the vertical slice remains one deployable process.
