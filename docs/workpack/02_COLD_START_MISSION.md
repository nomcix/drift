# DIRECTIVE DRIFT — Cold Start Mission

Content ID: `cold-start`  
Content version: `2.0.0`  
Rules version: `dd-rules-1`  
Score version: `cold-start-score-2`

## 1. Mission purpose

Cold Start is the complete vertical-slice content set. It must support an
onboarding failure, repeated practice, build comparison, and a meaningful
three-variant certification.

It tests:

1. asymmetric information allocation;
2. recon-to-engineer hazard communication;
3. a two-turn engineering commitment;
4. synchronized independent actions;
5. custody and extraction planning;
6. adaptation to a hidden but fair route/threat mutation.

The mission is not solved by a secret phrase. Several role allocations should
work, but they must establish shared timing and route contingencies.

## 2. Player-facing briefing

> A solar surge has stranded research station Orison on emergency power.
> Autonomous units Kite and Wren have eighteen turns before the archive
> bulkhead seals permanently.
>
> Restore auxiliary power. Activate both archive consoles together. Recover
> the flight recorder and extract both units through Landing Bay.
>
> The station’s security drone is still patrolling, and radiation has
> contaminated at least one service link. Kite can scout hazards. Wren can
> repair major systems. Communication is delayed.

The player sees the complete four-contract objective tree, all authored
briefing cards, agent capabilities, base station topology, and taught rules.
Practice variants reveal their mutation. Certification variants do not.

The full prose above is never placed into either agent context.

## 3. Facility topology

Coordinates and silhouettes are presentation data only. Rules use explicit
connections.

| Room ID | Label | Visual archetype | Anchor | Tags | Initial content |
|---|---|---|---|---|---|
| `landing-bay` | Landing Bay | open docking crescent | 145,430 | extraction, safe | Kite |
| `west-hall` | West Transit | tapered corridor | 390,390 | transit | none |
| `maintenance-alcove` | Maintenance | compact service hex | 325,660 | safe, start | Wren |
| `east-hall` | Service Spine | angled conduit room | 520,610 | transit, service | none |
| `junction` | Relay Nexus | circular hub | 720,430 | transit | none |
| `security-hub` | Security Array | radar octagon | 710,170 | security | drone |
| `auxiliary-power` | Auxiliary Reactor | broken generator ring | 720,720 | machinery | generator |
| `console-alpha` | Console Alpha | upper control wedge | 1020,300 | objective, console | Alpha |
| `console-beta` | Console Beta | lower control wedge | 1020,560 | objective, console | Beta |
| `archive-threshold` | Archive Gate | narrow iris | 1210,430 | locked, objective | bulkhead |
| `archive` | Flight Archive | shielded black-box chamber | 1415,430 | objective | recorder |

### 3.1 Base connections

All connections are bidirectional unless a materialized mutation says
otherwise.

| Connection ID | A | B | Initial state | Notes |
|---|---|---|---|---|
| `landing-west` | Landing Bay | West Transit | open | main route |
| `landing-maintenance` | Landing Bay | Maintenance | open | start link |
| `west-junction` | West Transit | Relay Nexus | open | hazard eligible |
| `maintenance-service` | Maintenance | Service Spine | open | start route |
| `service-junction` | Service Spine | Relay Nexus | open | hazard eligible |
| `junction-security` | Relay Nexus | Security Array | open | patrol route |
| `junction-power` | Relay Nexus | Auxiliary Reactor | open | repair route |
| `junction-alpha` | Relay Nexus | Console Alpha | open | objective route |
| `junction-beta` | Relay Nexus | Console Beta | open | objective route |
| `junction-gate` | Relay Nexus | Archive Gate | open | approach |
| `gate-archive` | Archive Gate | Flight Archive | locked | opens on sync |
| `service-security-crawl` | Service Spine | Security Array | open | Kite only |

### 3.2 Topology invariants

Every valid variant must preserve:

- a Kite-traversable route from start to both consoles, archive, and Landing
  Bay;
- a Wren-traversable route from start to generator, both consoles, archive,
  and Landing Bay;
- a route that can move the recorder to Landing Bay;
- no mandatory traversal with more unavoidable damage than available health;
- one no-damage reference solution in at most 17 turns;
- at least two valid console role assignments;
- at least two safe timing windows for console synchronization;
- no requirement for one specific support module;
- no hidden rule that contradicts a briefing card.

Content validation rejects a variant when the reference solver cannot prove
these invariants.

## 4. Initial state

At turn zero:

- Kite is active in Landing Bay with 2 health;
- Wren is active in Maintenance with 2 health;
- the generator is `damaged`;
- Alpha is `operational_unpowered`;
- Beta is `operational_unpowered`, unless the variant damages it;
- the archive gate is `locked`;
- the flight recorder is `secured`;
- the drone is at its materialized patrol start;
- one or more hazard connections are materialized;
- both agent memories are empty;
- team message budget is 6 plus module effects;
- turn is 0;
- deadline is the end of turn 18.

## 5. Briefing-card deck

Card text is normative and must match `examples/cold-start.mission.json`.

### `power-contract`

**AUXILIARY POWER**

> The archive systems need the Auxiliary Reactor online. Wren can repair its
> major generator; normal repair takes two consecutive interactions.

Tags: `required`, `objective`, `wren`, `commitment`

### `sync-contract`

**DUAL AUTHORIZATION**

> After power is online, different active units must activate Alpha and Beta on
> the same turn. An unmatched activation resets with no progress.

Tags: `required`, `objective`, `coordination`

### `recovery-contract`

**RECORDER CUSTODY**

> Successful console sync opens the Archive. One unit must enter, pick up the
> flight recorder, and carry it out. Damage can force a drop.

Tags: `required`, `objective`, `cargo`

### `extraction-contract`

**EXTRACTION CONDITION**

> Before turn 18 ends, both active units and the flight recorder must be in
> Landing Bay. Partial extraction is failure.

Tags: `required`, `objective`, `deadline`

### `kite-sensor-intel`

**RECON PACKAGE**

> Kite senses radiation on adjacent links and can scan adjacent rooms. Wren
> cannot identify a contaminated link before local exposure or a warning.

Tags: `intel`, `kite`, `hazard`

### `drone-intel`

**SECURITY DRONE**

> The drone follows a fixed patrol. Sharing its room after threat movement
> causes one damage; a hit may interrupt work or drop carried cargo.

Tags: `intel`, `threat`, `timing`

### `comms-intel`

**DELAYED COMMS**

> The team has six messages. A message sent on turn N arrives before decisions
> on turn N+1. State the intended sync turn early.

Tags: `intel`, `communication`, `coordination`

### `route-intel`

**SERVICE SCHEMATIC**

> Standard links can be service-locked. Kite alone can traverse the crawlspace
> between Service Spine and Security Array; neither unit can force a lock.

Tags: `intel`, `route`, `kite`

### `repair-protocol`

**COMMITMENT SAFETY**

> Moving, switching primary action, or taking damage interrupts a major repair
> and resets its progress. Confirm a safe window before starting.

Tags: `protocol`, `wren`, `commitment`, `threat`

### `efficiency-protocol`

**PERFORMANCE TERMS**

> Preserve health, messages, time, and module charges. Failed console sync and
> interrupted major repair each lose a score bonus but do not alone end a run.

Tags: `optional`, `score`, `efficiency`

### 5.1 Allocation design test

With eight total slots across two agents and ten available cards:

- at least two cards are omitted if none are duplicated;
- duplicating a critical coordination card omits an additional unique fact;
- all four required contracts cannot be duplicated;
- a role order can compress missing content at the cost of procedural detail.

No default allocation is preselected after onboarding.

## 6. Device state machines

### 6.1 Generator

```text
damaged
  -> repairing_1
  -> online
```

Only Wren receives repair actions. `repairing_1` returns to `damaged` if Wren:

- moves;
- takes damage;
- chooses another primary action;
- becomes disabled.

Rapid Repair Kit changes `damaged` directly to `online` once.

### 6.2 Consoles

```text
damaged | operational_unpowered
  -> ready
  -> activated_this_turn
  -> ready
```

- `ready` requires power and an undamaged console.
- Wren may repair a damaged console in one interaction.
- A pair of `activated_this_turn` actions by different active agents on Alpha
  and Beta opens the gate.
- One activation emits `ConsoleSyncFailed` and both consoles return to ready.
- Once the gate opens it remains open.

### 6.3 Flight recorder

```text
secured -> available -> carried <-> dropped -> extracted
```

- Gate opening changes `secured` to `available`.
- A unit in Archive can pick up the available or dropped recorder.
- Only one mission item may be carried by an agent.
- Damage drops it in the post-move room unless Cargo Clamp triggers.
- Landing Bay objective evaluation changes it to `extracted`.

## 7. Legal action catalogue

Action IDs are generated from state and include a stable category prefix:

- `move:<room-id>`;
- `scan:<room-id>` for Kite;
- `repair:generator`;
- `continue-repair:generator`;
- `repair:console-alpha`;
- `repair:console-beta`;
- `activate:console-alpha`;
- `activate:console-beta`;
- `pickup:flight-recorder`;
- `deploy:decoy-beacon`;
- `wait`.

Passive modules do not create an action. The exact legal action objects include
ID, public label, concise consequence, and target ID. The AI may select only
the ID.

## 8. Observation rules

Both agents observe:

- current room label and tags that are locally visible;
- current health, carried item, module state, and commitment;
- traversable exits from current room;
- entities in current room;
- delivered messages;
- remembered discoveries;
- objective facts present in their own cards/orders/memory;
- exact legal actions.

Kite additionally observes:

- radiation status of adjacent connections;
- scan results on use;
- crawlspace eligibility.

Wren additionally observes:

- machinery diagnosis in current room;
- repair progress and interruption risk;
- damaged console state when local.

Neither observes:

- the other agent’s current room unless local or communicated;
- undelivered messages;
- hidden hazard placement outside sensing;
- future patrol positions;
- cards or role order assigned to the other agent;
- authoritative objective completion beyond locally visible or delivered
  event summaries.

Both receive public event summaries only for station-wide events such as
`PowerRestored`, `ArchiveOpened`, and `AgentDisabled`.

## 9. Threat model

### 9.1 Radiation

Radiation is an authored mutation attached to a connection.

- traversal causes one damage;
- Kite senses it while adjacent;
- Hazard Shield prevents one damage and is consumed;
- a disabled result terminates the mission;
- radiation does not move during a run.

### 9.2 Drone patrol

The materialized variant stores a cyclic ordered list of room IDs and an index.

During threat phase:

1. active beacon override, if any, selects the next beacon-directed step;
2. otherwise advance one patrol index;
3. emit `DroneMoved`;
4. damage active agents in the destination room;
5. apply interruption and cargo rules;
6. emit all resulting events in stable agent ID order.

The route is deterministic after variant materialization. It is not model
controlled.

## 10. Practice variants

### `cs-practice-01` — Split Warning

- hazard: `service-junction`;
- no damaged console;
- drone route:
  `security-hub, junction, console-alpha, junction`;
- no service lock.

Lesson: Kite must warn Wren or allocate hazard knowledge sensibly.

### `cs-practice-02` — Second Repair

- hazard: `west-junction`;
- Beta starts damaged;
- slower drone route:
  `security-hub, junction, west-hall, junction, console-alpha, junction`;
- no service lock.

Lesson: schedule two kinds of engineering work without losing sync.

### `cs-practice-03` — Rotated Watch

- hazard: `service-junction`;
- no damaged console;
- base patrol is rotated to start at `console-alpha`;
- no service lock.

Lesson: do not memorize the first patrol phase.

### `cs-practice-04` — Broken Intake

- hazard: `west-junction`;
- `landing-west` is service-locked;
- crawlspace open to Kite;
- patrol uses Security Array and Relay Nexus.

Lesson: do not hard-code the first route. Kite must enter through Maintenance
and can use the crawlspace as a timing alternative while Wren still has a safe
Service Spine route.

### `cs-practice-05` — Tight Window

- hazard: `west-junction`;
- Alpha starts damaged;
- drone visits both console rooms;
- no service lock.

Lesson: plan repair, rendezvous, and threat timing.

### `cs-practice-random`

Materialize from the safe mutation catalogue while revealing the seed and
mutations. Use only combinations proven by solver and content tests.

## 11. Certification pool

Certification selects three without replacement from a server-held pool. The
initial pool contains at least six materialized variants, including:

- one damaged-console variant;
- one service-lock variant;
- one shifted-patrol variant;
- one with west radiation;
- one with east radiation;
- one combining a repair detour and patrol timing change.

Constraints:

- no certification variant introduces a new mechanic;
- each has a solver proof at 17 turns or fewer;
- none requires a specific module;
- fixed content seed is recorded but hidden until the certificate completes;
- a player build version sees the same three variants if a suspended
  certificate resumes;
- pool changes increment certification version.

## 12. Reference strategy families

Content tests must retain at least three families.

### Recon courier

Kite scouts routes, coordinates the far console, retrieves the recorder, and
returns. Wren restores power and holds a safe console window.

### Engineer courier

Kite scouts and stages at Alpha. Wren restores power, repairs any damaged
console, syncs, then retrieves while Kite begins the return route.

### Beacon window

The team uses Decoy Beacon to create a stable repair or courier window, trades
one action for lower communication/timing pressure, and splits extraction.

The solver may implement abstract policies rather than model language. The
reference exists to prove fairness, not to prescribe player wording.

## 13. Failure signatures

| Signature | Event evidence | Likely revision |
|---|---|---|
| `unknown-required-contract` | an agent repeatedly passes a locally available required interaction without card/order/message evidence | reallocate or communicate objective |
| `late-sync-message` | intended turn arrives after partner selected action | announce at least one turn earlier |
| `split-sync-model` | both agents name or act on different sync turns | define proposer/ack protocol |
| `repair-interrupted` | commitment reset by move/action/damage | allocate protocol or create safe window |
| `hazard-not-routed` | Kite sensed hazard, Wren traversed before warning arrived | scout earlier or duplicate hazard policy |
| `cargo-owner-conflict` | both route to Archive while extraction lane is uncovered | assign custody/fallback |
| `local-shortest-path` | agent takes known risky link despite safe mission time | make survival priority explicit |
| `message-exhaustion` | no budget at required sync or route warning | compress protocol and conserve |
| `illegal-provider-action` | decision validation fallback used | provider/runtime issue, not build strategy |

The UI may say “likely,” because declared rationales and event correlations do
not prove internal model causality.

## 14. Presentation events

Canonical events required by the client:

- `RunStarted`;
- `TurnStarted`;
- `MessageDelivered`;
- `AgentDecisionAccepted`;
- `AgentDecisionFallback`;
- `MessageQueued`;
- `AgentMoved`;
- `RoomScanned`;
- `HazardSensed`;
- `HazardTraversed`;
- `RepairStarted`;
- `RepairContinued`;
- `RepairInterrupted`;
- `PowerRestored`;
- `ConsoleRepaired`;
- `ConsoleActivated`;
- `ConsoleSyncFailed`;
- `ArchiveOpened`;
- `RecorderPickedUp`;
- `RecorderDropped`;
- `DroneMoved`;
- `AgentDamaged`;
- `ModuleConsumed`;
- `ObjectiveAdvanced`;
- `MissionSucceeded`;
- `MissionFailed`;
- `RunSuspended`;
- `TurnEnded`.

Every event has run ID, sequence, turn, phase, type, payload, and post-event
state hash where applicable.

## 15. Content acceptance

Cold Start content is complete only when:

- the JSON validates against `mission.schema.json`;
- every referenced ID resolves;
- every practice and certification mutation validates;
- a deterministic solver proves each materialized variant;
- generic baseline and designed-build gates pass;
- three reference strategy families remain viable;
- agent observation snapshots contain no forbidden truth;
- map presentation data renders without overlap at desktop and tablet
  breakpoints;
- all state machines and terminal conditions have rule tests;
- a human can understand a failed sync from replay without reading logs.
