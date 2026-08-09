# DIRECTIVE DRIFT — Decisions, Risks, and Naming

## 1. Locked decisions

These are defaults for the vertical slice. Changing one requires an accepted
ADR or product decision.

| ID | Decision | Reason |
|---|---|---|
| D-001 | Single-player first | validates core loop before networking |
| D-002 | Two asymmetric autonomous agents | enough coordination depth, legible |
| D-003 | Player sees full public mission; agents get allocated partial truth | core information game |
| D-004 | Four briefing slots per agent | forces omission/duplication tradeoff |
| D-005 | 240-char doctrine, 160-char private roles | constrained authorship |
| D-006 | No normal mid-run prompt | preparation remains the game |
| D-007 | Simultaneous turns and delayed finite messages | coordination has teeth |
| D-008 | Engine provides legal action IDs | model cannot invent mechanics |
| D-009 | Deterministic C# engine judges all outcomes | fair replay/evaluation |
| D-010 | .NET 10 LTS backend/core | C# preference and long support |
| D-011 | React/TypeScript browser UI | fastest fit for workbench and sharing |
| D-012 | SVG map, not Unity | distinct v1 visuals without engine/art overhead |
| D-013 | SQLite/single container initially | cheapest reliable bootstrap |
| D-014 | Scripted/fake first, one live provider later | testability and cost |
| D-015 | Free limited prototype | maximize learning and access |
| D-016 | One mission with variants | depth before content breadth |

## 2. Open decisions and gates

| ID | Decision | Owner | Due | Evidence |
|---|---|---|---|---|
| O-001 | first live model/profile | engineering/product | P8 | structured reliability, cost, latency |
| O-002 | exact host | engineering | P11 | current price, volume, region, backups |
| O-003 | public live-credit grant | product | before Phase 3 | alpha cost/abuse/retention |
| O-004 | supporter pricing | product | after 50 testers | unit economics and demand |
| O-005 | final name clearance | founder/legal | before public store push | trademark/domain/store search |
| O-006 | audio scope | creative | after P7 | map comprehensibility without it |
| O-007 | account system | product | post-prototype | retention/cloud-sync demand |
| O-008 | second mission | product/design | post-validation | another-mission intent and content cost |

## 3. Technology decision: no Unity in v1

Unity is viable C# technology, but not the best first implementation for this
specific game shape.

React/SVG wins v1 on:

- text/card editing;
- responsive browser distribution;
- DOM accessibility;
- API integration;
- replay timelines;
- automated UI testing;
- deployment size;
- iteration without an asset/editor pipeline.

Unity wins later if the design pivots to:

- direct spatial control;
- physics;
- 3D rooms;
- character animation;
- console-native distribution;
- local GPU-heavy simulation.

Do not maintain parallel Unity and web clients.

Godot is not the compromise for this C# browser target: current Godot 4
documentation states that C# projects cannot export to the web. See the
[official web export documentation](https://docs.godotengine.org/en/4.x/tutorials/export/exporting_for_web.html).

## 4. Working name

Selected prototype name: **DIRECTIVE DRIFT**

Why it fits:

- `Directive` names the player-authored command layer;
- `Drift` names the gap between intent and local autonomous behavior;
- the alliteration is memorable;
- it supports visual language such as drift trace, drift point, and stable
  directive;
- it does not lock the fiction to one station or military campaign.

Primary tagline:

> Design the briefing. Survive the drift.

Secondary descriptor:

> An autonomous-team strategy game.

Repository/product slug:

```text
directive-drift
```

Namespace:

```text
DirectiveDrift
```

## 5. Preliminary name check

A July 2026 exact-title web search did not surface an existing video game named
“Directive Drift” on general results, Steam-targeted results, or itch-targeted
results.

This is only preliminary product research. It is not:

- trademark clearance;
- company-name clearance;
- domain or social-handle reservation;
- legal advice;
- proof that a conflicting unannounced or regional mark does not exist.

Before a store page, paid marketing, or incorporation around the name:

1. search major game stores again;
2. search relevant trademark databases/classes in intended markets;
3. search company registries;
4. inspect domains and social handles;
5. check phonetic and translated conflicts;
6. consult qualified counsel if commercial exposure justifies it;
7. record the final name in an ADR.

Do not spend heavily on a logo before this gate.

## 6. Alternate names

Retain only as fallback:

1. `Causal Command`
2. `Blackbox Doctrine`
3. `Split Directive`

Do not return to `RELAY`: it is generic, crowded, and an exact game titled
Relay has appeared in store search.

## 7. Risk register

Ratings: likelihood (L) and impact (I), 1 low to 5 high.

| Risk | L | I | Early signal | Mitigation / trigger |
|---|---:|---:|---|---|
| Generic optimal build succeeds | 4 | 5 | ≥25% gate missed | inspect leakage; tighten dependency/card budget; do not ship |
| Players watch but do not revise | 4 | 5 | low second-build rate | improve prediction, diagnosis, comparison; reconsider hook |
| Failure feels like model randomness | 4 | 5 | tester cannot name cause | deterministic signals, pinned profile, wider margins |
| Information boundary leaks | 3 | 5 | forbidden marker in context | serializer tests and independent review; S1 |
| Model spend spikes | 3 | 5 | cost/run or retries rise | hard reservations/caps/kill switch |
| Provider deprecates model | 3 | 4 | notice or behavior drift | profile versioning, adapter, re-evaluation |
| SVG map becomes UI time sink | 3 | 4 | P6 blocks walking skeleton | meet concept with limited shapes/effects; cut optional polish |
| Map is attractive but misleading | 2 | 5 | visual vs event disagreement | presentation reducer from canonical events; tests |
| C#/TS contract drift | 3 | 4 | hand-written client types | OpenAPI generation and CI drift test |
| SQLite concurrency failure | 2 | 4 | claim/lock errors | one instance, WAL, short transactions; migrate at trigger |
| Agent coding causes broad inconsistent refactor | 4 | 3 | overlapping packet changes | small packets, integrator ownership, architecture tests |
| One mission is memorized | 4 | 4 | route scripts dominate | variants and private observations; held-out evaluation |
| Certification reflects model luck | 3 | 4 | high same-build variance | three runs, wider design margins, environment label |
| Onboarding feels like prompt work | 3 | 4 | typing abandonment | card-first interaction, provided examples, low character load |
| Free users exceed budget | 3 | 5 | cap reached early | invite credits, scripted fallback, global cap |
| BYOK creates support/security burden | 2 | 4 | key requests before retention | defer to self-hosted mode |
| Name conflict emerges | 2 | 4 | store/trademark hit | provisional branding and fallback list |
| Scope expands to campaign/multiplayer | 4 | 4 | backlog before loop proof | P2 backlog and explicit validation gate |

## 8. Technical migration triggers

### SQLite to PostgreSQL

Trigger when any two occur:

- more than one app instance required;
- sustained concurrent operation claims cause lock errors;
- backups/restore exceed acceptable downtime;
- database grows beyond easy volume management;
- account/query features require richer concurrency.

### React/SVG to a game engine

Trigger an investigation, not an automatic rewrite, when:

- validated design requires direct movement or physics;
- 3D spatial reasoning becomes the dominant fun;
- console distribution has proven business value;
- map DOM/performance cannot meet measured requirements after profiling;
- campaign art production already requires an engine pipeline.

### One live provider to multiple

Trigger when:

- availability materially harms runs;
- provider price threatens margin;
- model comparison is requested by players;
- regional/privacy requirements differ.

Do not add providers merely for technical elegance.

## 9. Scope change test

Before accepting a feature, answer:

1. Does it strengthen build → run → diagnose → revise?
2. Can scripted mode test it?
3. Does it preserve agent partial knowledge?
4. Does it preserve deterministic judgment/replay?
5. Does it fit one mission?
6. Is its ongoing model/content/ops cost bounded?
7. What existing item is cut?

If answers are weak, put it in P2.

## 10. Product kill/pivot criteria

After two serious prototype iterations, pivot or stop if:

- fewer than 30% of qualified testers intentionally revise;
- the generic baseline cannot be kept weak without opaque tricks;
- players consistently ask to control units directly;
- robust play requires so much text that it becomes work;
- live provider variance erases build effects;
- cost per retained player cannot support any plausible offer;
- a second mission appears more expensive than the interest it would unlock.

A technically impressive multi-agent demo is not enough reason to continue.
