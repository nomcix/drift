# DIRECTIVE DRIFT — Product Brief

## 1. Product thesis

Most AI games reward discovering a powerful prompt or watching a model improvise.
Directive Drift makes a different promise: the player designs a durable command
system for autonomous characters who must act without complete information.

The fun is not asking, “Can an AI solve this?” The fun is asking:

- What must Kite know that Wren does not?
- Which facts are worth duplicating?
- What does “wait for confirmation” mean when messages are delayed?
- When should a local agent abandon its role to protect the mission?
- Why did a doctrine that worked on one layout collapse on another?

The result should feel closer to building a chess opening, programming a
cooperative board-game team, and debugging a small organization than to using
a chatbot.

## 2. Player fantasy

You are an off-site mission controller responsible for two autonomous field
units. Before contact, you decide:

- their shared doctrine;
- their individual roles;
- which mission facts each receives;
- which support module each carries.

Once the mission begins, communication is delayed and intervention is
restricted. You watch their plan meet incomplete information, diagnose the
result, and revise the command architecture.

## 3. Core hook

**Design the briefing, then watch intent drift—or hold—under pressure.**

The emotional loop is:

1. authorship: “I built this team”;
2. suspense: “Will they infer what I meant?”;
3. surprise: “That was clever / disastrous”;
4. legibility: “I understand why”;
5. mastery: “I know what I will change”;
6. proof: “The revised doctrine survived a new variant.”

The fifth step is the retention engine. Every failure must generate a specific
new hypothesis.

## 4. Target player

Primary:

- strategy, automation, deckbuilding, immersive-sim, and puzzle-game players;
- people curious about AI but tired of generic chat interfaces;
- players who enjoy optimizing systems and sharing builds;
- streamers and developers who can narrate emergent failures.

Secondary:

- AI practitioners interested in controllability and multi-agent behavior;
- educators discussing information, incentives, or organizational design.

This is not initially aimed at:

- action players seeking direct control;
- players who want a long authored story;
- prompt-engineering courses;
- people who require a fully offline product on day one.

## 5. Experience targets

| Property | Vertical-slice target |
|---|---|
| First meaningful choice | under 3 minutes |
| First execution | under 8 minutes |
| Practice run | 4–8 minutes |
| Build revision | 2–5 minutes |
| Complete first session | 35–60 minutes |
| Mastery runway | 2–4 hours in one mission |
| Required typing | under 600 characters per build |
| Live model calls | at most 36 per run; target fewer through early termination |

## 6. Design pillars

### 6.1 Preparation is play

The briefing workbench is not a settings screen. Allocation, duplication, and
word-budget tradeoffs must be visible, tactile, and consequential.

### 6.2 Partial knowledge is fair

Agents lack information by an explicit player-authored allocation. The UI shows
exactly what each one will know. Hidden variants alter facts within taught
rules, not through arbitrary surprises.

### 6.3 Autonomy has readable causes

Every action exposes:

- the selected legal action;
- a short agent rationale;
- the observations and messages available at that moment;
- relevant doctrine and role excerpts;
- the deterministic outcome.

Rationales are not ground truth about model internals, but they are useful
declared intent. The event log remains authoritative.

### 6.4 Failure teaches

The replay highlights the first decisive divergence:

- missing information;
- coordination failure;
- commitment interruption;
- unsafe local optimization;
- message timing;
- resource misuse;
- execution error.

### 6.5 The AI is a player piece, not the referee

Models select among legal actions. They never:

- invent actions;
- mutate state;
- decide whether an objective was achieved;
- set score;
- reveal hidden facts;
- summarize private information for the other agent.

### 6.6 Fairness survives model replacement

The game must support a pinned official model configuration for comparisons,
but its mechanics and evaluation cannot depend on one provider’s quirks.

## 7. Product structure

### Vertical slice

- one authored mission;
- two asymmetric agents;
- one visual operations map;
- one command-workbench screen;
- practice, certification, replay, comparison;
- local guest profile;
- optional live AI.

### Future game, only if validated

- a campaign of facilities with different information structures;
- new agents with genuine capabilities, not stat inflation;
- unlockable doctrine tools and briefing-card formats;
- asynchronous community challenges with pinned execution environments;
- human-versus-human “command duel” scenarios;
- daily deterministic seeds;
- creator-authored missions after robust content tooling exists.

## 8. What makes this a game rather than an AI demo

The player operates under explicit scarcity:

- four briefing slots per agent;
- one 240-character shared doctrine;
- one 160-character role order per agent;
- one module per agent, two total;
- six delayed team messages;
- eighteen simultaneous turns;
- hidden-but-fair mission variants;
- a deterministic score and certification pool.

There is no single text box that can contain the entire solution without
sacrificing something else. A copied full briefing would exceed order space,
erase procedural nuance, and still not supply future local observations.

## 9. Success criteria

### Product

In a moderated or instrumented test of at least 20 qualified players:

- 70% launch a second run;
- 50% intentionally revise a build;
- 35% complete at least four runs;
- median session reaches 35 minutes;
- 60% can explain one failure cause correctly;
- at least 25% share, copy, or screenshot a build/result;
- at least 40% say they want another mission.

### Mechanical

- generic-optimal baseline: at most 25% certification success;
- competent designed build: at least 70% over pinned matrix;
- no universally dominant briefing allocation in the practice pool;
- at least three materially distinct strategy families can certify;
- model variance changes executions but not rule resolution.

### Operational

- p95 cached page load under 3 seconds on broadband;
- p95 turn endpoint excluding model latency under 300 ms;
- zero unbounded model requests;
- replay produces zero model cost;
- a hosted private test can run below the monthly budget in
  `09_LAUNCH_AND_MONETIZATION.md`.

## 10. Failure criteria

Pause expansion if any remain true after two design iterations:

- most players write generic orders and succeed;
- players cannot predict why briefing allocation matters;
- watching a run is more interesting than revising it;
- the first failure feels random rather than diagnostic;
- live AI cost exceeds willingness to pay by more than 3×;
- the map looks like a dashboard rather than a place;
- the strongest strategy is memorizing one route;
- certification primarily measures provider luck.

## 11. V1 business stance

The first public prototype should be free to start:

- scripted tutorial and a limited number of live runs are free;
- replay and local build editing remain free;
- no subscription before repeat play is demonstrated;
- optional supporter purchase may fund additional model credits;
- bring-your-own-key is an advanced option, not onboarding.

The prototype’s job is to acquire evidence and advocates, not maximize revenue.
Hard usage limits make this compatible with bootstrapping.

## 12. Creative direction

Tone:

- quiet competence under pressure;
- orbital salvage rather than military conquest;
- tense but not grim;
- technical language with human-readable consequences;
- occasional dry warmth in agent personality.

Visual shorthand:

> a deep-space mission-control table crossed with a neon transit map and
> submarine sonar.

The station should feel physical through shape, light, sound, and motion even
though the rules use a small topological graph.

## 13. Title and tagline

Working title: **DIRECTIVE DRIFT**  
Primary tagline: **Design the briefing. Survive the drift.**

Alternate launch copy:

> Two autonomous agents. One incomplete plan. You decide what each knows—then
> live with what they infer.

The title is provisional pending the naming gate.
