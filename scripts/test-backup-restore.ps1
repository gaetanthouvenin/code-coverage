#Requires -Version 7.2
<#
.SYNOPSIS
  Dump-and-restore drill of the backup of deploy/vm (M4.02), on local Docker.

.DESCRIPTION
  Builds the backup image of deploy/vm/backup, starts a throwaway PostgreSQL (the pinned image of
  docker-compose.prod.yml) with a sample schema of two organizations, runs one backup with the image
  (BACKUP_ONCE), checks the healthcheck, restores the dump with pg_restore into a fresh PostgreSQL,
  and compares the row counts of every table. Everything it creates is removed at the end.

  Without Docker, the drill is skipped with an explicit reason and exits 0. Any failure exits 1.
  The drill on the real data of a VM (restoring a production dump on a test VM) is an operator task:
  docs/operations.md, "Restore".

.EXAMPLE
  pwsh scripts/test-backup-restore.ps1
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent
$postgres = 'postgres:17-alpine@sha256:3540c0362c809df084ffac0ae69fe794515f9de92692f02178bcdce0ef0302ac'

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
  Write-Output 'SKIPPED: docker is not installed; the backup and restore drill needs Docker.'
  exit 0
}

& docker info --format '{{.ServerVersion}}' *> $null
if ($LASTEXITCODE -ne 0) {
  Write-Output 'SKIPPED: the Docker engine does not answer; the backup and restore drill needs Docker.'
  exit 0
}

$run = [guid]::NewGuid().ToString('N').Substring(0, 8)
$network = "cc-backup-drill-$run"
$source = "cc-backup-source-$run"
$target = "cc-backup-target-$run"
$volume = "cc-backup-dumps-$run"
$image = "cc-backup-drill:$run"
$password = [guid]::NewGuid().ToString('N')

function Invoke-Docker {
  $output = & docker @args 2>&1
  if ($LASTEXITCODE -ne 0) { throw "docker $($args -join ' ') failed: $output" }
  return $output
}

function Wait-Postgres([string] $Container) {
  for ($i = 0; $i -lt 60; $i++) {
    & docker exec $Container pg_isready -U cc -d codecoverage *> $null
    if ($LASTEXITCODE -eq 0) { return }
    Start-Sleep -Milliseconds 500
  }
  throw "$Container did not become ready."
}

function Get-Counts([string] $Container) {
  $sql = "SELECT 'organizations', count(*) FROM organizations UNION ALL SELECT 'repositories', count(*) FROM repositories UNION ALL SELECT 'commits', count(*) FROM commits ORDER BY 1"
  return (Invoke-Docker exec $Container psql -U cc -d codecoverage -At -F '=' -c $sql) -join ';'
}

try {
  Invoke-Docker build --quiet -t $image (Join-Path $root 'deploy/vm/backup') | Out-Null
  Invoke-Docker network create $network | Out-Null
  Invoke-Docker volume create $volume | Out-Null
  Invoke-Docker run -d --name $source --network $network -e POSTGRES_DB=codecoverage -e POSTGRES_USER=cc -e "POSTGRES_PASSWORD=$password" $postgres | Out-Null
  Wait-Postgres $source

  # A sample of two organizations: a backup holds every organization of the instance.
  $seed = @'
CREATE TABLE organizations (id bigint PRIMARY KEY, login text NOT NULL);
CREATE TABLE repositories (id bigint PRIMARY KEY, organization_id bigint NOT NULL REFERENCES organizations, name text NOT NULL);
CREATE TABLE commits (id bigserial PRIMARY KEY, repository_id bigint NOT NULL REFERENCES repositories, sha text NOT NULL, coverage numeric(9,5));
INSERT INTO organizations VALUES (1, 'first-org'), (2, 'second-org');
INSERT INTO repositories VALUES (10, 1, 'app'), (20, 2, 'lib');
INSERT INTO commits (repository_id, sha, coverage) SELECT 10 + (g % 2) * 10, md5(g::text), g % 100 FROM generate_series(1, 5000) g;
'@
  Invoke-Docker exec $source psql -U cc -d codecoverage -v ON_ERROR_STOP=1 -q -c $seed | Out-Null
  $before = Get-Counts $source

  # One backup with the image, as the backup service runs it.
  Invoke-Docker run --rm --network $network -v "${volume}:/backups" -e "PGHOST=$source" -e PGUSER=cc -e PGDATABASE=codecoverage -e "PGPASSWORD=$password" -e BACKUP_ONCE=true $image | Out-Host
  Invoke-Docker run --rm -v "${volume}:/backups" --entrypoint /bin/sh $image /opt/backup/healthcheck.sh | Out-Null

  # The healthcheck follows the interval: 2 h of margin past BACKUP_INTERVAL_HOURS.
  $ages = @(
    @{ Hours = 1; Age = 2 * 3600; Healthy = $true },
    @{ Hours = 1; Age = 4 * 3600; Healthy = $false },
    @{ Hours = 48; Age = 30 * 3600; Healthy = $true },
    @{ Hours = 24; Age = 27 * 3600; Healthy = $false })
  foreach ($case in $ages) {
    $probe = "stamp=`$((`$(date -u +%s) - $($case.Age))); mkdir -p /tmp/b; echo `$stamp > /tmp/b/.last-success; BACKUP_DIRECTORY=/tmp/b BACKUP_INTERVAL_HOURS=$($case.Hours) /bin/sh /opt/backup/healthcheck.sh"
    & docker run --rm --entrypoint /bin/sh $image -c $probe *> $null
    if (($LASTEXITCODE -eq 0) -ne $case.Healthy) {
      throw "The healthcheck of a $($case.Age / 3600) h old backup every $($case.Hours) h is not $($case.Healthy ? 'healthy' : 'unhealthy')."
    }
  }
  $dump = (Invoke-Docker run --rm -v "${volume}:/backups" --entrypoint /bin/sh $image -c 'ls /backups/codecoverage-*.dump') | Select-Object -First 1
  if (-not $dump) { throw 'The backup wrote no dump.' }

  # The restore, into a fresh server.
  Invoke-Docker run -d --name $target --network $network -e POSTGRES_DB=codecoverage -e POSTGRES_USER=cc -e "POSTGRES_PASSWORD=$password" $postgres | Out-Null
  Wait-Postgres $target
  Invoke-Docker run --rm --network $network -v "${volume}:/backups:ro" -e "PGPASSWORD=$password" --entrypoint pg_restore $image `
    --host $target --username cc --dbname codecoverage --no-owner --exit-on-error $dump.Trim() | Out-Null
  $after = Get-Counts $target

  if ($before -cne $after) {
    throw "The restored database differs: before $before, after $after."
  }

  # The restored copy of the drill: the worker of its Api is off.
  $env:TAG = 'drill'; $env:DB_PASSWORD = 'x'; $env:DP_CERT_PASSWORD = 'x'; $env:WORKER_ENABLED = 'false'
  try {
    $config = (& docker compose --env-file (Join-Path $root 'deploy/vm/.env.example') -f (Join-Path $root 'deploy/vm/docker-compose.prod.yml') config --format json) -join "`n" | ConvertFrom-Json
  }
  finally {
    Remove-Item Env:TAG, Env:DB_PASSWORD, Env:DP_CERT_PASSWORD, Env:WORKER_ENABLED -ErrorAction SilentlyContinue
  }
  if ($config.services.api.environment.Worker__Enabled -cne 'false') { throw 'WORKER_ENABLED=false does not turn the worker of the Api off.' }

  Write-Output "Backup and restore drill passed: $after."
}
catch {
  Write-Output "FAILED: $($_.Exception.Message)"
  exit 1
}
finally {
  & docker rm -f $source $target *> $null
  & docker volume rm $volume *> $null
  & docker network rm $network *> $null
  & docker image rm $image *> $null
}
