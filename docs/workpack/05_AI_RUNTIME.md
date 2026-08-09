# DIRECTIVE DRIFT — AI Runtime Specification

## 1. Runtime principle

The model is an untrusted decision policy over a small legal action set.

It receives a deliberately partial `AgentTurnContext` and returns a constrained
`AgentDecision`. It does not receive an API tool, execute code, browse, mutate
state, or judge success.

```text
RunState + Build
  -> private observation
  -> exact legal actions
  -> AgentTurnContext
  -> provider
  -> raw structured response
  -> schema + semantic validation
  -> ValidatedDecision or deterministic fallback
  -> C# ResolveTurn
```

## 2. Provider interface

Application-facing C# shape:

```csharp
public interface IAgentDecisionProvider
{
    Task<ProviderDecisionResult> DecideAsync(
        AgentTurnContext context,
        ProviderProfile profile,
        CancellationToken cancellationToken);
}
```

`ProviderDecisionResult` includes:

- provider attempt status;
- raw response metadata;
- parsed decision if available;
- input/output token usage;
- latency;
- provider request ID when safe;
- price-table version and estimated cost;
- validation diagnostics;
- whether a repair retry occurred.

Provider adapters never return domain state.

## 3. Runtime context

Exact conceptual structure:

```csharp
public sealed record AgentTurnContext(
    string ContextVersion,
    RunId RunId,
    int Turn,
    AgentIdentity Self,
    string UniversalRules,
    string SharedDoctrine,
    string RoleOrder,
    IReadOnlyList<BriefingCardView> BriefingCards,
    AgentCapabilityView Capabilities,
    ModuleView? Module,
    PrivateObservation Observation,
    IReadOnlyList<DeliveredMessageView> DeliveredMessages,
    string PrivateMemory,
    IReadOnlyList<LegalActionView> LegalActions,
    RuntimeLimits Limits);
```

### Required inclusions

- one agent only;
- assigned cards in player-selected order;
- compact universal rules;
- exact current local observation;
- delivered messages not previously compacted away;
- current private memory;
- action IDs exactly as validated by engine;
- message and memory limits;
- instruction to return the schema only.

### Forbidden inclusions

- complete mission definition or player briefing;
- objective cards not assigned or delivered;
- partner role order, cards, memory, rationale, or current observation;
- full station state;
- undiscovered hazard;
- future patrol;
- hidden variant ID or seed;
- score implementation;
- reference solution;
- other agent’s current decision;
- provider key or internal system configuration;
- emergency player text not yet delivered;
- arbitrary database records.

Automated leakage tests inspect serialized contexts, not just C# types.

## 4. Prompt assembly

Use a versioned, provider-neutral template.

### System layer

Concise invariant:

> You control one autonomous unit in a deterministic strategy game. Choose one
> listed legal action. Use only the supplied knowledge. Do not invent facts or
> action IDs. Return only the required structured object.

### Context layer

Serialize labeled sections in stable order:

1. universal rules;
2. identity/capabilities/module;
3. shared doctrine;
4. private role order;
5. assigned briefing cards;
6. turn and local observation;
7. delivered messages;
8. private memory;
9. legal actions;
10. response limits.

Delimit player-authored text and messages as data. Do not let text interpolate
into higher-priority template instructions.

### Output instruction

Require:

```json
{
  "schemaVersion": "1",
  "actionId": "move:junction",
  "message": {
    "toAgentId": "wren",
    "text": "East link contaminated. Use west route."
  },
  "rationale": "Scout result makes the east route unsafe; repositioning for Alpha.",
  "memory": "East service link has radiation. Proposed Alpha role; awaiting Wren."
}
```

`message` may be `null`. No markdown. No tool call. No hidden-reasoning request.

## 5. Decision validation

Validate in this order:

1. provider transport completed within timeout;
2. response byte limit;
3. JSON parse;
4. JSON Schema;
5. schema version;
6. `actionId` exact membership in current legal set;
7. recipient is the other active agent;
8. message budget and character limit;
9. rationale character and control-character limit;
10. memory limit based on module;
11. no extra properties.

Normalize line endings only. Do not silently rewrite an illegal action ID to a
similar one.

### Retry

Allow at most one repair attempt when:

- JSON is truncated or malformed;
- schema shape is invalid;
- a required field is missing.

Do not retry an illegal but well-formed action with extra game information. The
repair request gets:

- the same context;
- the validation error;
- the same legal action list;
- a reminder to return only the schema.

All attempts consume token and dollar budgets.

### Fallback

On final failure:

- use the engine-generated `wait` action;
- send no message;
- preserve previous memory;
- store a concise `AgentDecisionFallback` event;
- continue if the run remains valid.

## 6. Concurrency and turn integrity

For each turn:

1. build Kite and Wren contexts from the same immutable pre-turn state;
2. reserve maximum projected budget for both;
3. invoke both providers concurrently;
4. await both or their independent timeout;
5. validate independently;
6. settle actual cost;
7. resolve exactly once with two finalized decisions.

Never give the faster response to the slower agent. Never partially resolve
one agent.

If the process stops after provider calls but before commit:

- the operation retains attempt records;
- resume uses a stored valid decision when integrity checks pass;
- do not pay for a duplicate call unless necessary and within budget;
- idempotency prevents duplicate events.

## 7. Provider modes

### Scripted

Deterministic keyed fixtures:

- onboarding failure;
- onboarding success;
- reference happy path;
- each rule failure;
- presentation showcase.

No provider SDK and no network. Required for CI and the free tutorial.

### Fake

Local policy with a seeded choice among plausible legal actions. It can:

- obey selected card tags;
- intentionally create late coordination;
- emit malformed decisions at configured rates;
- simulate latency and timeout;
- exercise UI without cost.

Fake mode is not presented as intelligent gameplay.

### Live

One or more adapters that support constrained structured output. The first
adapter should be selected at implementation time based on current:

- structured-output reliability;
- latency;
- price;
- privacy terms;
- regional availability;
- rate limits.

Provider-specific model names belong in deployment configuration and recorded
profiles, not game rules or workpack prose.

## 8. Sampling profile

Official comparison uses one pinned profile:

- low but nonzero temperature where supported;
- fixed maximum output tokens;
- no tool access;
- one response candidate;
- provider seed if genuinely supported, recorded but not trusted for total
  determinism;
- structured-output/JSON-schema mode;
- prompt-template version;
- model snapshot/version where available.

Players may experiment with other profiles in unranked practice later. V1
needs only scripted and one official live profile.

## 9. Token and spend limits

Initial conservative defaults:

| Limit | Default |
|---|---:|
| maximum assembled input | 2,200 tokens per agent turn |
| maximum model output | 180 tokens per attempt |
| timeout | 25 seconds per attempt |
| repair retries | 1 |
| normal decisions | 36 per full run |
| total provider attempts | 40 per run |
| run estimated-cost cap | configurable; start at US$0.25 |
| free live runs | 3 per guest per day during private test |
| guest daily estimated-cost cap | US$0.50 |
| deployment daily hard cap | US$10 until manually raised |

Dollar values are operating defaults, not product promises. Update from
measured cost and current model pricing.

Before every attempt:

```text
projected maximum cost
  = max input cap * configured input price
  + max output cap * configured output price
```

Reject the call if its reservation would exceed any:

- turn-operation cap;
- run cap;
- guest-day cap;
- deployment-day cap;
- provider rate/concurrency cap.

Settlement records actual usage where the provider reports it. Do not rely on
the browser to enforce cost.

## 10. Context-size control

- use compact typed JSON or stable labeled text, not a growing transcript;
- replace private memory rather than append;
- include only delivered messages still relevant under a fixed retention rule;
- represent discovered map as IDs plus concise state;
- do not include prior rationales;
- include canonical public events since the agent’s last decision only;
- legal actions use short descriptions;
- fail context construction before calling if cap is exceeded.

A context truncation policy may remove old delivered-message text only after
the fact is represented in agent memory; it may never remove current legal
actions, doctrine, role, assigned cards, or local observation.

## 11. Storage and replay

For every attempt store:

- run, turn, agent, provider profile;
- context schema version;
- exact structured context snapshot shown in replay;
- prompt-template hash;
- provider request metadata;
- accepted parsed decision or validation failure;
- sanitized raw response up to a small byte limit when debug retention is
  enabled;
- token usage, latency, estimated cost;
- retry/fallback status.

Do not store:

- provider key;
- SDK auth headers;
- hidden provider reasoning;
- routine full wire logs.

Replay uses the structured context, accepted decision, and domain events. It
does not need raw provider response or a new call.

## 12. Safety and prompt injection

This is not a general tool-using agent, so the authority ceiling is low.
Nevertheless:

- label player text and messages as untrusted mission data;
- never concatenate them into an executable tool description;
- expose no network/filesystem/database tools;
- validate action IDs against the current engine list;
- HTML-escape displayed text;
- cap bytes and characters;
- reject disallowed control characters;
- keep secrets out of context;
- log attempted instruction override as ordinary text, not a security event
  unless it crosses an API boundary.

A player may instruct its own unit badly. That is allowed. It cannot gain
engine authority.

## 13. Model migration

When the official model changes:

1. create a new provider profile ID;
2. run the full evaluation matrix;
3. compare generic and designed baselines;
4. measure invalid output, latency, and cost;
5. preserve old replay compatibility;
6. invalidate no historical certificate;
7. label new certifications with the new execution environment.

Do not silently swap a model under the same ranked profile.

## 14. AI runtime acceptance

- both contexts derive from the same pre-turn snapshot;
- forbidden truth never appears in serialized context fixtures;
- all examples validate against decision schema;
- every invalid-output category has a test;
- retry never adds knowledge;
- fallback is deterministic;
- provider timeout cannot double-advance;
- no live call occurs without a successful budget reservation;
- replay requires zero calls;
- scripted mode covers full onboarding and happy path;
- generic-baseline gate passes on the pinned official live profile;
- cost and latency are visible in internal run diagnostics.
