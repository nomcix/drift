# DIRECTIVE DRIFT — Prioritized Backlog

This backlog translates the workpack into implementable stories. Packet IDs
refer to `07_AGENT_BUILD_PLAN.md`.

## P0 — Must ship

### Foundation

| ID | Packet | Story | Acceptance shorthand |
|---|---|---|---|
| FND-001 | P0 | Scaffold .NET 10 solution and strict projects | clean build |
| FND-002 | P0 | Scaffold React/Vite strict client | clean npm build |
| FND-003 | P0 | Enforce architecture references | forbidden reference test |
| FND-004 | P0 | Add CI and lockfiles | clean-clone green |
| FND-005 | P0 | Build single container | health endpoints pass |

### Contracts and content

| ID | Packet | Story | Acceptance shorthand |
|---|---|---|---|
| CNT-001 | P1 | Validate mission JSON Schema | canonical pass, malformed fail |
| CNT-002 | P1 | Validate build schema | all fixtures |
| CNT-003 | P1 | Validate decision schema | all invalid categories |
| CNT-004 | P1 | Map authoring DTOs to typed IDs | no label IDs |
| CNT-005 | P1 | Validate cross-references | stable errors |
| CNT-006 | P3 | Materialize fixed variants | golden content |
| CNT-007 | P3 | Validate topology/invariants | impossible variant rejected |
| CNT-008 | P3 | Solve all variants | ≤17-turn proof |

### Core simulation

| ID | Packet | Story | Acceptance shorthand |
|---|---|---|---|
| SIM-001 | P2 | Implement deterministic random | golden vectors |
| SIM-002 | P2 | Create immutable run state | no framework refs |
| SIM-003 | P2 | Build private observations | leakage unit cases |
| SIM-004 | P2 | Generate legal actions | capability cases |
| SIM-005 | P2 | Resolve simultaneous movement | stable events |
| SIM-006 | P2 | Resolve delayed messages | N→N+1 |
| SIM-007 | P2 | Implement generator commitment | all interrupts |
| SIM-008 | P2 | Implement console sync | same-turn pair only |
| SIM-009 | P2 | Implement archive/cargo/extraction | custody invariant |
| SIM-010 | P2 | Implement drone and radiation | deterministic threat |
| SIM-011 | P2 | Implement six modules | one-use rules |
| SIM-012 | P2 | Implement terminal state and score | exact fixtures |
| SIM-013 | P2 | Canonical events and state hash | replay equality |

### Persistence/API

| ID | Packet | Story | Acceptance shorthand |
|---|---|---|---|
| API-001 | P4 | Create secure guest profile | ownership test |
| API-002 | P4 | Save immutable build/version | used version locked |
| API-003 | P4 | Start a run | all versions snapshotted |
| API-004 | P4 | Enqueue idempotent turn | one op per turn |
| API-005 | P4 | Claim/recover operation | restart test |
| API-006 | P4 | Persist decision/state/events atomically | failure rollback |
| API-007 | P4 | Page events and load replay | ordered complete |
| API-008 | P4 | Publish OpenAPI/generate client | CI drift check |
| API-009 | P8 | Reserve and settle model cost | race/cap tests |

### Briefing workbench

| ID | Packet | Story | Acceptance shorthand |
|---|---|---|---|
| UI-001 | P5 | Present complete player briefing | no agent-context reuse |
| UI-002 | P5 | Edit shared doctrine | 240-char boundary |
| UI-003 | P5 | Edit private role orders | 160 each |
| UI-004 | P5 | Assign/duplicate/reorder cards | four exact slots |
| UI-005 | P5 | Equip distinct modules | one each |
| UI-006 | P5 | Show overlap/omission | descriptive only |
| UI-007 | P5 | Record build hypothesis | never enters runtime |
| UI-008 | P5 | Keyboard-complete workbench | Playwright |

### Map and replay

| ID | Packet | Story | Acceptance shorthand |
|---|---|---|---|
| MAP-001 | P6 | Render typed irregular room shapes | silhouette gate |
| MAP-002 | P6 | Render conduits and all states | traceable graph |
| MAP-003 | P6 | Render agent/drone/hazard tokens | non-color identity |
| MAP-004 | P6 | Reduce canonical events | final-state snapshots |
| MAP-005 | P6 | Animate movement/scan/repair | ordered queue |
| MAP-006 | P6 | Animate power propagation | visual payoff |
| MAP-007 | P6 | Animate sync/gate/cargo | objective clarity |
| MAP-008 | P6 | Add map lenses | no live truth leak |
| MAP-009 | P6 | Add pause/speed/instant | same final state |
| MAP-010 | P6 | Add reduced/readable modes | accessibility pass |
| RPL-001 | P7 | Replay exact run without provider | zero calls |
| RPL-002 | P7 | Surface diagnostic signals | evidence-linked |

### AI runtime

| ID | Packet | Story | Acceptance shorthand |
|---|---|---|---|
| AI-001 | P8 | Build exact private context | forbidden marker suite |
| AI-002 | P8 | Version prompt template | stable hash |
| AI-003 | P8 | Validate semantic decision | exact action membership |
| AI-004 | P8 | Retry formatting once | same knowledge |
| AI-005 | P8 | Deterministic wait fallback | event recorded |
| AI-006 | P8 | Run two calls concurrently | same pre-state |
| AI-007 | P8 | Fake latency/error provider | all cases |
| AI-008 | P8 | One live structured adapter | capped smoke run |
| AI-009 | P8 | Store usage/cost/context | replayable metadata |

### Mastery and launch

| ID | Packet | Story | Acceptance shorthand |
|---|---|---|---|
| MST-001 | P9 | Practice variant selection | revealed truth |
| MST-002 | P9 | Build/run comparison | first diff |
| MST-003 | P9 | Hidden three-run certification | 2/3, no leak |
| MST-004 | P9 | Assisted-run eligibility rule | cannot certify |
| MST-005 | P9 | Share result card | no secrets |
| OPS-001 | P11 | Production deploy and volume | health/backup |
| OPS-002 | P11 | Global spend kill switch | tested |
| OPS-003 | P11 | Private feedback instrumentation | privacy-reviewed |

## P1 — Should ship if P0 is stable

| ID | Story | Why |
|---|---|---|
| UX-101 | Side-by-side build text diff | accelerates revision |
| UX-102 | Message graph visualization | makes coordination legible |
| UX-103 | Replay turn scrubber | faster diagnosis |
| UX-104 | Build duplicate/rename/export/import | supports experimentation |
| UX-105 | Four mastery badges | light progression |
| UX-106 | Shareable replay link with expiry/privacy | acquisition loop |
| AUD-101 | Original SFX for key events | spatial payoff |
| AUD-102 | Ambient hum with power state | atmosphere |
| OPS-101 | Admin usage dashboard | private-test control |
| OPS-102 | Automated daily backup verification | resilience |
| RES-101 | In-product one-question failure survey | learning |

## P2 — Deliberately later

| ID | Story | Gate |
|---|---|---|
| FUT-201 | Second mission | repeat play validated |
| FUT-202 | Daily seed | certification stable |
| FUT-203 | Account linking/cloud sync | guest retention justifies |
| FUT-204 | Additional official model profile | comparison demand |
| FUT-205 | BYOK in self-hosted mode | security/support design |
| FUT-206 | Community build gallery | moderation/privacy ready |
| FUT-207 | Asynchronous PvP challenge | single-player mastery proven |
| FUT-208 | User-authored missions | content validator mature |
| FUT-209 | Steam build | demand and package plan |
| FUT-210 | Unity/native client investigation | 3D/direct-control need |

## Cut order

If schedule slips, cut in this order:

1. audio;
2. badges;
3. share-card automation;
4. side-by-side comparison polish;
5. practice-random UI;
6. extra fixed practice variants beyond those needed for evaluation;
7. live provider from the public build, retaining invite-only access.

Do not cut:

- partial-information boundary;
- deterministic core/replay;
- generic-baseline gate;
- spend caps;
- scripted onboarding;
- map visual identity;
- accessibility of primary interactions;
- content invariant solver.

## Definition of ready

A story is ready when:

- product behavior is specified;
- contract/events are named;
- dependency packet is merged;
- acceptance can run without a live provider unless story is explicitly live;
- no unresolved design decision materially changes implementation.

## Definition of done

- code and tests merged;
- acceptance evidence recorded;
- schemas/examples/docs updated if boundary changed;
- accessible and error states included;
- no secret/network needed in default CI;
- telemetry is privacy-reviewed;
- worktree is clean and repository remains buildable.
