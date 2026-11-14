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
