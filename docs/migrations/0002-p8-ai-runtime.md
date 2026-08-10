# Migration 0002 — P8 AI runtime

Migration `20260810010923_P8AiRuntime` advances schema metadata to version `2`
and adds `ProviderDecisionCheckpoints`.

Each row is keyed by turn operation and opaque agent ID. It stores the provider
profile and pre-decision state integrity fields, the exact private serialized
context, prompt-template hash, sanitized diagnostics, token/cost/latency totals,
and the finalized accepted decision or deterministic fallback. An expired
operation lease can therefore resume without dispatching a duplicate provider
call when the stored profile and pre-state hash still match.

The existing builds, runs, snapshots, canonical events, resolved decisions, and
usage ledger are unchanged. Downgrading removes provider checkpoints and resets
schema metadata to version `1`; it does not alter P4/P7 replay data.
