# Directive Drift

Directive Drift is a browser-first strategy game about designing the command
system for two autonomous, information-constrained agents. This repository is
currently at Packet P3: the reproducible application skeleton, strict
authored-content boundary, pure deterministic simulator, Cold Start
materializer, reference solver, and scripted evaluation harness are present.
Application persistence and the HTTP run spine remain intentionally deferred
to Packet P4.

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

This performs schema/reference validation and proves every fixed practice and
server-held certification variant with interchangeable and no-damage policies.
Run the scripted anti-shortcut tutorial fixtures with:

```sh
dotnet run --project tools/DirectiveDrift.EvaluationCli -- \
  --build examples/generic-optimal-build.json \
  --provider-mode scripted --matrix tutorial --repetitions 1

dotnet run --project tools/DirectiveDrift.EvaluationCli -- \
  --build examples/designed-build.json \
  --provider-mode scripted --matrix tutorial --repetitions 1
```

The evaluation CLI also accepts `practice`, `certification`, `pinned`, `all`,
or a comma-separated variant-ID matrix. P3 deliberately supports only
`scripted` provider mode; live providers remain out of scope.

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

Current packet evidence and the P3 content-fairness corrections are recorded
in [`docs/build-status.md`](docs/build-status.md).

The complete build specification is preserved under [`docs/workpack`](docs/workpack/README.md).
