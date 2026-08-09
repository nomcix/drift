# Repository Instructions for Coding Agents

These instructions become the root `AGENTS.md` in the implementation
repository.

## Mission

Implement the Directive Drift vertical slice specified by this workpack.
Preserve deterministic rules, partial information, strict AI boundaries, and a
fast browser-first experience.

## Non-negotiable product rules

- The player knows the complete mission.
- An agent never receives the complete mission, complete objective tree,
  partner observation, hidden variant, or authoritative state.
- Build configuration is locked before a run.
- There is no normal mid-run player prompt.
- Agents choose simultaneously from engine-generated legal action IDs.
- The C# engine alone resolves state, objectives, hazards, and score.
- AI text is never treated as a command unless its action ID validates.
- Replays require no model calls.
- A generic “act optimally” build must remain a weak baseline.

Do not solve gameplay problems by secretly giving agents more information.

## Technology boundary

Use:

- .NET 10 LTS and C# for the authoritative core, content, application, AI
  orchestration, persistence, and ASP.NET Core API;
- React, TypeScript, and Vite for the browser client;
- SVG and CSS for the primary map;
- SQLite through EF Core for local and small hosted deployments;
- xUnit for C# tests;
- Vitest and Playwright for client and end-to-end tests.

Do not introduce Unity, Godot, Blazor, Node server code, Python runtime
services, Redis, Kafka, GraphQL, WebSockets, or an agent framework without an
accepted architecture decision record.

## Dependency rules

`DirectiveDrift.Core` is pure and deterministic. It must not reference:

- ASP.NET Core;
- EF Core;
- provider SDKs;
- file or network I/O;
- wall-clock time;
- random APIs without injected seeded state;
- UI packages.

Allowed dependency direction:

```text
Contracts <- Core <- Content
                  <- Application <- Api
                  <- AI ----------^
                  <- Persistence -^

OpenAPI -> Web
Domain events -> Web presentation reducer
```

The browser never imports or reimplements authoritative rule resolution.

## Coding rules

- Enable nullable reference types and warnings as errors.
- Prefer immutable records and explicit value objects in the domain.
- Use opaque IDs; labels are not IDs.
- Inject `TimeProvider` outside the core.
- Inject seeded deterministic random state into the core.
- Use integer score arithmetic. Do not use floating point in rules.
- Validate all content and all model output at boundaries.
- Store canonical domain events, not UI animation events.
- Keep provider-specific objects inside provider adapters.
- Never log API keys, complete prompts, or raw user-authored text by default.
- Use cancellation tokens on I/O and provider calls.
- A retry must not advance simulation state.
- Avoid speculative abstractions. Implement the current two-agent mission.

## Contract changes

Contract changes require:

1. an updated JSON Schema;
2. updated C# boundary type;
3. regenerated TypeScript API/client types where applicable;
4. updated examples;
5. contract tests;
6. a migration note if persisted data changes.

Never hand-edit generated client files.

## Required test layers

Every rule change needs a unit or property test. Maintain:

- core rule tests;
- content/schema tests;
- determinism and replay tests;
- decision validation tests;
- AI prompt-boundary tests;
- API integration tests;
- persistence migration tests;
- UI reducer/component tests;
- Playwright happy-path and failure-path smoke tests;
- generic-baseline and designed-build evaluation tests.

No test may depend on a live model unless it is explicitly tagged
`LiveProvider` and excluded from default CI.

## Visual implementation rules

- Build the map as semantic SVG, not canvas and not a grid of cards.
- Room geometry comes from mission presentation data.
- Keep simulation geometry topological; visual coordinates never affect rules.
- Animate canonical events through a presentation queue.
- Provide `prefers-reduced-motion`.
- Maintain readable contrast without relying on glow.
- Do not add raster textures until the SVG acceptance target has been reached.
- Do not copy another game’s interface or visual assets.

## Working method

Before editing:

1. read the assigned packet in `07_AGENT_BUILD_PLAN.md`;
2. inspect adjacent code and tests;
3. state assumptions in the task handoff;
4. identify the smallest complete vertical change.

Before handing off:

1. run the packet’s required commands;
2. report files changed;
3. report tests run and exact result;
4. identify any contract or scope deviation;
5. leave the repository buildable.

Do not silently weaken an acceptance criterion to make a test pass.

## Security and cost

- Secrets remain server-side.
- Default local development uses scripted or fake providers.
- Live calls require an explicitly configured provider.
- Enforce hard input/output token caps, timeout, retry, turn, run, user-day, and
  global-day budgets.
- When a budget is exhausted, stop cleanly and preserve a replayable partial
  run.
- Treat build text and model output as untrusted content.

## Completion rule

A packet is not complete because code exists. It is complete when its named
acceptance tests pass and the behavior can be demonstrated through the
smallest intended interface.
