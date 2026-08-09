# Directive Drift build status

Last updated: 2026-08-09

| Packet | Status | Evidence |
|---|---|---|
| P0 — Repository and guardrails | Complete | Solution/web/container acceptance commands pass. |
| P1 — Contracts and content boundary | Complete | Strict schemas, DTOs, loaders, stable validation errors, and content CLI pass. |
| P2 — Deterministic core simulator | Complete | Core rules, PCG32, observation, determinism, replay, terminal, and architecture tests pass. |
| P3 — Cold Start materializer, solver, and evaluation harness | Complete | Eleven fixed variants have ≤17-turn interchangeable-loadout and no-damage proofs; three named policy families and scripted onboarding fail/success gates pass. |
| P4+ | Not started | Application persistence, HTTP run advancement, live providers, and player certification UX remain deferred. |

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
- `dotnet test` — 109 passed, 0 failed, 0 skipped.
- `npm ci --prefix src/DirectiveDrift.Web` — passed.
- `npm run lint --prefix src/DirectiveDrift.Web` — passed with 0 warnings.
- `npm run test --prefix src/DirectiveDrift.Web` — 1 passed.
- `npm run build --prefix src/DirectiveDrift.Web` — passed.
- `docker build .` — passed; image
  `sha256:214b2c6d303aa8ed33c64c35bc16420731b46e88d57dc6410d0733e9d3bae3c1`.
- Cold Start content CLI — passed; content `2.0.1`, 11 variants proven.
- Generic scripted tutorial — failed as intended with
  `unknown-required-contract` and 0 decision fallbacks.
- Designed scripted tutorial — succeeded with 0 decision fallbacks.
- Designed pinned scripted matrix — 8/8 succeeded with 0 decision fallbacks.

P3-touched C# paths pass `dotnet format --verify-no-changes`. A repository-wide
format check still reports the pre-existing import order in
`tests/DirectiveDrift.Core.Tests/RunStartFactoryTests.cs`; P3 does not modify
that P2 file.
