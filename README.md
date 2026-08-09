# Directive Drift

Directive Drift is a browser-first strategy game about designing the command
system for two autonomous, information-constrained agents. This repository is
currently at Packet P2: the reproducible application skeleton, strict
authored-content boundary, and pure deterministic simulator are present. The
Cold Start materializer and reference solver remain intentionally deferred to
the next packet.

## Prerequisites

- .NET SDK `10.0.202` (pinned by `global.json`)
- Node.js 24 or newer and npm
- Docker, for the container acceptance gate

## Validate the repository

```sh
dotnet build
dotnet test
npm ci --prefix src/DirectiveDrift.Web
npm run lint --prefix src/DirectiveDrift.Web
npm run test --prefix src/DirectiveDrift.Web
npm run build --prefix src/DirectiveDrift.Web
docker build .
```

Run the API locally with:

```sh
dotnet run --project src/DirectiveDrift.Api
```

The process exposes liveness at `/health/live` and readiness at
`/health/ready`. Run the production-shaped container locally with
`docker compose up --build`; its persistent SQLite data directory is backed by
the `directive-drift-data` named volume.

Validate the canonical mission with:

```sh
dotnet run --project tools/DirectiveDrift.ContentCli -- \
  validate content/missions/cold-start/mission.json
```

## Architecture

```text
Core <--- Content -------------------+
  ^                                 |
  +----- Application <--- AI -------+---> Api
                    <--- Persistence+

Api-generated HTTP contracts ----------------> Web (future packet)
```

`DirectiveDrift.Core` is deterministic and BCL-only. Architecture tests check
the allowed project-reference graph and scan Core for forbidden framework,
I/O, clock, network, nondeterministic-random, and presentation dependencies.
The Core owns immutable run state, private observations, legal actions,
simultaneous turn resolution, canonical events, terminal scoring, PCG32 state,
and versioned SHA-256 state hashes. It consumes only materialized opaque IDs;
mission JSON mapping remains a Content concern.

The complete build specification is preserved under [`docs/workpack`](docs/workpack/README.md).
