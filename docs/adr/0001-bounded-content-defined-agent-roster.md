# ADR 0001: Bound the v1 roster while making agent identity content-defined

- Status: Accepted
- Date: 2026-08-09
- Decision owners: Directive Drift implementation team

## Context

The v1 build schema described `agents` as an object with required `kite` and
`wren` properties. The JSON happened to resemble an ID-keyed map, but the
schema and the C# `BuildAgentsDocument` made two Cold Start identities part of
the portable build contract. Mission validation reinforced that coupling by
requiring those two IDs even though mission agent definitions already carry
opaque IDs, labels, starts, health, and capabilities.

The vertical slice still requires exactly two simultaneously acting autonomous
agents. This correction is intended to remove identity coupling before P4
persists builds; it does not expand roster cardinality or gameplay scope.

## Decision

For build contract version `1`, `agents` is an object map with exactly two
properties. Each property name must satisfy the shared opaque-ID definition,
and each property value is an `agentBuild`:

```json
{
  "agents": {
    "agent-id-from-mission": {
      "roleOrder": "...",
      "briefingCardIds": ["...", "...", "...", "..."],
      "moduleId": "..."
    },
    "other-agent-id-from-mission": {
      "roleOrder": "...",
      "briefingCardIds": ["...", "...", "...", "..."],
      "moduleId": "..."
    }
  }
}
```

The C# authoring boundary represents this object as an
`IReadOnlyDictionary<AgentId, AgentBuildDocument>`. Its contract JSON converter
reads and writes `AgentId` values as object property names. After schema
validation, mission-relative validation requires the typed build key set to
equal the selected mission's two agent definitions.

The selected mission remains authoritative for agent labels and capabilities,
the available module definitions, and the briefing-card catalogue from which
the four per-agent selections are eligible. Builds store only opaque
references and player-authored orders. Neither schema nor rule code assigns
meaning based on a Cold Start label.

The two-agent invariant is enforced independently at four boundaries:

1. `mission.schema.json` requires exactly two agent definitions;
2. `build.schema.json` requires exactly two agent-build entries;
3. mission-relative build validation repeats the two-entry invariant and
   requires exact equality with the selected mission roster;
4. `RunStartFactory` requires exactly two distinct materialized agents and
   starts both active.

## Consequences

- Other two-agent missions can choose their own opaque agent IDs and display
  labels without changing the build schema or boundary DTOs.
- A build that substitutes an unknown ID for a mission agent remains
  schema-valid but fails mission-relative cross-reference validation.
- Three-agent play remains invalid in both authored builds and the engine.
- Cold Start fixture and reference-solution text may continue to use its
  authored Kite and Wren identities.
- No new objective types, persistence behavior, API surface, or P4 work are
  introduced.

## Migration implications

The canonical JSON examples use an object keyed by the Cold Start agent IDs,
so their serialized representation remains valid and requires no data rewrite.
The generic baseline intentionally lists those keys in reverse lexical order
to demonstrate that JSON property order does not assign agent roles.
Contract version `1` is retained because this is a pre-persistence correction:
all existing valid v1 JSON remains valid, and only previously hardcoded
identity constraints are relaxed before mission-relative validation.

The C# source contract is intentionally breaking for callers that accessed
`BuildAgentsDocument.Kite` or `.Wren`; those callers must enumerate or index
`BuildDocument.Agents` by the selected mission's `AgentId`. Any generated
client derived from the former fixed-property schema must be regenerated.
There is no generated TypeScript API client or persisted build store in
P0-P3, so this repository needs neither a generated-file update nor a database
migration. If external pre-P4 drafts use the canonical Cold Start IDs, their
JSON remains unchanged.

## Rejected alternatives

- Keep fixed Cold Start properties: this leaks one mission's authored identity
  into a portable boundary and forces label-based branching later.
- Allow an unbounded map: this would silently introduce unsupported roster
  sizes before engine, UI, orchestration, and evaluation design exists.
- Use an array with repeated `agentId` fields: it permits duplicate IDs without
  an additional uniqueness layer and is less direct for mission-relative
  lookup.
