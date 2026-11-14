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
through Azure SQL and its automatic backups ([deployment-azure.md](deployment-azure.md)).

**A host with `COMPOSE_FILE` in its `.env`** (the host of "Continuous deployment", whose proxy runs in a
container): every command of this section runs as `docker compose --project-directory <directory> …`
**without `-f`**. A `-f docker-compose.prod.yml` would override `COMPOSE_FILE` and drop
`docker-compose.host.yml`: Caddy would come back, and the volumes and the network of the host would
be lost. "Restore" gives the commands of that host in full.

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

On the host of "Continuous deployment" (`COMPOSE_FILE` in `.env`), restoring a dump **in place** of
the production database, e.g. to return to an older version: the database is dropped and created
again, since `pg_restore` would leave the tables a newer migration added.

```bash
cd /data03/docker/code-coverage
docker compose --project-directory . stop web api
docker compose --project-directory . exec db psql -U cc -d postgres -c 'DROP DATABASE codecoverage WITH (FORCE)'
docker compose --project-directory . exec db createdb -U cc codecoverage
docker compose --project-directory . cp ./data/backups/codecoverage-<stamp>.dump db:/tmp/restore.dump
docker compose --project-directory . exec db pg_restore -U cc -d codecoverage --no-owner --exit-on-error /tmp/restore.dump
# set TAG in .env to the version the dump belongs to, then the Api first:
docker compose --project-directory . up -d api
docker compose --project-directory . up -d web
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

## Continuous deployment

Every `v*` tag whose CI succeeds, pre-releases included, is deployed to the production host by the
`deploy` job of [`ci-cd.yml`](../.github/workflows/ci-cd.yml) (M4.02b). The job runs on a self-hosted
runner of that host, in the GitHub environment `production`, and waits for its reviewer to approve.
It never runs on a pull request. It runs
[`scripts/Deploy-Production.ps1`](../scripts/Deploy-Production.ps1) from the checkout of the tag on
the compose of [`deploy/vm`](../deploy/vm), here behind the Nginx Proxy Manager (NPX) already running
on the host instead of Caddy.

What a deployment does, in this order:

1. Checks the tools of the host, then that the commit of the tag is on `origin/main` (a stable
   version) or on `origin/develop` or a `release/*` branch (a pre-release).
2. Logs in to GHCR with the token of the job, in a Docker configuration of the job, never in
   `~/.docker`.
3. Checks `.env`, `secrets/dp.pfx` and the data directories, and compares the version with the
   current `TAG` by SemVer 2.0 precedence (`1.2.0-beta.9 < 1.2.0-beta.10 < 1.2.0-rc.1 < 1.2.0`). The
   same version is a redeployment, and an older one is refused. It also checks that the Docker
   network of `PROXY_NETWORK` exists.
4. Copies an explicit list of `deploy/vm` files into `DEPLOY_DIR`, keeping the files it replaces
   until the Api starts. It never touches `.env` nor `secrets/`.
5. Pulls the Api and Web images of the version, and refuses them unless their
   `org.opencontainers.image.revision` label is the commit of the tag.
6. Takes a backup (`BACKUP_ONCE`). Only then does it write the new `TAG` to `.env`, the previous one
   going to `.env.previous-tag` (a redeployment leaves that file as it was).
7. Starts the Api and waits for it to be healthy (300 s; a long migration stays `starting` or
   `unhealthy` meanwhile), then does the same for the Web.
8. Checks `/health/ready` of both hosts through NPX on the port 443 of the host (`curl --resolve`,
   which avoids the NAT loop of a home server). Then it compares the served
   `/api/v1/uploader/linux-x64.sha256` with the asset of the release.
9. Removes the `-api` and `-web` images other than the current version, the previous `TAG` and the
   version of `.env.previous-tag`. It never runs a global prune: the host is not dedicated.

The script defaults to a dry run, and the job passes `-Execute`. A dry run by hand on the host prints
what it would do and changes nothing:

```bash
pwsh scripts/Deploy-Production.ps1 -Version 1.2.3 -Revision <commit of v1.2.3> -Directory /data03/docker/code-coverage
```

### Preparing the host

**Tools:** PowerShell (`pwsh`) 7.4 or later, `curl`, Docker with the compose plugin 2.24 or later.
The compose plugin must be installed **system-wide** (the apt package `docker-compose-plugin`, in
`/usr/libexec/docker/cli-plugins` or `/usr/lib/docker/cli-plugins`), not in `~/.docker/cli-plugins`:
the job points `DOCKER_CONFIG` to a directory of its own, where a per-user plugin is not found.

**Runner user.** The user of the runner service is in the `docker` group, which is **equivalent to
root** on the host: whoever runs a job on this runner controls the host. Hence the dedicated label,
the pre-job hook and, above all, the tag ruleset below.

**What may run on the runner is decided by who may push a `v*` tag.** The hook accepts any job of the
`ci-cd.yml` of a `v*` tag, and that file comes from the tag itself: a tag can carry a `ci-cd.yml` with
a job that targets `cc-production` and declares no environment. The required reviewer of
`production` only holds the `deploy` job; it does not protect the runner. The tag ruleset on
`refs/tags/v*` ("GitHub settings" below) is therefore **mandatory**, in place before the runner gets
its label, and checked again whenever the collaborators of the repository change.

**Production directory** (`DEPLOY_DIR=/data03/docker/code-coverage`, `DATA_DIR=$DEPLOY_DIR/data`):

```bash
sudo mkdir -p /data03/docker/code-coverage/secrets /data03/docker/code-coverage/data/{db,backups,api,web}
sudo chown -R runner:runner /data03/docker/code-coverage          # the user of the runner service
sudo chown 70:70 /data03/docker/code-coverage/data/db /data03/docker/code-coverage/data/backups   # postgres alpine
sudo chown 1001:1001 /data03/docker/code-coverage/data/api /data03/docker/code-coverage/data/web  # Api and Web
sudo chmod 700 /data03/docker/code-coverage/secrets
# after copying the certificate:
sudo chown 1001:1001 /data03/docker/code-coverage/secrets/dp.pfx && sudo chmod 400 /data03/docker/code-coverage/secrets/dp.pfx
# after writing .env:
chmod 600 /data03/docker/code-coverage/.env
```

**`.env`.** It is filled as in "First deployment" above, plus the lines of the NPX variant of
[`.env.example`](../deploy/vm/.env.example):

- `COMPOSE_FILE=docker-compose.prod.yml:docker-compose.host.yml`: compose reads it from `.env`, so
  every command is a plain `docker compose --project-directory /data03/docker/code-coverage …`.
- `PROXY_NETWORK`: the existing Docker network of NPX.
- `PROXY_NETWORK_CIDR`: its subnet, from
  `docker network inspect <network> --format '{{(index .IPAM.Config 0).Subnet}}'`.
- `DATA_DIR=/data03/docker/code-coverage/data`.

`ACME_EMAIL` is unused there but must keep a value, because compose checks every variable of its
files.

[`docker-compose.host.yml`](../deploy/vm/docker-compose.host.yml) does four things:

- disables Caddy;
- joins the Api and the Web to the NPX network without publishing a port;
- replaces `ForwardedHeaders__KnownProxies` with `ForwardedHeaders__KnownNetworks=${PROXY_NETWORK_CIDR}`:
  the address of the NPX container changes when it is recreated, so the Api and the Web trust the
  `X-Forwarded-*` headers of the whole subnet. Attach only trusted containers to that network;
- bind-mounts the volumes under `DATA_DIR`.

`.env` and `dp.pfx` stay on the host, never in GitHub.

**First deployment, by hand.** The images of a private repository are private:

1. Log in once with a classic personal access token limited to `read:packages`
   (`docker login ghcr.io`).
2. Copy `deploy/vm` to `DEPLOY_DIR`.
3. Run `docker compose --project-directory /data03/docker/code-coverage pull`, then `up -d`.
4. Remove the token afterwards (`docker logout ghcr.io`).

The deployments that follow log in with the token of the job. The bind-mounted volumes must exist
before the first `up`. A host that already ran the plain `docker-compose.prod.yml` has named volumes
of the same names with other options, and compose refuses them. Back up, remove the old volumes, and
restore into the new database (see "Restore").

### The NPX proxy hosts

Add two proxy hosts in NPX, both on the network named in `PROXY_NETWORK`:

| | Api | Web |
|---|---|---|
| Domain name | `CC_API_HOST` | `CC_WEB_HOST` |
| Scheme, forward host, port | `http`, `code-coverage-api-1`, `8080` | `http`, `code-coverage-web-1`, `8080` |
| Websockets support | not needed | **on** (Blazor Server keeps a WebSocket open) |
| SSL | a Let's Encrypt certificate, Force SSL, HTTP/2 | the same |

The forward host is the container name of compose (project `code-coverage`), never the service name
`api` or `web`: other stacks on the shared network may use the same name. NPX sends
`X-Forwarded-Proto` and `X-Forwarded-For` by default. The Web needs them to build the `https`
callback of the GitHub sign-in, and the Api trusts them from `PROXY_NETWORK_CIDR` only. An upload
refused with 413 by NPX needs a larger `client_max_body_size` in the advanced configuration of the
Api host.

### The runner

- **Dedicated label.** Add the label `cc-production` to the runner: the job targets
  `[self-hosted, cc-production]`, never `self-hosted` alone. Run no other runner on this host.
- **Pre-job hook.** Install
  [`deploy/vm/runner/job-started.sh`](../deploy/vm/runner/job-started.sh) as root, out of reach of
  the runner user (e.g. `/opt/actions-runner-hooks/job-started.sh`, owned by root, mode 755). Name it
  in the `.env` of the runner directory, then restart the runner service:

  ```bash
  ACTIONS_RUNNER_HOOK_JOB_STARTED=/opt/actions-runner-hooks/job-started.sh
  CC_DEPLOY_REPOSITORY=<owner>/code-coverage
  ```

  The hook fails any job that does not meet all of these conditions:
  - it comes from this repository;
  - its event is `push`;
  - its ref is `refs/tags/v*`;
  - its `GITHUB_WORKFLOW_REF` starts with
    `<owner>/code-coverage/.github/workflows/ci-cd.yml@refs/tags/v`.

  A pull request that targets the label is therefore refused before its first step. Without
  `CC_DEPLOY_REPOSITORY`, every job is refused. `sh deploy/vm/runner/test-job-started.sh` tests it
  offline, and the CI runs that test.

### GitHub settings (owner)

- **Environment `production`:**
  - under deployment branches and tags, replace the branch rule `v*` with a **tag** rule `v*.*.*`;
  - add the owner as **required reviewer**. The job waits for the approval before it starts, and the
    wait does not count in its `timeout-minutes` (20). A pending approval expires after 30 days,
    and the job then fails;
  - add the variable `DEPLOY_DIR` = `/data03/docker/code-coverage`;
  - add the variable `PRODUCTION_URL` = the https address of the Web (`https://<CC_WEB_HOST>`), the
    link the run shows for the environment. It is optional: unset, the run shows no link.
- **Tag ruleset** on `refs/tags/v*` (**mandatory**, see "Preparing the host"): only the owner may
  create, move or delete such a tag. A tag is what deploys, and its `ci-cd.yml` is what runs on the
  runner.
- **Packages** `code-coverage-api` and `code-coverage-web` on GHCR: under "Manage Actions access",
  give this repository the `Read` role, so that the token of the job can pull them.

### Redeploying, returning, bursts

- **Redeploy** the same version with "Re-run job" on the run of the tag, which keeps the context of
  the tag. There is no `workflow_dispatch`.
- **Return to an older version.** A migration is forward only, so this is a manual operation on the
  host:
  1. stop the Web and the Api;
  2. restore the dump taken before the deployment ("Restore", the commands of a host with
     `COMPOSE_FILE`);
  3. check out the older tag in a directory of your own (`git clone --branch v1.2.2 …`): its
     `scripts/Deploy-Production.ps1` copies the `deploy/vm` of its own checkout (`-Source` defaults
     to it), so the files match the version;
  4. log in to GHCR with a classic token limited to `read:packages` (`docker login ghcr.io`): the
     script always pulls;
  5. run that script with the older version, its commit, `-AllowDowngrade` and `-Execute`:

     ```bash
     pwsh scripts/Deploy-Production.ps1 -Version 1.2.2 -Revision <commit of v1.2.2> -Directory /data03/docker/code-coverage -AllowDowngrade -Execute
     ```

  6. log out afterwards (`docker logout ghcr.io`).
- **A failure before the start of the Api** puts back the previous `TAG` and the `deploy/vm` files it
  had replaced; the running containers were not touched. A dump taken by the failed run stays in the
  backups.
- **A failure after the start of the Api** is not rolled back automatically, because the schema may
  have migrated. The job fails and names the data directory, the dump and the steps:
  1. stop the Api and the Web;
  2. run `pg_restore` of the dump;
  3. set `TAG` to the content of `.env.previous-tag` (a redeployment of the same version leaves that
     file as it was, so it still names the version to return to);
  4. run `up -d api`, then `up -d web`.

  The images of the current version, of the previous `TAG` and of the version in
  `.env.previous-tag` are never removed by the cleanup, so that the return finds them on the host.
- **A burst of tags.** The deployments queue in the concurrency group `production` and are never
  cancelled, but GitHub keeps only the running one and the latest pending one. A tag older than the
  deployed version is then refused ("older than the deployed"), as expected.

### Before making the repository public

This is mandatory: a self-hosted runner of a public repository can be reached by the pull requests
of forks.

- Under "Fork pull request workflows from outside collaborators", require an approval for **all**
  outside collaborators.
- Check that the tag ruleset on `refs/tags/v*` is active and lets only the owner create, move or
  delete a tag. This is a blocking check, not a review item: a tag decides what runs on the runner.
- Keep the hook and the dedicated label in place, or unregister the runner.
- Review the visibility of the GHCR packages.
