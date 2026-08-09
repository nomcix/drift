# DIRECTIVE DRIFT — Fresh-Start Build Workpack

Version: `0.2.0`  
Status: build-ready vertical-slice specification  
Prepared: 2026-07-26

## The decision

Build **Directive Drift** as a browser-first, single-player strategy game about
designing the briefings, standing orders, and information topology of a
two-agent autonomous team.

The player knows the entire mission. Each agent receives only:

- a short shared doctrine;
- its own short role order;
- four selected briefing cards;
- local observations;
- its own memory;
- delayed messages from its partner;
- the legal actions available this turn.

The player does not prompt agents during execution. Both agents choose
simultaneously. The deterministic game engine—not an AI model—resolves the
turn and judges success.

This creates the core game:

> You are not telling an AI what to do next. You are designing a command system
> that must keep working after contact with incomplete information.

## Why this replaces the RELAY workpack

This package is a clean revision, not a patch. It changes three foundational
choices:

1. **Gameplay:** scarce briefing loadouts make generic “act optimally” prompts
   strategically incomplete.
2. **Technology:** the authoritative simulator and API are C#/.NET; the
   browser workbench is React/TypeScript.
3. **Presentation:** the map is an illustrated SVG operations table with
   irregular room silhouettes, conduits, scanning, glow, state animation, and
   event choreography—not a grid of generic squares.

Do not merge the old RELAY architecture into this one. Start a new repository
from this workpack.

## What to build

The vertical slice contains:

- one mission, **Cold Start**;
- two agents, Kite and Wren;
- one briefing/loadout workshop;
- one animated mission map;
- one deterministic 18-turn simulation;
- scripted, fake, and live AI decision providers;
- six revealed practice variants;
- three hidden certification variants;
- replay, comparison, scoring, and failure diagnosis;
- local guest saves;
- strict spend controls.

Not in v1:

- multiplayer;
- Unity;
- a 3D world;
- user-authored missions;
- freeform mid-run prompting;
- agent-generated actions outside the legal action list;
- model-based scoring;
- ranked competition;
- mobile-native applications;
- a live-service economy.

## Read order

1. [00_PRODUCT_BRIEF.md](00_PRODUCT_BRIEF.md)
2. [01_GAME_DESIGN.md](01_GAME_DESIGN.md)
3. [02_COLD_START_MISSION.md](02_COLD_START_MISSION.md)
4. [03_VISUAL_AND_MAP_SPEC.md](03_VISUAL_AND_MAP_SPEC.md)
5. [04_TECHNICAL_ARCHITECTURE.md](04_TECHNICAL_ARCHITECTURE.md)
6. [05_AI_RUNTIME.md](05_AI_RUNTIME.md)
7. [06_EVALUATION_AND_QA.md](06_EVALUATION_AND_QA.md)
8. [07_AGENT_BUILD_PLAN.md](07_AGENT_BUILD_PLAN.md)
9. [08_BACKLOG.md](08_BACKLOG.md)
10. [09_LAUNCH_AND_MONETIZATION.md](09_LAUNCH_AND_MONETIZATION.md)
11. [10_DECISIONS_RISKS_AND_NAMING.md](10_DECISIONS_RISKS_AND_NAMING.md)
12. [HANDOFF_PROMPT.md](HANDOFF_PROMPT.md)

Machine-readable material:

- `contracts/agent-decision.schema.json`
- `contracts/build.schema.json`
- `contracts/mission.schema.json`
- `examples/cold-start.mission.json`
- `examples/designed-build.json`
- `examples/generic-optimal-build.json`
- `examples/agent-decision.example.json`

Visual target:

- `visuals/map-style-concept.svg`
- `visuals/map-style-concept.png`
- `visuals/README.md`

## Source-of-truth precedence

When files disagree, use this order:

1. accepted architecture decision recorded in the implementation repository;
2. JSON contracts;
3. mission example;
4. numbered specifications;
5. backlog;
6. illustrative prose.

Gameplay changes that weaken the anti-shortcut acceptance test require explicit
product approval.

## Build path in one page

The optimal AI-assisted path is a sequence of small, verifiable packets:

1. repository, CI, linting, and architecture-boundary checks;
2. contracts and content validation;
3. pure deterministic simulator with scripted decisions;
4. Cold Start reference solver and generic-baseline harness;
5. ASP.NET Core application API and SQLite persistence;
6. React workbench and static SVG map;
7. event-driven map animation and replay;
8. fake provider, then live provider behind the same interface;
9. certification variants, spend controls, and diagnostics;
10. accessibility, browser QA, packaging, and deploy.

Each packet in `07_AGENT_BUILD_PLAN.md` is independently reviewable. An agent
must finish its tests and handoff notes before the next packet starts. Do not
ask one coding agent to “build the whole game” in a single run.

## Definition of vertical-slice complete

The slice is complete only when all of the following are true:

- a new player can create a build and launch a run without developer help;
- the same seed plus the same validated decisions produces an identical event
  log and score;
- the full mission is never present in either agent’s runtime context;
- the generic-optimal baseline clears no more than 25% of the certification
  pool over the pinned evaluation matrix;
- at least one human-authored designed build clears at least 70%;
- no model call can create, modify, or judge simulation state directly;
- the live-provider path has per-run, per-day, and global spend limits;
- a failed run can be replayed and its decisive failure understood;
- the primary desktop map meets the visual acceptance bar in
  `03_VISUAL_AND_MAP_SPEC.md`;
- all automated tests and the manual acceptance script pass.

## Product test

The prototype answers one question:

> Is designing an information-constrained autonomous team rewarding enough that
> players voluntarily revise, rerun, and compare builds for at least an hour?

If players admire the AI once but do not revise a build, the prototype has
failed even if the technology works.

## Working title

**Directive Drift** is a provisional product name selected after a preliminary
exact-title search. It is not trademark clearance. Preserve the product slug
`directive-drift` until the naming gate in `10_DECISIONS_RISKS_AND_NAMING.md`.

## Elevator pitch

**Directive Drift is a strategy game where you never control the units—you
design what two autonomous agents know, what they value, and how they
coordinate, then watch your command system survive an emergency it can only
partially see.**
