# DIRECTIVE DRIFT — Bootstrap, Launch, and Monetization Plan

## 1. Recommendation

Launch the first useful version free with hard limits.

Offer:

- unlimited scripted tutorial and replays;
- free build creation and local history;
- a small, explicit bank of live-run credits;
- invite-gated replenishment during the private test;
- optional donations/supporter purchase;
- no subscription until repeat play is demonstrated;
- no bring-your-own-key in normal onboarding.

The v1 objective is evidence and word of mouth. Charging before players prove
they want to revise and rerun will hide the most important signal.

## 2. Can it be bootstrapped?

Yes, if scope and live calls remain bounded.

The expensive part is not hosting the map or C# server. It is up to 36 model
decisions per full live run. Bootstrap by separating:

- **free deterministic play:** tutorial, scripted showcase, replay, editing;
- **metered autonomy:** only real decision calls consume credits;
- **research cohort:** invite codes and a global circuit breaker;
- **offline development:** scripted/fake providers by default.

Suggested first cohort:

- 30–50 invited testers;
- 8–10 live runs each over the entire test, not daily entitlement;
- a hard model-spend experiment budget of US$100–200;
- one small server;
- no paid acquisition.

Use actual measured cost before widening access.

## 3. Cheapest credible hosting shape

One small always-on container with:

- ASP.NET Core serving the compiled React client;
- API and background operation worker;
- SQLite on a persistent volume;
- daily offsite database backup;
- outbound calls to one model provider;
- CDN/proxy in front if the host supplies it.

Expected non-AI infrastructure for a private prototype is generally in the
single-digit to low-tens of US dollars per month, depending on provider,
region, storage, and backup. Compare current Docker hosts at deployment time;
do not redesign around a promotional free tier.

Why not start serverless:

- durable turn operations and provider latency are easier in one process;
- SQLite needs a stable volume;
- budget and concurrency locks are simple;
- cold-start and request-duration behavior vary by platform.

Move to managed PostgreSQL and multiple instances only after concurrency proves
the need.

## 4. Live-credit model

### Private alpha

- account/guest receives 10 live-run credits total;
- one credit starts one live run;
- failed infrastructure operations can automatically return the credit;
- player-caused mission failure does not;
- scripted runs cost no credit;
- admin can grant more after feedback.

### Public prototype

Test one of:

- 5 introductory live runs, then one weekly refill;
- one live run per day with a small stored cap;
- free tutorial plus supporter-funded credits.

Do not advertise a daily amount until real cost, completion, and abuse rates
are known.

### What a credit guarantees

A credit permits a run within:

- maximum 18 turns;
- maximum 40 provider attempts;
- configured input/output caps;
- configured dollar cap.

If the provider fails or the run suspends before gameplay advances, preserve
or restore fair credit through server-side rules.

## 5. Why BYOK is not the default

Bring-your-own-key sounds costless but creates:

- confusing setup before the player sees the game;
- key-handling trust and security obligations;
- provider-specific support;
- unpredictable model behavior;
- fairness fragmentation;
- players accidentally overspending;
- poor conversion from a shared link.

Support it later in a clearly labeled self-hosted/developer mode if demanded.
Never ask a player to paste a provider key into client-only code that sends it
through untrusted analytics or storage.

## 6. Spend circuit breakers

Enforce server-side:

- input, output, and response-byte caps;
- 25-second attempt timeout;
- one formatting retry;
- 40 attempts/run;
- estimated dollar cap/run;
- concurrent live-operation cap;
- per-guest credit and daily cap;
- per-IP abuse throttle;
- provider account budget alert;
- deployment daily and monthly caps;
- manual kill switch to scripted-only mode.

Budget flow:

```text
estimate worst case
-> reserve guest/run/global allowance
-> dispatch
-> settle actual or conservative estimate
-> release unused reservation
```

When any hard cap is reached:

- do not dispatch;
- preserve run state;
- show a plain explanation;
- allow replay and scripted play;
- never quietly fall back to a different paid model.

## 7. V1 monetization

### Recommended

1. Free prototype.
2. Optional `Support Directive Drift` purchase/donation.
3. Supporter gets a thank-you badge and a modest live-credit grant if unit
   economics permit.
4. No gameplay power, exclusive briefing cards, or stronger models tied to
   payment.

itch.io supports browser HTML projects and donations for HTML5 games in its
[official creator documentation](https://itch.io/docs/creators/html5). Because
Directive Drift needs its own API, use the primary hosted game URL and an
itch.io project page/launcher or carefully tested HTTPS embed rather than
assuming itch can host the backend.

### Do not start with a season subscription

A subscription makes sense only if:

- players return weekly;
- multiple missions or rotating challenges exist;
- model cost and gross margin are measured;
- there is a reliable content cadence;
- cancellation/support/account systems are ready.

One mission and uncertain retention do not justify recurring billing.

### Possible post-validation offers

- one-time early-access purchase including a campaign;
- monthly challenge pass with included run credits;
- supporter credit packs priced above worst-case model cost;
- creator edition after mission tooling exists.

Never sell “unlimited AI” without a technically enforced fair-use ceiling.

## 8. Acquisition hook

Market the consequence, not the technology:

> I gave one robot the objective and the other the warning. They each made the
> right move—and lost.

The best asset is a 30–45 second clip:

1. cards split between Kite/Wren;
2. launch;
3. reactor power floods through the map;
4. two agents approach consoles;
5. their messages cross one turn late;
6. archive stays shut;
7. replay highlights the information gap;
8. “What would you change?”

Avoid leading with “multi-agent LLM framework.” Players should understand the
decision in five seconds.

## 9. Where to find the first players

In order:

1. 10 handpicked strategy/automation players in direct calls;
2. 20–40 players from developer and AI-builder networks;
3. small strategy-game and systems-design communities with permission;
4. itch.io page with a direct playable link and development log;
5. short clips on channels where the builder already participates;
6. micro-creators who cover unusual strategy/AI games;
7. a Show HN only after there is a direct playable experience and the poster
   genuinely participates in HN;
8. Steam Playtest later, when the build and store presence justify the setup.

Steam can wait. The current
[Steam Direct documentation](https://partner.steamgames.com/doc/gettingstarted/appfee)
describes a per-product fee and recoupment threshold; pay it when Steam
wishlists/playtesting solve a proven distribution problem, not to validate the
first loop.

## 10. Built-in sharing loop

After a run, generate one compact share object:

- build codename;
- card allocation diagram;
- result;
- one decisive event;
- map snapshot;
- replay link if player opts in;
- call to propose a revised build.

Good share copy is causal:

- `Power restored. Sync missed by one turn.`
- `Kite knew the route. Wren had the deadline. Neither had both.`
- `Certified 2/3 with only four messages.`

Do not share raw private role text or messages without explicit selection.

## 11. Landing page

Above the fold:

- one looping 12–18 second map clip;
- title and tagline;
- one-sentence mechanic;
- `Play the free prototype`;
- no account requirement for scripted tutorial.

Next:

- “You control what they know” three-panel explanation;
- 45-second failure/revision clip;
- map screenshot;
- one short testimonial after testing;
- email/Discord opt-in only if there is a real update cadence.

## 12. Launch sequence

### Phase 0 — Instrumented internal

- 5 people;
- scripted onboarding plus 3 live runs;
- fix comprehension and critical runtime failures.

### Phase 1 — Concierge private

- 20 people;
- observe at least half live;
- manually grant credits;
- weekly mechanics iteration;
- no broad announcement.

### Phase 2 — Invite alpha

- 50–100 people;
- fixed spend budget;
- share links;
- evaluate organic invitations and build revision.

### Phase 3 — Free public prototype

Only if product/evaluation gates pass:

- own URL;
- itch page;
- public clip and development post;
- limited live credits;
- scripted fallback under high demand;
- publish what the team is learning.

## 13. Analytics events

Collect the minimum:

- tutorial started/completed;
- build created/versioned;
- card allocation summary counts, not text;
- run started/completed/provider mode;
- replay opened and decisive marker visited;
- build revised after run;
- certification started/completed;
- share generated/opened;
- credit exhausted/support clicked;
- estimated cost and latency server-side.

Core funnel:

```text
visit
-> tutorial complete
-> first build
-> first run
-> replay
-> intentional revision
-> third run
-> certification/share
```

The north-star prototype event is **revision after diagnosis**, not raw run
count.

## 14. Unit-economics gate

Before charging, calculate:

```text
model cost per started run
model cost per successful run
model cost per retained player-week
payment processing + platform share
support/refund allowance
gross margin under p90 heavy use
```

Price included credits from p90 cost, not average ideal runs. Keep a margin for
retries, provider changes, taxes/fees, and abuse.

## 15. Bootstrap go/no-go

Continue toward paid early access if:

- target revision/retention metrics pass;
- at least 40% want another mission;
- organic sharing produces qualified plays;
- cost per retained player is tolerable;
- the generic baseline remains weak;
- content production estimates are sustainable.

Pause or redesign if:

- most users consume free live runs but do not revise;
- explanation requires a long AI tutorial;
- model cost scales faster than engagement;
- the visual spectacle, not command design, is the only praised feature;
- players want direct control rather than more command tools.
