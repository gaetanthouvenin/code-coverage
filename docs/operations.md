# Operations

How to run a CodeCoverage instance in production. The design behind these steps is in
[ARCHITECTURE.md](ARCHITECTURE.md) §12 (in French); this guide is the operator's view.

## Alerts

The hosts export their metrics through OTLP only (`OTEL_EXPORTER_OTLP_ENDPOINT`, a bootstrap value;
meter `CodeCoverage`, ARCHITECTURE §12.6). [`deploy/observability`](../deploy/observability) turns them
into Prometheus alerts:

- `alerts.yml`: the rules, with their thresholds (never in the configuration of the hosts);
- `alerts.test.yml`: their unit tests, run by CI with `promtool test rules`;
- `docker-compose.observability.yml`: an overlay that adds an OpenTelemetry Collector (OTLP in,
  Prometheus exporter out) and Prometheus, both pinned by digest:

  ```bash
  docker compose -f docker-compose.yml -f deploy/observability/docker-compose.observability.yml up -d
  ```

  Firing alerts are listed on <http://localhost:9090/alerts>.

The overlay sends no notification. Wire Prometheus to the receiver of your choice (Alertmanager with
mail or chat, a hosted Prometheus) outside this repository: receiver URLs and passwords are secrets
and never go into these files.

Metrics are not split by organization (ADR-013): the monitoring stack sees the repository names of
every organization (`uploads_received_total{repo}`) and must be restricted to the operators of the
instance, like the database backups.

### The alerts

| Alert | Expression | Meaning | First diagnostic |
|---|---|---|---|
| `CodeCoverageDeadJobs` | `max(jobs_dead) > 0` for 1 min | A job used all its attempts: the notification, parse or import it carried never happened. | `/admin/operations`, read the last error of the dead job, fix the cause, then retry it (`POST /api/v1/ops/jobs/{id}/retry`). Dead jobs stay until retried or purged after `Retention:JobDays`. |
| `CodeCoverageJobQueueStalled` | `max(job_oldest_queued_age_seconds) > 600` for 2 min | The oldest runnable job has waited more than 10 minutes. | Is an Api with `Worker:Enabled` running? Its logs, the database load. |
| `CodeCoverageWorkerHeartbeatStale` | `max(worker_heartbeat_age_seconds) > 120` for 1 min | No worker has beaten for 2 minutes (a worker beats every 15 s and writes `WorkerHeartbeats`). | Start an Api with the worker, or read why it stopped. |
| `CodeCoverageWorkerHeartbeatMissing` | no `worker_heartbeat_age_seconds` while the job gauges are exported, for 5 min | No worker ever beat, or its last beat was purged (older than a day), where the stale rule resolves. | Start an Api with the worker, or read why it stopped. |
| `CodeCoverageMetricsAbsent` | `absent_over_time(job_oldest_queued_age_seconds[5m])` | No Api has exported the job gauges for 5 minutes. | Every Api down, or the OTLP endpoint, the collector or Prometheus broken. |
| `CodeCoverageWebhookSignatureFailures` | `sum(increase(webhook_signature_failures_total[10m])) > 0`, or a series absent 10 min ago now above 0 | Webhook deliveries with an invalid signature. | `GitHub:WebhookSecret` out of step with the App after a rotation, or forged requests. |
| `CodeCoverageGitHubQuotaLow` | recorded remaining / limit `< 0.10` per installation, for 5 min | Less than 10 % of the GitHub quota of an installation is left. | Jobs that call GitHub wait for the reset without losing an attempt; look for a burst (reprocess, sweep). |

### Metric names

The collector exports the names of the meter as they are (`add_metric_suffixes: false`): the names
already carry their unit (`_seconds`) and `_total`. A gauge read from the database (`jobs_dead`,
`worker_heartbeat_age_seconds`, `jobs_queued`, `job_oldest_queued_age_seconds`) is sampled every 30 s
by each Api, only while a listener collects the job gauges. So are `github_ratelimit_limit` and
`github_ratelimit_recorded_remaining` (label `installation`): both come from the same `GitHubQuotas`
rows, and a quota whose reset time has passed counts as full. `github_ratelimit_remaining` reports the
last response this process read; the alert does not use it. Counters reset when a host restarts: rules use `increase()`.

### Checking the rules

```bash
docker run --rm --entrypoint promtool -v "$PWD/deploy/observability:/rules:ro" -w /rules prom/prometheus:v3.5.0 test rules alerts.test.yml
```

### Provoking the alerts on a local stack

With the overlay running (and `OTEL_METRIC_EXPORT_INTERVAL` at 10 s, as the overlay sets it):

| Alert | How |
|---|---|
| `CodeCoverageDeadJobs` | Insert a row of `Jobs` in the state `Dead`, or let a job fail its last attempt. |
| `CodeCoverageWebhookSignatureFailures` | `POST /api/v1/github/webhooks` with a wrong `X-Hub-Signature-256` (401). |
| `CodeCoverageWorkerHeartbeatStale` | Restart the Api with `Worker__Enabled=false`. |
| `CodeCoverageWorkerHeartbeatMissing` | Start a fresh instance with `Worker__Enabled=false` (no beat is ever written). |
| `CodeCoverageJobQueueStalled` | With no worker, queue a job whose `RunAfter` is more than 10 minutes old. |
| `CodeCoverageMetricsAbsent` | Stop the Api and the Web (the collector keeps a series 5 minutes, then the rule waits 5 more). |
| `CodeCoverageGitHubQuotaLow` | Needs GitHub answers with a low `x-ratelimit-remaining`; covered by `alerts.test.yml`. |

## Production on a VM

[`deploy/vm`](../deploy/vm) runs the whole instance on one Docker host (M4.02): Caddy (TLS, ACME),
PostgreSQL 17, the Api (REST API, job worker, migrations), the Web (console) and a backup container.
Images only: the Api and the Web come from the registry of the CI at the version of a release, the
other images are public and pinned by digest.

**PostgreSQL only.** A VM on SQL Server has no backup recipe here; production on SQL Server goes
through Azure SQL and its automatic backups (the Azure deployment, M4.03).

### First deployment

1. A VM with Docker (Compose 2.24 or later), ports 80 and 443 open to the Internet, and two DNS names
   pointing to it: one for the Api (reached by GitHub, the CI agents and the badge proxies), one for
   the Web console (ADR-011).
2. Copy `deploy/vm` to the VM, then `cp .env.example .env` and fill in every value: `IMAGE_PREFIX` and
   `TAG` (a release, without the `v`), the host names, `ACME_EMAIL`, `DB_PASSWORD`,
   `DP_CERT_PASSWORD`.
3. Put the Data Protection certificate (PKCS#12) at `DP_CERT_FILE` (`./secrets/dp.pfx`). Use a managed
   certificate; `scripts/new-dp-certificate.ps1` makes a self-signed one. **Back it up apart from the
   database** (a password manager, a vault): without it the stored secrets (GitHub App key, webhook
   secret, OAuth secret, protected tokens) are unreadable and must be typed again; the coverage data
   survives.
4. `docker compose -f docker-compose.prod.yml pull`, then `docker compose -f docker-compose.prod.yml up -d`.
   The Api migrates the schema at start; the Web stays not ready until the schema is current.
5. Open `https://<web host>/setup` to create the GitHub App from its manifest, then admit the first
   organization from `/admin/organizations`.

Caddy runs at the fixed address `172.30.0.10` of the `edge` network (subnet `172.30.0.0/24`), the only
proxy the Api and the Web trust for `X-Forwarded-For` and `X-Forwarded-Proto` (the Web builds the https
callback of the GitHub sign-in from it). For a Caddy installed on the VM itself, use `Caddyfile.example`
at the root with the root `docker-compose.yml` instead.

### Update

Set the new `TAG` in `.env`, pull, then **start the Api first**, which migrates the schema, and the
Web after it:

```bash
docker compose -f docker-compose.prod.yml pull
docker compose -f docker-compose.prod.yml up -d api
docker compose -f docker-compose.prod.yml up -d web
```

A migration is forward only: before an update, take a backup (below, "On demand").

### Backups

The `backup` service runs `pg_dump -Fc` at start and then every `BACKUP_INTERVAL_HOURS` (24) into the
`backups` volume, keeps `BACKUP_RETENTION_DAYS` days (14), and turns **unhealthy** when the last
success is older than 26 hours (`docker compose ps`). A dump holds **every organization** of the
instance (ADR-013): whoever reads the backups reads all of them; restrict the storage accordingly.

**Off-machine copy.** The dumps on the VM do not survive the loss of the VM. Set `BACKUP_HOOK` to a
shell command run on each new dump, its path in `$1`, that copies it to storage off the machine (an
object store, another host). The recovery point objective of ARCHITECTURE §12.5, 24 hours at most,
holds only with this copy in place.

**On demand** (before an update):

```bash
docker compose -f docker-compose.prod.yml run --rm -e BACKUP_ONCE=true backup
```

### Restore

Quarterly, restore the latest dump on a test VM and check that the Api is ready on it. The restored
copy holds the GitHub App key and the queued jobs of production: **its worker stays off**, its backup
service is not started and its `BACKUP_HOOK` is empty, so that it neither acts on the real repositories
nor writes over the off-machine copies of production:

```bash
# On the test VM, with the same deploy/vm and a fresh database volume. In its .env: WORKER_ENABLED=false
# and BACKUP_HOOK= (empty).
docker compose -f docker-compose.prod.yml up -d db
docker compose -f docker-compose.prod.yml cp ./codecoverage-<stamp>.dump db:/tmp/restore.dump
docker compose -f docker-compose.prod.yml exec db pg_restore -U cc -d codecoverage --no-owner --exit-on-error /tmp/restore.dump
docker compose -f docker-compose.prod.yml up -d --no-deps api web   # never the backup service
```

The test VM needs the same Data Protection certificate to read the stored secrets. A restored copy runs
its worker (`WORKER_ENABLED=true`) and its backup service only when it **replaces** production, once
the old instance is stopped.
`scripts/test-backup-restore.ps1` runs the same drill on local Docker with a sample schema (backup
image, `pg_restore` into a fresh server, row counts compared); it is skipped with a reason without
Docker.

### The administration command line

The command line of the Api stays as the documented break-glass path (PLAN §11-4): admitting an
organization or creating an upload token without the console.

```bash
docker compose -f docker-compose.prod.yml exec api dotnet CodeCoverage.Api.dll admin admit-organization --login my-org --account-id 123456
docker compose -f docker-compose.prod.yml exec api dotnet CodeCoverage.Api.dll admin create-upload-token --owner my-org --scope organization --label ci
```

What it does is audited under the actor `[command-line]`.
