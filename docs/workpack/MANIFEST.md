# Workpack Manifest

Version: `0.2.0`

## Product and design

| File | Purpose |
|---|---|
| `README.md` | Entry point, scope, build order, and completion bar |
| `00_PRODUCT_BRIEF.md` | Product thesis, audience, pillars, success metrics |
| `01_GAME_DESIGN.md` | Complete game loop and mechanics |
| `02_COLD_START_MISSION.md` | First mission, variants, cards, and content rules |
| `03_VISUAL_AND_MAP_SPEC.md` | Art direction, map system, animation, UX acceptance |

## Engineering and evaluation

| File | Purpose |
|---|---|
| `04_TECHNICAL_ARCHITECTURE.md` | C#/.NET and React architecture |
| `05_AI_RUNTIME.md` | Information boundary, prompt construction, provider loop |
| `06_EVALUATION_AND_QA.md` | Determinism, anti-shortcut, QA, and release gates |
| `07_AGENT_BUILD_PLAN.md` | Ordered, independently verifiable work packets |
| `08_BACKLOG.md` | Prioritized implementation stories |
| `09_LAUNCH_AND_MONETIZATION.md` | Bootstrap hosting, cost controls, launch plan |
| `10_DECISIONS_RISKS_AND_NAMING.md` | Locked choices, open gates, risk register |
| `AGENTS.md` | Root instructions for coding agents |
| `HANDOFF_PROMPT.md` | Copyable orchestration and packet prompts |

## Contracts and fixtures

| File | Purpose |
|---|---|
| `contracts/agent-decision.schema.json` | Valid AI decision envelope |
| `contracts/build.schema.json` | Valid player build |
| `contracts/mission.schema.json` | Valid authored mission |
| `examples/cold-start.mission.json` | Canonical mission fixture |
| `examples/designed-build.json` | Expected competent build fixture |
| `examples/generic-optimal-build.json` | Anti-shortcut baseline |
| `examples/agent-decision.example.json` | Example provider response |

## Visual reference

| File | Purpose |
|---|---|
| `visuals/map-style-concept.svg` | Code-native target frame for the mission map |
| `visuals/map-style-concept.png` | Rendered 1600×900 preview of the SVG target |
| `visuals/README.md` | How to inspect and translate the concept |

## Package integrity

The package is internally complete when:

- every path listed above exists;
- every JSON file parses;
- all three schemas validate as JSON Schema Draft 2020-12 documents;
- each example validates against its schema;
- Markdown links resolve;
- the SVG parses and has a `viewBox`;
- the PNG preview is 1600×900;
- the ZIP contains this directory at its root.

The implementation repository may copy these files into `docs/workpack/`, but
must preserve the version and commit the originals before interpreting them.
