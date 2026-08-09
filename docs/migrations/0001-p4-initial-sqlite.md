# P4 initial SQLite migration

Migration: `InitialCreate`
Packet: P4 — Application, persistence, and API spine

P0–P3 did not create a database or persist builds/runs. This migration creates
the initial SQLite schema for guest profiles, immutable build versions, runs,
per-turn snapshots, leased turn operations, resolved decisions, canonical
events, usage ledger entries, certification placeholders, and schema metadata.

There is no prior data transformation. Local Development and integration tests
apply the migration automatically. Hosted deployments must opt into the
explicit startup migration step; incompatible databases are never deleted or
silently recreated.
