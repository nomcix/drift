# DIRECTIVE DRIFT — Evaluation and QA Plan

## 1. Quality thesis

The vertical slice is only successful if:

1. rules are deterministic and fair;
2. agent information boundaries are real;
3. a generic optimality prompt is weak;
4. intentional builds improve robustly;
5. failure is understandable;
6. the map feels like a game;
7. live-model cost is bounded.

Normal software tests cover only the first two. This plan combines rule tests,
behavior evaluation, usability tests, visual QA, and operational gates.

## 2. Test modes

| Mode | Provider | Purpose | Default CI |
|---|---|---|---|
| unit | none | pure rule behavior | yes |
| scripted integration | scripted | full deterministic spine | yes |
| fake resilience | fake | malformed/timeout/cost states | yes |
| browser smoke | scripted | user-critical paths | yes |
| evaluation | live pinned profile | behavior and cost | scheduled/manual |
| exploratory | live current candidates | model selection | no |

No pull request depends on an external model.

## 3. Core rule tests

### State machines

- generator begins, continues, completes;
- movement/action/damage interrupts generator;
- Rapid Repair Kit completes once;
- damaged console repair and power commute correctly;
- only two different active agents on different ready consoles sync;
- unmatched activation resets;
- gate remains open;
- recorder pickup/drop/repickup/extraction;
- Cargo Clamp and Hazard Shield consume once;
- deadline boundary at turn 18;
- disabled agent terminal failure;
- ranked score exact arithmetic.

### Turn phases

- prior message delivers before observation;
- same-turn message is absent from partner context;
- decisions use identical pre-turn version;
- communication queues before movement but delivers next turn;
- movement events precede interactions;
- threat interrupts current commitment;
- objective evaluation follows threat;
- stable event ordering under ties.

### Legal actions

- no action targets an undiscovered/inaccessible route;
- Kite and Wren capability differences;
- service lock and crawlspace;
- module actions only when equipped/unused/legal;
- repair continuation available only in valid state;
- wait always exists for active agent;
- terminal run has no advance action.

## 4. Property and metamorphic tests

Generate valid small states and assert:

- health never leaves allowed range;
- message budget never increases except declared start module;
- turn increases exactly once per resolved turn;
- event sequence is contiguous;
- no entity occupies an unknown room ID;
- objective state cannot regress except explicitly resettable activation;
- recorder has exactly one custody state;
- score does not use wall time;
- all accepted action IDs came from pre-turn legal lists.

Metamorphic:

- changing visual coordinates changes no legal action/event/state hash;
- changing a label changes no rule result;
- permuting dictionary insertion order changes no event order;
- serializing/deserializing state preserves next result;
- same materialized mission plus decisions produces byte-equivalent canonical
  events;
- replay on another supported OS produces the same state hashes.

## 5. Content validation

For every materialized variant:

- all IDs resolve;
- connections have valid endpoints;
- agent starts are valid;
- card IDs are unique;
- exactly ten cards and four slots per agent;
- hazard and lock mutations use allowed targets;
- drone patrol rooms connect under threat movement rules;
- topology invariants pass;
- reference solver succeeds in ≤17 turns;
- one no-damage solution exists;
- at least two console-role assignments remain possible;
- no specific module is required;
- player briefing and cards remain true;
- presentation nodes fit declared viewBox bounds.

Certification content is stored separately from browser-delivered practice
content until reveal.

## 6. Information-leakage tests

Create a marker value for every forbidden field, serialize each agent context,
and assert marker absence.

Tests include:

- hidden hazard connection;
- future drone route;
- variant ID/seed;
- unassigned briefing-card text;
- partner role order;
- partner cards;
- partner private memory;
- partner current observation;
- partner current decision;
- complete objective tree;
- reference solution;
- score formula.

Positive controls assert assigned cards, delivered messages, and public
station-wide events do appear.

Run tests against the actual provider request serializer, not an intermediate
object alone.

## 7. Anti-shortcut evaluation

### 7.1 Baseline build

Use `examples/generic-optimal-build.json` exactly:

- shared doctrine: complete the mission optimally and efficiently;
- both roles: choose the best legal action for assigned goals and communicate
  when useful;
- mechanically neutral card allocation;
- no strategy-specific support pairing.

Do not deliberately sabotage it with contradictory instructions.

### 7.2 Designed build

Use `examples/designed-build.json` as the reference competent build. It should
specialize information, duplicate sync, establish proposer/ack timing, protect
repair, and assign recorder custody.

### 7.3 Pinned matrix

Before public release, evaluate each baseline across:

- 8 valid variants: 5 practice-equivalent and 3 held-out;
- 5 repetitions per variant;
- one pinned provider/model/prompt profile;
- 40 total runs per build;
- identical variant/repetition schedule for both builds.

Hard gates:

- generic baseline succeeds in at most 10/40 runs (25%);
- designed build succeeds in at least 28/40 runs (70%);
- designed build exceeds generic by at least 18 runs;
- invalid-decision rate below 2% of agent turns;
- no one variant accounts for more than half the performance gap.

Record confidence intervals, but do not use them to excuse a missed hard gate.

### 7.4 If the baseline is too strong

Inspect:

- whether agents received implicit full objectives;
- legal action descriptions that leak the solution;
- public event summaries that reveal missing contracts;
- too many briefing slots;
- hazards with no meaningful consequence;
- sync windows that are too broad;
- provider common-sense priors matching the one mission;
- certification variants too similar to practice.

Fix mechanics or content, then rerun both builds. Do not merely make the
baseline wording worse.

### 7.5 If the designed build is weak

Inspect:

- impossible or misleading card text;
- message delay that makes taught protocols infeasible;
- reference build relying on one provider quirk;
- hidden variants violating taught rules;
- output invalidity;
- insufficient deadline margin;
- UI causing unintended allocation.

Preserve difficulty through coordination, not arbitrary model failure.

## 8. Determinism and replay suite

Golden fixtures include:

- each practice variant materialization;
- full scripted onboarding failure;
- full scripted success;
- console mismatch;
- repair interruption by move;
- repair interruption by drone;
- hazard shield;
- cargo clamp;
- provider fallback;
- Emergency Burst assisted run;
- success at turn 18;
- failure after turn 18.

For each:

1. run from seed and decisions;
2. capture ordered events and turn hashes;
3. reconstruct from start snapshot and decisions;
4. assert exact equivalence;
5. project to presentation state;
6. apply events instantly and assert final UI snapshot.

Golden updates require reviewed explanation.

## 9. API and persistence tests

- build schema and ownership;
- immutable build version after use;
- start run snapshots all versions/profile;
- duplicate idempotency key;
- two simultaneous advance requests;
- operation claim/lease/recovery;
- provider timeout and suspended state;
- budget reservation race;
- atomic state/events/usage settlement;
- restart between provider and resolve;
- event pagination order;
- replay access ownership;
- certification variant secrecy;
- certification resume uses same pool;
- EF migration from prior fixture;
- SQLite WAL backup/restore.

Use a temporary real SQLite database for integration tests, not an in-memory
provider with different semantics.

## 10. Web tests

### Unit/component

- card assign, duplicate, remove, reorder;
- exact slot and character limits;
- overlap meter;
- module exclusivity;
- build version diff;
- operation polling states;
- event reducer;
- map lens truth;
- reduced-motion behavior;
- accessible labels;
- diagnostics and score display.

### Playwright

1. complete scripted onboarding failure and diagnose missing sync card;
2. revise card loadout and complete scripted success;
3. create a custom build, run, pause, speed up, replay;
4. use Emergency Burst and see certification ineligibility;
5. simulate provider timeout and resume;
6. hit guest budget and see a clean non-destructive stop;
7. operate the build screen by keyboard;
8. run at 1280×720 and tablet breakpoint.

## 11. Visual QA

Capture stable screenshots for:

- unpowered turn 1;
- hazard sensed;
- repair 1/2;
- power restoration final state;
- consoles ready;
- sync wave/gate open final state;
- recorder carried;
- drone collision;
- success;
- failure;
- Kite lens;
- Wren lens;
- truth replay;
- reduced motion/readable mode;
- 1280×720 and 1024×768.

Human checklist:

- map reads as a place, not cards;
- six or more room silhouettes are distinct;
- conduit connectivity is traceable;
- labels do not collide;
- agents do not disappear in glow;
- threat is visible without motion;
- unpowered and unknown are not confused;
- objective state is readable at normal zoom;
- side rails do not dominate;
- 2× animation remains understandable.

## 12. Performance QA

Measure on a midrange laptop:

- page-load transfer and time to interaction;
- SVG DOM count;
- animation frame rate for power restoration and simultaneous movement;
- long-task duration;
- memory after ten replays;
- API p50/p95 excluding and including provider;
- SQLite transaction duration;
- client polling request volume.

Gates:

- no sustained animation below 50 fps in target profile;
- no essential interaction blocked by a >200 ms client main-thread task;
- idle map performs no continuous JavaScript animation loop;
- event list remains responsive at 500 events;
- replay seek to any turn under 200 ms after data load.

## 13. Cost and resilience QA

Automated fake-provider tests:

- request predicted above run cap is never sent;
- two agents reserve atomically;
- retry counts against caps;
- output over byte/token cap fails;
- daily cap stops new calls but preserves existing replay;
- cost settlement cannot be negative;
- price-table version stored;
- provider reports no usage: conservative estimate settles;
- process crash releases stale reservation safely;
- one guest cannot bypass cap with client state changes.

Private-test dashboard:

- spend today and seven-day trend;
- cost per started/completed run;
- calls per successful run;
- timeout/invalid/fallback rate;
- provider latency;
- scripted vs live usage;
- hard-cap remaining.

## 14. Usability research script

Recruit at least 20 strategy/system players, with a mix of AI familiarity.

Without explaining the intended solution:

1. ask what they believe they control;
2. complete onboarding;
3. let them create one build;
4. ask for a prediction;
5. run and replay;
6. ask what caused the first important failure/success;
7. allow free revision for 30 minutes;
8. ask whether they want another mission and what they would share.

Observe:

- whether they treat cards as consequential;
- whether they try a generic instruction;
- whether surprise feels fair;
- time to first revision;
- whether replay changes their hypothesis;
- whether the map supports spatial reasoning;
- whether typing feels creative or like work.

Do not lead with “prompt engineering.” Describe the game fantasy.

## 15. Release gates

### Alpha gate

- scripted spine complete;
- all core/content/determinism tests pass;
- build workbench and static map usable;
- no live provider required.

### Private live gate

- provider adapter and budgets pass;
- replay/cost records complete;
- generic and designed smoke matrix trending correctly;
- privacy notice and deletion path;
- deployment hard cap configured.

### Public prototype gate

- full 40+40 evaluation passes;
- at least 15 private testers and critical usability fixes;
- certification pool validated;
- visual acceptance passes;
- browser/accessibility smoke passes;
- operational alerts and backup tested;
- free-use limits visible before first live run;
- no unresolved severity-1 defect.

## 16. Bug severity

- **S1:** secret exposure, unbounded spend, ownership bypass, state corruption,
  hidden-truth leak, duplicate charge/turn, impossible certified content.
- **S2:** run softlock, replay mismatch, wrong score, inaccessible critical
  path, map materially misrepresents state.
- **S3:** confusing diagnostic, animation defect, noncritical responsive issue,
  recoverable provider error.
- **S4:** cosmetic polish, copy, nonblocking telemetry.

No public release with open S1 or S2 issues.
