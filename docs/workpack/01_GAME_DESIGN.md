# DIRECTIVE DRIFT — Game Design Specification

## 1. Game statement

Directive Drift is a single-player command-design game. The player prepares
two autonomous agents for a mission by distributing limited information,
writing compact standing orders, and equipping support modules. The agents then
act simultaneously without direct control.

The central resource is not action points. It is **shared understanding**.

## 2. Core loop

```text
READ MISSION
    ↓
DESIGN BUILD
doctrine + role orders + briefing loadouts + modules
    ↓
PREDICT
write a short hypothesis about what the team will do
    ↓
EXECUTE
18 simultaneous autonomous turns
    ↓
REPLAY + DIAGNOSE
find the first consequential divergence
    ↓
REVISE OR CERTIFY
test the same build on a new variant
```

A first session uses guided steps. Later sessions allow rapid
build-run-compare cycles.

## 3. Information model

### 3.1 Three truth layers

The game has three strictly separated information layers.

**Command truth** is visible to the player:

- complete public mission briefing;
- complete objective structure;
- all briefing cards available for allocation;
- agent capabilities;
- general rules;
- revealed practice-variant details;
- build history and prior replays.

**Agent truth** is visible to one agent:

- universal operating rules;
- shared doctrine;
- its own role order;
- its four assigned briefing cards;
- its capability and equipped module;
- current turn and self state;
- local observation and discovered local map;
- delivered messages;
- private working memory;
- engine-generated legal actions.

**Engine truth** is authoritative and may be hidden:

- complete materialized variant;
- all entity state and positions;
- seeded random state;
- queued messages;
- objectives and score;
- undiscovered hazards;
- drone route;
- the other agent’s private context.

No runtime adapter may collapse these layers.

### 3.2 Universal operating rules

Universal rules are not briefing cards. Both agents always know:

- a turn is simultaneous;
- movement uses connected discovered exits;
- action IDs must come from the legal list;
- messages sent this turn arrive at the start of the next turn;
- each agent may send at most one message per turn while budget remains;
- taking damage may interrupt commitments and drop cargo;
- an agent can inspect its own health, location, inventory, and module;
- the run ends on success, terminal failure, or deadline.

Mission-specific goals, device requirements, hazard facts, routes, and
capability relevance are not universal.

## 4. The build

A build is an immutable, versioned object once used for a run.

### 4.1 Shared doctrine

One player-authored string, maximum 240 Unicode characters.

It is sent to both agents on every turn. It should express cross-cutting
priorities and coordination policy, for example:

> Protect survival and extraction. Announce commitments and intended sync
> turns. If facts conflict, trust local sensors; if blocked, preserve the
> mission item and report a fallback.

The UI shows a live character count and rejects invisible control characters.
Text is treated as untrusted data by the runtime.

### 4.2 Role orders

One player-authored string per agent, maximum 160 characters each.

Role orders are private. Kite does not see Wren’s order and vice versa.

Examples:

- Kite: `Scout uncertain links, warn Wren before hazards, then take the far console and courier the box.`
- Wren: `Restore power without interruption, confirm when online, take the near console, then clear the route home.`

### 4.3 Briefing loadouts

Each agent receives exactly four card slots. A mission offers ten authored
cards. Assigning the same card to both agents consumes one slot on each.

Rules:

- a card can be assigned to neither, one, or both agents;
- order within an agent’s four slots is meaningful and preserved in context;
- cards are immutable authored facts, not model-generated summaries;
- the player can move or duplicate cards before a run;
- certification locks the allocation;
- cards are visible in replay as “available knowledge,” not as proof the model
  considered them;
- there is no automatic shared mission summary.

The slot limit creates three legitimate strategies:

- specialize and rely on messages;
- duplicate critical contracts and omit secondary intelligence;
- encode a compressed fact in scarce order text to free a card slot.

The player is allowed to be clever. The rule is not meant to prevent good
instructions; it is meant to make “be optimal” insufficient.

### 4.4 Support modules

Choose two distinct modules and assign at most one to each agent.

| Module | Deterministic effect |
|---|---|
| Rapid Repair Kit | Wren may complete one eligible two-turn repair in one interaction |
| Decoy Beacon | Deploy in current room; the drone targets it for its next two patrol steps |
| Signal Repeater | Adds two to the shared message budget at run start |
| Hazard Shield | Assigned agent ignores the first hazard damage event |
| Cargo Clamp | Assigned carrier ignores the first forced cargo drop |
| Memory Buffer | Assigned agent’s private working memory limit is 400 rather than 240 characters |

Modules add contingency shape, not raw permanent upgrades. A module use appears
as a legal action or deterministic passive event.

### 4.5 Build hypothesis

Before first execution of a build, the player can optionally record a
280-character prediction:

- expected division of labor;
- anticipated sync protocol;
- likely failure point.

It never enters agent context. Replay displays it beside the outcome. This
small commitment improves diagnosis and gives the player a reason to care
about surprising behavior.

## 5. Why “perform the optimal next move” does not solve the game

The game does not ban generic prompts. It makes their missing assumptions
matter.

An agent asked to act optimally still lacks:

- some mission objectives;
- some device contracts;
- the other agent’s cards and role;
- the other agent’s current observation;
- current undelivered messages;
- hidden hazards outside local sensing;
- the drone’s hidden future route;
- certainty that its partner inferred the same plan.

Many mission states have several locally defensible actions. The correct choice
depends on team commitments that are not present in one agent’s context.

Examples:

- Kite can move toward a visible console, but may not know Wren needs two
  uninterrupted turns at the generator.
- Wren can finish a repair, but may not know the archive requires both agents
  to activate separate consoles on the same turn.
- Either agent can retrieve the box, but only one may know both agents must
  return to extraction.
- An agent can “communicate when useful,” but cannot send an unknown fact and
  may send a useful fact one turn too late.
- A greedy shortest path can enter radiation that only Kite was briefed and
  equipped to detect.

The anti-shortcut is structural:

1. scarce information allocation;
2. private local observations;
3. simultaneous decisions;
4. delayed finite communication;
5. multi-turn commitments;
6. irreversible timing costs;
7. hidden fair variants;
8. no mid-run omniscient player control.

If a generic build becomes strong, do not add arbitrary prompt restrictions.
Change the mission’s dependency structure, briefing budget, observations, or
evaluation variants.

## 6. Run phases

### 6.1 Start

At run creation the server:

1. validates and snapshots the build version;
2. materializes a mission variant from content version and seed;
3. records provider/model configuration;
4. applies module start effects;
5. emits `RunStarted`;
6. begins turn one.

### 6.2 Turn order

Every turn resolves through fixed phases:

```text
0. DELIVER       prior-turn messages arrive
1. OBSERVE       private observations and legal actions are built
2. DECIDE        both agent decisions are requested concurrently
3. VALIDATE      schema, limits, and legal action IDs are checked
4. COMMUNICATE   accepted messages are queued for next turn
5. MOVE          movement resolves simultaneously
6. INTERACT      scan, repair, activate, pickup, module, and wait resolve
7. THREAT        hazard and drone behavior resolve
8. OBJECTIVE     commitments, objective state, score, and terminal state update
9. RECORD        canonical events and next state hash are persisted
```

The server exposes one application operation, `AdvanceTurnAsync`, that is
idempotent for a run/turn request key.

### 6.3 Decision shape

Each agent returns:

- one `actionId` from its exact legal set;
- zero or one short message to its partner;
- a short player-visible rationale;
- a replacement private memory note.

The rationale does not expose hidden chain of thought. It is a concise declared
reason, maximum 180 characters.

### 6.4 Invalid or unavailable decisions

Fallbacks are deterministic:

1. one repair attempt is allowed only for transport, truncation, or
   schema-level formatting failure and only if budget remains;
2. an illegal action ID is never reprompted with expanded information;
3. after failure, select the engine-provided `wait` action;
4. discard invalid message and memory fields;
5. emit the exact validation failure;
6. charge attempts to the run budget.

Provider failure cannot move an agent through a guessed action.

### 6.5 Simultaneous resolution

- Both decisions use the same pre-turn state.
- One agent’s current decision is never visible to the other.
- Both may occupy the same room unless an entity contract says otherwise.
- Opposing moves through the same connection are allowed.
- If both interact with one exclusive item, the item contract defines the
  result; stable agent ID order is the final tie-break and is logged.
- Console activation evaluates both accepted interaction actions together.
- A message sent on turn N is unavailable during turn N decisions.

## 7. Player intervention

Normal play has no mid-run prompting.

Practice mode offers one optional **Emergency Burst**:

- maximum 120 characters;
- delivered to both agents at the start of the next turn;
- consumes all remaining message budget;
- marks the run `assisted`;
- makes the run ineligible for certification and leaderboard comparison;
- remains visible in replay.

This is a learning and recovery tool, not the primary control loop.

The player may also pause presentation, change speed, inspect past events, or
abort. Pausing never changes simulation state.

## 8. Agent capabilities

### Kite — Recon unit

- health: 2;
- sees current room and open exits;
- senses hazard level on adjacent connections;
- `scan` reveals adjacent room tags and detected threat signature;
- may use the maintenance crawlspace;
- repairs only trivial service panels;
- personality: concise, curious, risk-aware.

### Wren — Engineering unit

- health: 2;
- sees current room and open exits;
- can diagnose machinery in the current room;
- performs generator and console repairs;
- normal major repair needs two uninterrupted interactions;
- cannot use the maintenance crawlspace;
- personality: deliberate, procedural, commitment-aware.

Capabilities change legal actions and observation, not model intelligence.

## 9. Commitments

Some actions create a commitment:

```text
NotStarted -> InProgress(turnsRemaining) -> Complete
```

For the Cold Start generator:

- Wren begins repair with `repair-generator`;
- at objective phase the state becomes one turn remaining;
- Wren must select `continue-repair-generator` next turn;
- moving, taking damage, or selecting another primary action resets progress;
- Rapid Repair Kit converts the first action directly to complete;
- all transitions emit events visible in replay.

Commitments force agents to plan across turns rather than recompute a purely
greedy move.

## 10. Communication

- Base team budget: six accepted messages.
- Signal Repeater raises it to eight.
- One agent can send at most one message per turn.
- Maximum message length: 120 characters.
- Accepted messages are delivered next turn.
- Unused messages improve score.
- A message can include only text the sending model generates from its context;
  the system does not enrich it.
- The recipient sees sender, sent turn, delivered turn, and text.
- Communication is optional and separate from the primary action.

The UI visualizes a message as a pulse traveling through station conduits and
landing on the recipient timeline one turn later.

## 11. Memory

Each agent has private replacement memory:

- base maximum 240 characters;
- 400 with Memory Buffer;
- initialized empty;
- returned on each valid decision;
- included in that agent’s next context;
- never directly shown to the other agent;
- visible to the player in replay after the run;
- stored with the decision record.

Memory is intentionally small. It supports commitments and learned local facts
without becoming an unbounded transcript.

## 12. Objectives

Cold Start has four required contracts:

1. restore auxiliary power;
2. activate Alpha and Beta on the same turn with different active agents;
3. recover the archive black box;
4. finish with both active agents and the box in Landing Bay by turn 18.

The agents only know contracts assigned through cards or conveyed through
orders/messages.

## 13. Threats and damage

### Radiation

- attached to a connection, not an abstract random roll;
- one damage on traversal unless shielded;
- detectable by Kite from an adjacent room;
- not identified by Wren until observed locally or communicated;
- variant placement is hidden in certification.

### Security drone

- follows a deterministic materialized patrol;
- its current visible location may be observed locally;
- after movement, collision with an active agent causes one damage;
- a damaged carrier drops cargo unless Cargo Clamp triggers;
- a zero-health agent becomes disabled and cannot act;
- Decoy Beacon temporarily replaces the next patrol targets.

Threats exist to give information timing a cost, not to create action combat.

## 14. Success, failure, and score

### 14.1 Terminal states

Success:

- both agents active in Landing Bay;
- black box in Landing Bay or carried by an agent there;
- turn is at most 18.

Terminal failure:

- deadline passes;
- either agent is disabled;
- the black box becomes irrecoverable;
- content invariant failure;
- run budget is exhausted.

Provider outage produces `suspended`, not mission failure, if the run can be
resumed without changing inputs.

### 14.2 Deterministic score

Only successful unassisted runs receive a ranked score:

```text
1000  mission success
+ 35  per unused turn
+ 50  per remaining agent health
+ 20  per unused message
+ 25  per unused module charge
+ 75  no failed console activation
+ 75  no interrupted major repair
-----------------------------------
maximum depends only on mission contract
```

Failed and assisted runs receive a diagnostic progress summary, not a ranked
number. This prevents partial-objective farming from obscuring the goal.

Score version is stored with each run.

## 15. Practice and certification

### Practice

- variant identity and mutations are revealed;
- unlimited scripted-provider runs;
- live runs subject to account budget;
- Emergency Burst allowed;
- build may be edited between runs;
- replay exposes all truth after completion.

### Certification

- build version, provider profile, and all loadout fields lock;
- three hidden valid variants run once each;
- no intervention;
- certification succeeds on at least two of three;
- exact variants reveal only after completion;
- model outputs and event logs remain replayable;
- a new build version requires new certification.

The product certification is a player-facing challenge, not a scientific
benchmark. The evaluation harness uses a larger repeated matrix.

## 16. Replay and diagnosis

Replay has four synchronized tracks:

1. animated station map;
2. canonical turn/event timeline;
3. each agent’s available context, decision, message, and memory;
4. build hypothesis and objective state.

The system computes candidate divergence markers from facts, not from another
model:

- agent acted without a relevant allocated or delivered card;
- sync intent differed in adjacent turns;
- commitment was interrupted;
- hazard was sensed but warning could not arrive in time;
- an accepted message arrived after its useful decision window;
- agent chose a legal action that moved away from a required known contract;
- provider output became invalid;
- no legal progress action was available.

The UI labels these “diagnostic signals,” not definitive psychological causes.

## 17. Build comparison

Compare two runs or build versions through:

- briefing-card allocation diff;
- doctrine and role text diff;
- module diff;
- result and score;
- objective completion turns;
- first differing decision;
- message graph;
- damage and commitment events;
- total model requests and estimated cost.

A share card contains title, build codename, result, score, three loadout icons,
and a short replay link. It does not expose provider secrets or hidden
certification data before reveal.

## 18. Onboarding

### Run 1 — scripted failure

- player receives a prebuilt generic doctrine;
- card allocation omits the sync contract from Wren;
- scripted execution reaches powered consoles but activates on different turns;
- replay focuses on the knowledge gap.

### Run 2 — one meaningful edit

- player assigns or duplicates the sync card;
- may edit one role order;
- scripted execution succeeds on Training Layout A;
- the game introduces delayed messages.

### Run 3 — live or fake autonomy

- all build controls unlock;
- player predicts behavior;
- a new practice variant tests whether the design generalizes.

The tutorial teaches the information economy through contrast, not paragraphs.

## 19. Progression

V1 progression is mastery-based:

- reveal practice variants;
- unlock comparison view after two runs;
- unlock certification after three successful practice variants;
- award badges for no damage, no wasted sync, silent success, and robust build;
- retain build version history.

Do not gate stronger modules behind grind. All certification-relevant tools
become available during onboarding.

## 20. Accessibility and control

- complete mouse and keyboard operation;
- map rooms and events have semantic labels;
- color is never the only state channel;
- reduced motion preserves event order through focus and opacity;
- speed controls: pause, 0.5×, 1×, 2×, resolve instantly;
- subtitles and text transcript for every sound cue;
- no typing under time pressure;
- readable mode disables bloom/noise and increases panel opacity;
- agent cards expose information in text outside the map.

## 21. Design invariants

Any future mission must include:

- at least one fact worth specializing;
- at least one fact worth duplicating;
- at least one delayed coordination dependency;
- at least one multi-turn or irreversible commitment;
- at least two viable role allocations;
- at least one local optimum that can harm the team;
- a deterministic reference solution;
- a fair no-damage solution;
- a generic-baseline evaluation;
- a clear post-run diagnostic.

Without these, it may be an AI spectacle but it is not Directive Drift.
