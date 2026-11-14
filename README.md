# CodeCoverage

Self-hosted replacement of `codecov.io` for the repositories of a GitHub organization.

The service ingests the Cobertura, LCOV, OpenCover, JaCoCo and Clover reports produced by GitHub
Actions and Azure Pipelines, publishes commit statuses and one pull request comment on GitHub,
serves SVG badges and shows the coverage history. A repository that works with Codecov keeps its
`codecov.yml`, its status names and its secrets: only the upload step of the pipeline changes.

> **Status:** under construction (milestone M0, foundations). See [docs/PLAN.md](docs/PLAN.md).

## Components

| Component | Role |
|---|---|
| `CodeCoverage.Api` | REST API `/api/v1`, uploads, GitHub webhooks, badges, uploader downloads, Swagger and Scalar; hosts the job worker and applies the migrations |
| `CodeCoverage.Web` | Blazor Server console: GitHub sign-in, repositories, commits, pull requests, annotated source, administration and settings |
| `CodeCoverage.Uploader` | `coverage-upload`, the CLI that detects the CI context and sends the reports (`dotnet tool` and standalone binaries) |
| Database | PostgreSQL 17 (default) or SQL Server 2022 / Azure SQL; it holds the whole state |

The Web and the Api are published on two host names (for example `coverage.example.com` and
`coverage-api.example.com`).

## Prerequisites

- .NET SDK 10.0.403 or a later feature band (`global.json`);
- Docker, for `docker compose` and for the database integration tests (Testcontainers); without
  Docker those tests are skipped.

## Build and test

```shell
dotnet build CodeCoverage.slnx -c Release
dotnet test CodeCoverage.slnx
```

The build treats every warning as an error.

## Run with Docker

```shell
cp .env.example .env                    # then fill in DB_PASSWORD and DP_CERT_PASSWORD
pwsh scripts/new-dp-certificate.ps1     # development Data Protection certificate (secrets/dp.pfx)
docker compose up -d --build            # PostgreSQL; Api on 127.0.0.1:8081, Web on 127.0.0.1:8080
docker compose -f docker-compose.yml -f docker-compose.sqlserver.yml up -d --build   # SQL Server
```

Both hosts listen on the loopback interface only: `Caddyfile.example` puts TLS in front of them.
`pwsh scripts/smoke-compose.ps1` starts the stack on each database provider with throwaway secrets,
waits for `/health/ready` on both hosts, calls `/swagger` and checks that the containers run as a
non-root user, then removes everything.

Only the bootstrap values come from the environment (connection string, database provider, Data
Protection certificate). Every other setting — GitHub App, public URLs, upload limits, processing,
notifications, retention — is stored in the database and edited in the web console, where it is
reloaded without a restart.

Until the web console manages them, an organization is admitted and its upload tokens are created
from the Api container. The command runs next to the running Api and exits without starting a
second server:

```shell
docker compose exec api dotnet CodeCoverage.Api.dll admin admit-organization --login my-org --account-id 123456
docker compose exec -T api dotnet CodeCoverage.Api.dll admin create-upload-token --owner my-org --scope organization --label ci
printf '%s' "$CODECOV_TOKEN" | docker compose exec -T api dotnet CodeCoverage.Api.dll admin create-upload-token \
  --owner my-org --scope repository --repository my-repo --label codecov --import-value-stdin
docker compose exec api dotnet CodeCoverage.Api.dll admin revoke-upload-token --owner my-org --id 12
```

A generated token is written once, alone, on the standard output; everything else goes to the
standard error. An imported `CODECOV_TOKEN` is read from the standard input only, so that it never
shows in the arguments of a process, and is never written back. `--forge` (default `github`) names
the forge of the organization.

## Parity with Codecov

`tools/parity/Compare-WithCodecov.ps1` compares an instance with Codecov through both APIs: the
project and flag totals of each commit (within ±0.01 point) and the patch totals of each file of a
pull request. `.github/workflows/parity.yml` runs it daily during the pilot; every gap is explained in
[docs/parity-log.md](docs/parity-log.md). See `tools/parity/README.md`;
`pwsh tools/parity/Test-CompareWithCodecov.ps1` runs it on a frozen sample, without network.

## Documentation

| Document | Content |
|---|---|
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Architecture, data model, upload contract, pipeline, GitHub integration, deployment |
| [docs/CONVENTIONS.md](docs/CONVENTIONS.md) | Code conventions of the project |
| [docs/PLAN.md](docs/PLAN.md) | Implementation plan, cut into micro-features |
| [AGENT.md](AGENT.md) | Rules for contributors and coding agents |
| [.github/SECURITY.md](.github/SECURITY.md) | Security policy |

## Shared files

`.editorconfig` and `GitVersion.yml` follow the project conventions
([docs/CONVENTIONS.md](docs/CONVENTIONS.md) §2). They belong to this repository: no template
synchronises them, change them here.

## License

Apache License 2.0: see [LICENSE](LICENSE). The source headers keep the copyright notice of the
author; the licence grants the rights.
