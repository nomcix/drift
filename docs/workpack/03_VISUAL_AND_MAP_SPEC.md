# DIRECTIVE DRIFT — Visual and Map Specification

## 1. Answer to the art question

Yes: the v1 map can look distinctive and polished without a large custom-art
or rendering effort.

Use an authored SVG operations table, not a literal tile map:

- irregular room silhouettes imply station function;
- curved, double-line conduits replace square adjacency;
- power, message, scan, and threat states move through the same topology;
- agent tokens have shape, trail, posture, and color;
- lighting transforms as the station powers up;
- the event queue gives every deterministic rule a visual beat;
- subtle procedural grain and scanlines unify the frame.

This requires front-end design work, but essentially no asset pipeline. It is a
better fit for v1 than Unity, 3D models, tile sets, or commissioned
illustrations.

Custom art becomes useful later for:

- character portraits and expressive moments;
- mission splash images;
- station exterior/background plates;
- campaign biomes;
- trailers and store capsules;
- premium cosmetic themes.

The playable map itself can remain vector-first through launch.

## 2. Visual thesis

> A deep-space mission-control table crossed with a neon transit map and
> submarine sonar.

It should feel:

- spatial, not tabular;
- engineered, not generic cyberpunk;
- luminous because systems carry energy, not because every edge glows;
- tense but readable;
- compact enough to understand in one glance;
- alive even when no character sprite is present.

Do not reproduce another game’s HUD. The reference is a combination of real
systems language: transit topology, oscilloscope traces, orbital schematics,
and instrument-panel labeling.

## 3. Visual hierarchy

The desktop screen has three zones:

```text
┌──────────────┬─────────────────────────────────┬───────────────┐
│ BUILD / TEAM │                                 │ TURN / COMMS  │
│ compact rail │      STATION OPERATIONS MAP     │ context rail  │
│              │                                 │               │
├──────────────┴─────────────────────────────────┴───────────────┤
│ EVENT TIMELINE / playback / objective state                   │
└───────────────────────────────────────────────────────────────┘
```

The map owns at least 60% of the primary frame. Panels support it rather than
turning the product into an analytics dashboard.

## 4. Design tokens

### 4.1 Color

```css
--void-950: #03060c;
--void-900: #07101a;
--panel-850: #0a1420;
--panel-800: #0d1b29;
--line-dim: #183348;
--line: #2b5b70;
--text: #d8edf2;
--text-muted: #7896a2;
--kite: #58e6ff;
--wren: #ffad66;
--power: #80f0a7;
--warning: #ffd166;
--danger: #ff5477;
--archive: #b49aff;
--white: #f5feff;
```

Every semantic color also has a non-color channel:

- Kite: triangular token and `K` glyph;
- Wren: hexagonal token and `W` glyph;
- danger: diagonal hatch and pulse;
- power: solid inner conduit;
- unpowered: broken/dashed conduit;
- selected: double outline;
- objective ready: orbiting tick marks;
- disabled: collapsed token and crossbar.

### 4.2 Type

Use a locally bundled variable sans only if licensing is clear. The fallback
stack is:

```css
font-family: Inter, ui-sans-serif, system-ui, sans-serif;
```

Use a system monospace stack for IDs, telemetry, turn labels, and compact
numbers. Avoid decorative sci-fi fonts in body copy.

Typography roles:

- 11–12 px uppercase telemetry labels;
- 13–14 px secondary UI;
- 15–16 px primary controls;
- 18–24 px room/objective labels when space allows;
- 28–36 px title/result moments.

### 4.3 Shape language

- outer panels: clipped or gently chamfered corners;
- Landing Bay: open crescent/docking jaws;
- transit: tapered capsules and chevrons;
- Relay Nexus: concentric circular hub;
- Security Array: radar octagon;
- Auxiliary Reactor: broken concentric ring;
- consoles: opposing wedges;
- Archive Gate: vertical iris;
- Flight Archive: shielded asymmetric hexagon.

Never render every room with the same card component.

## 5. SVG architecture

Use one responsive root `<svg>` with a stable `viewBox`, rendered inside a
semantic React component. The SVG has ordered layers:

```text
00-definitions       gradients, masks, filters, reusable symbols
10-field             deep background, stars, grid, grain
20-structure         hull shadows and inactive conduits
30-network           power and communication conduit states
40-rooms             room silhouettes and labels
50-hazards           radiation, locks, drone sweep
60-objectives        repair, console, archive, extraction indicators
70-agents            tokens, trails, carried items
80-events            temporary pulses, path travel, damage rings
90-focus             keyboard focus, selection, inspection
```

Production code should keep long-lived state layers separate from transient
event animation. Do not mutate the DOM from the simulator.

### 5.1 Mission presentation data

Content supplies only safe typed values:

```json
{
  "roomId": "auxiliary-power",
  "shape": "reactor-ring",
  "anchor": { "x": 720, "y": 720 },
  "size": { "w": 148, "h": 112 },
  "rotation": 0,
  "labelPlacement": "below"
}
```

The client maps `shape` to trusted React/SVG components. Authored content does
not inject raw SVG or CSS.

Connections may provide waypoints for visual routing:

```json
{
  "connectionId": "junction-power",
  "waypoints": [[720, 500], [720, 650]]
}
```

Waypoints never affect movement rules.

## 6. Room component states

Each room supports:

- undiscovered by selected agent;
- discovered;
- locally observed this turn;
- occupied by one or both agents;
- occupied by drone;
- objective unavailable;
- objective available;
- objective in progress;
- objective complete;
- powered/unpowered;
- damaged;
- threatened;
- selected/focused.

State visual order:

1. base silhouette;
2. system fill;
3. objective ring;
4. threat treatment;
5. occupant tokens;
6. focus outline.

Labels remain readable at all states.

## 7. Connection component states

Each conduit supports:

- unknown;
- open/unpowered;
- open/powered;
- locked;
- Kite-only;
- contaminated;
- recently traversed;
- selected route;
- message in flight;
- drone path.

Visual treatments:

- topology base: dim double stroke;
- powered: green/cyan inner stroke with slow dash flow;
- contaminated: danger hatch knots and directional pulse;
- locked: crossbar glyph at midpoint;
- Kite-only: narrow cyan crawl line;
- recent movement: agent-color tracer lasting 700–1100 ms;
- message: compact luminous packet following the path;
- predicted intent: dotted line, never confused with accepted movement.

## 8. Agent tokens

Tokens are small vector emblems rather than character sprites.

### Kite

- triangular forward marker;
- split cyan/cool-white ring;
- scanning arc during recon;
- fast, lightly eased movement;
- cyan trail;
- `K` label in reduced-detail mode.

### Wren

- compact hexagon/wrench-notch marker;
- amber/white ring;
- rotating inner tool tick during repair;
- heavier movement easing;
- amber trail;
- `W` label in reduced-detail mode.

At normal zoom, orientation follows path tangent. On overlapping occupancy, the
tokens offset around the room anchor. Agent identity cannot depend only on
color.

## 9. Threat language

### Radiation

- three small danger diamonds on the affected conduit;
- low-frequency transverse ripple;
- diagonal hatch visible without animation;
- warning tone only when first sensed/traversed;
- no full-screen red wash.

### Drone

- red diamond/octagonal token;
- translucent radar cone or sweep clipped to current room;
- one-turn motion ghost showing where it came from, not where it will go;
- collision: brief expanding ring, token recoil, and event caption;
- no humanoid enemy art required.

## 10. Power transformation

Power restoration is the key visual payoff in the first mission.

Before:

- rooms sit near-black with sparse edge light;
- conduits are broken/dashed;
- console labels flicker at low opacity;
- archive iris is a cold vertical seam.

On `PowerRestored`:

1. reactor ring closes and flashes;
2. a green-white pulse travels reactor → nexus;
3. two pulses branch to Alpha and Beta;
4. room fills rise from 8% to 18% luminance;
5. objective rings appear;
6. ambient hum layer fades in;
7. the timeline records the event after the pulse lands.

Total choreography: 1.4–2.0 seconds at 1×. Reduced-motion mode applies the end
state immediately and uses one 150 ms opacity transition.

## 11. Event choreography

The client reduces canonical events into presentation state and enqueues
animations. It never decides outcomes.

| Event | Primary beat | Duration target |
|---|---|---:|
| move | token follows conduit + trail | 650 ms |
| scan | two expanding clipped arcs | 800 ms |
| message queued | packet departs token to side rail | 450 ms |
| message delivered | packet returns from rail to recipient | 500 ms |
| repair started | segmented progress ring 1/2 | 550 ms |
| repair interrupted | ring shears and drains | 500 ms |
| power restored | network propagation | 1700 ms |
| console activation | wedge fills and emits half-wave | 600 ms |
| sync success | waves meet at gate; iris opens | 1300 ms |
| damage | threat ring + token recoil | 450 ms |
| pickup | archive shard nests under token | 500 ms |
| success | route backlight + objective lockup | 1800 ms |

At 2×, durations halve with a floor of 120 ms. “Resolve instantly” applies
events in order with no transient animation.

## 12. Information views

The same map has three explicit lenses:

- **Command view:** player-known topology and current public events;
- **Kite view:** rooms/connections known in Kite’s observation history;
- **Wren view:** Wren’s corresponding truth.

Changing lens must visibly alter unknown regions and the right-side context
panel. Never reveal hidden engine truth during a live certification run.

Post-run replay adds **Truth view**, which reveals materialized hazards, patrol,
and both contexts.

## 13. Workbench visual design

The build screen uses the map as a dim background schematic. Briefing cards
appear as physical data slivers connected to Kite or Wren by thin lines.

Required interactions:

- drag or keyboard-assign a card to a slot;
- duplicate with one action;
- remove and reorder;
- compare what only Kite, only Wren, both, or neither know;
- show a warning for unassigned required contracts without forbidding the run;
- show character budgets in place;
- equip one module per agent;
- name and version the build;
- record the optional prediction.

An “information overlap” meter is descriptive:

```text
SPECIALIZED 5     SHARED 2     OMITTED 3
```

It does not rate the build as good or bad.

## 14. Responsive behavior

Primary target: desktop browser at 1280×720 or larger.

- ≥1280 px: three-zone layout.
- 960–1279 px: right rail becomes a drawer; map remains central.
- 720–959 px: build and run screens stack; map pans inside fixed aspect frame.
- <720 px: replay/read-only support is acceptable in v1; build editing may
  display a desktop recommendation.

Do not shrink map labels below readability to claim mobile support.

## 15. Accessibility

- Rooms are focusable logical buttons only when interactive.
- Each room has an accessible label summarizing public state.
- An adjacent structured list offers the same room/event information.
- Keyboard movement follows visual topology, not arbitrary DOM order.
- Focus stroke is white plus dark outline.
- `prefers-reduced-motion` disables path travel and pulsing.
- Readable mode removes noise, scanlines, and large blurs.
- WCAG AA contrast applies to text and essential indicators.
- Sound never carries unique information.

## 16. Performance budget

Desktop target:

- 60 fps during token and conduit animation on a midrange integrated GPU;
- first SVG interactive within the page-load target;
- fewer than 25 simultaneous animated elements;
- no full-map animated blur filter;
- blur/glow bounds kept local;
- no canvas redraw loop when state is idle;
- DOM node target below 900 for the playfield;
- animation driven by CSS transforms, opacity, stroke dash, or Web Animations.

Use Chrome performance profiling before adding decorative filters.

## 17. Asset budget

Required custom raster art for vertical slice: **zero**.

Recommended optional assets:

- one subtle generated or procedural star/noise texture under 150 KB;
- 8–12 short original sound effects;
- one ambient loop;
- one legally licensed local font subset;
- favicon and social share image derived from SVG.

Do not block the functional slice on any optional asset.

## 18. Visual acceptance gate

The primary run screen passes only if:

- an unprompted tester describes it as a station/map rather than boxes or a
  dashboard;
- at least six room functions are distinguishable by silhouette alone;
- a tester can trace open connectivity in under ten seconds;
- agent identity, danger, power, and objective readiness remain legible in
  grayscale or through non-color cues;
- power restoration visibly changes the whole station;
- every canonical event can resolve instantly without losing state;
- reduced-motion mode remains clear;
- no presentation coordinate changes a simulation result;
- the concept in `visuals/map-style-concept.svg` is met or intentionally
  improved.

## 19. What custom art can wait for

After mechanical validation, the highest-value art order is:

1. store capsule and key art;
2. Kite/Wren portraits with 4–6 expressions;
3. mission exterior plate;
4. enhanced icon family;
5. additional station themes;
6. only then consider 2D room illustrations or 3D.

Unity becomes worth reevaluating only if the proven product needs spatial
physics, direct movement, dense character animation, 3D cameras, or
console-native distribution. None is required to test the current hook.
