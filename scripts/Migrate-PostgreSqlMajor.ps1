#Requires -Version 7.4
<#
.SYNOPSIS
  Moves the database of the production directory to the PostgreSQL major version of a checkout (M4.02e).

.DESCRIPTION
  A new PostgreSQL major version does not read the data of the previous one: the database goes through
  a dump and a restore. Run by hand on the Docker host, from the checkout of the release that brings the
  new version (its deploy/vm is -Source), before deploying that release: scripts/Deploy-Production.ps1
  refuses a database of another major version than its db image. Without -Execute it is a dry run: the
  checks run, and each command that would change something is printed instead of run.

  For the host of "Continuous deployment": bind-mounted volumes under DATA_DIR, COMPOSE_FILE in .env.
  Every compose command runs from <Directory> (a relative COMPOSE_FILE, M4.02c).

   1. Checks: .env, DATA_DIR, the db image of the deployed compose (the source version) and the one of
      -Source (the target version, newer), the PG_VERSION of DATA_DIR/db (the source version), and the
      db container running.
   2. Backup (BACKUP_ONCE) by the backup service of the deployed compose, hence by the image of the
      source version; the path of the dump is required.
   3. Row count of every table of the source database.
   4. Stop of the Web, the Api, the backup service, then the database.
   5. The deployed files that -Source replaces are kept in <Directory>/.pg-upgrade-<stamp>; DATA_DIR/db
      is renamed DATA_DIR/db-pg<source>-<stamp>, never removed, and an empty DATA_DIR/db is created for
      the postgres user of the image (70:70, mode 700).
   6. Copy of the compose files, the Caddyfile, .env.example and backup/ of -Source. .env and secrets/
      are never touched; the TAG stays the deployed one.
   7. Start of the new database (it initializes an empty codecoverage database), then pg_restore of the
      dump by the backup service of -Source (the target version).
   8. Row count of every table of the new database, compared with the source.
   9. Start of the Api, wait for its health, then the same for the Web, then the backup service.

  On a failure after the stop of the database, the migration goes back: the new data directory is
  renamed DATA_DIR/db-pg<target>-failed-<stamp>, the old one gets its name back, the files are put
  back, and the database, the Api, the Web and the backup service start again on the source version.
  The old data directory and the saved files are removed by the owner, once the new version runs.

.PARAMETER Directory
  The production directory of the host (DEPLOY_DIR), e.g. /data03/docker/code-coverage.

.PARAMETER Source
  The deploy/vm directory of the release that brings the new major version; deploy/vm of this checkout
  by default.

.PARAMETER HealthTimeoutSeconds
  How long a service may stay starting or unhealthy before the migration fails.

.PARAMETER HealthPollSeconds
  The interval between two health reads.

.PARAMETER Execute
  Runs the migration. Without it, a dry run.

.EXAMPLE
  pwsh scripts/Migrate-PostgreSqlMajor.ps1 -Directory /data03/docker/code-coverage

.EXAMPLE
  pwsh scripts/Migrate-PostgreSqlMajor.ps1 -Directory /data03/docker/code-coverage -Execute
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory)]
  [string] $Directory,
  [string] $Source = (Join-Path (Split-Path -Parent $PSScriptRoot) 'deploy/vm'),
  [ValidateRange(1, 3600)]
  [int] $HealthTimeoutSeconds = 300,
  [ValidateRange(1, 60)]
  [int] $HealthPollSeconds = 5,
  [switch] $Execute
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Every failure ends as one error line, unwrapped, and the exit code 1.
trap {
  Write-Host "::error::$($_.Exception.Message)"
  exit 1
}

# The files of deploy/vm copied by scripts/Deploy-Production.ps1. .env and secrets/ never.
$copiedFiles = @(
  'docker-compose.prod.yml',
  'docker-compose.host.yml',
  'Caddyfile',
  '.env.example',
  'backup/backup.sh',
  'backup/healthcheck.sh',
  'backup/Dockerfile'
)

# The row count of every table, one line per table: schema.table=count.
$countSql = "SELECT table_schema || '.' || table_name || '=' || (xpath('/row/c/text()', query_to_xml(format('SELECT count(*) AS c FROM %I.%I', table_schema, table_name), false, true, '')))[1]::text FROM information_schema.tables WHERE table_type = 'BASE TABLE' AND table_schema NOT IN ('pg_catalog', 'information_schema') ORDER BY 1"

#region Helpers

function Write-Step {
  param([string] $Message)

  Write-Host "migrate: $Message"
}

function Read-DotEnv {
  # KEY=VALUE lines; comments, blank lines and surrounding quotes are ignored.
  param([string] $Path)

  $values = @{}

  foreach ($line in [System.IO.File]::ReadAllLines($Path)) {
    if ($line -match '^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=(.*)$') {
      $values[$Matches[1]] = $Matches[2].Trim().Trim('"').Trim("'")
    }
  }

  return $values
}

function Get-ComposeServiceImage {
  # The image of a service of a compose file of ours: the first `image:` line of its block.
  param([string] $Path, [string] $Service)

  $inService = $false

  foreach ($line in [System.IO.File]::ReadAllLines($Path)) {
    if ($line -match '^  (\S[^:]*):\s*$') {
      $inService = $Matches[1] -eq $Service
    }
    elseif ($inService -and $line -match '^    image:\s*(\S+)') {
      return $Matches[1]
    }
  }

  throw "The image of the $Service service of $Path is not found."
}

function Get-PostgresMajor {
  param([string] $Image, [string] $Path)

  if ($Image -notmatch '^postgres:(\d+)') {
    throw "The db image of $Path ('$Image') is not a postgres:<major> image."
  }

  return [int]$Matches[1]
}

function Invoke-Docker {
  # Runs docker and returns its output lines (stderr included); throws on a non-zero exit code unless
  # -AllowFailure, which returns the output and leaves $LASTEXITCODE to the caller.
  param([string[]] $Arguments, [switch] $AllowFailure)

  $output = & docker @Arguments 2>&1 | ForEach-Object { "$_" }

  if ($LASTEXITCODE -ne 0 -and -not $AllowFailure) {
    $output | ForEach-Object { Write-Host "  $_" }
    throw "docker $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
  }

  return @($output)
}

function Invoke-Compose {
  # Runs from the production directory: a relative path of COMPOSE_FILE is resolved from the current
  # directory of compose. The previous directory is put back even when the command fails.
  param([string[]] $Arguments, [switch] $AllowFailure)

  Push-Location -LiteralPath $Directory

  try {
    return Invoke-Docker -Arguments (@('compose', '--project-directory', $Directory) + $Arguments) -AllowFailure:$AllowFailure
  }
  finally {
    Pop-Location
  }
}

function Invoke-Change {
  # A command that changes something: printed in a dry run, run otherwise.
  param([string] $Description, [scriptblock] $Action)

  if ($Execute) {
    Write-Step $Description
    & $Action
  }
  else {
    Write-Step "DRY RUN, would $Description"
  }
}

function Invoke-AsRoot {
  # A shell command run as root in a container of an image, with DATA_DIR mounted on /data: the data
  # directories belong to the postgres user of the image, not to the user of this script.
  param([string] $Image, [string] $Command)

  return Invoke-Docker -Arguments @('run', '--rm', '--user', '0:0', '--entrypoint', '/bin/sh', '-v', "${dataDir}:/data", $Image, '-c', $Command)
}

function Get-PostgreSqlDataVersion {
  # The PG_VERSION files of DATA_DIR/db: at its root (the layout up to 17) and in <major>/docker (the
  # layout of 18 and later). Returns @{ Path; Major } entries. Read through a container of the image
  # with -Execute; directly in a dry run, and $null when this user cannot read it.
  param([string] $Image)

  $lines = @()

  if ($Execute) {
    $lines = Invoke-AsRoot -Image $Image -Command 'cd /data/db && for f in PG_VERSION */docker/PG_VERSION; do if [ -f "$f" ]; then echo "$f $(cat "$f")"; fi; done'
  }
  else {
    try {
      $root = Join-Path $dbData 'PG_VERSION'
      $null = [System.IO.Directory]::GetFiles($dbData)

      if (Test-Path -LiteralPath $root -PathType Leaf) {
        $lines += "PG_VERSION $([System.IO.File]::ReadAllText($root).Trim())"
      }

      foreach ($major in [System.IO.Directory]::GetDirectories($dbData)) {
        $docker = Join-Path $major 'docker'

        if (Test-Path -LiteralPath $docker -PathType Container) {
          $null = [System.IO.Directory]::GetFiles($docker)
          $file = Join-Path $docker 'PG_VERSION'

          if (Test-Path -LiteralPath $file -PathType Leaf) {
            $lines += "$(Split-Path -Leaf $major)/docker/PG_VERSION $([System.IO.File]::ReadAllText($file).Trim())"
          }
        }
      }
    }
    catch [System.UnauthorizedAccessException] {
      return $null
    }
  }

  # The comma keeps an empty list from being unrolled into $null, which means unreadable.
  return , @($lines | ForEach-Object { if ("$_" -match '^(\S+) (\S+)$') { @{ Path = $Matches[1]; Major = $Matches[2] } } })
}

function Wait-Healthy {
  # Polls the health of the container of a service. starting and unhealthy are tolerated until the
  # timeout; exited, dead and restarting fail at once.
  param([string] $Service)

  $deadline = [DateTimeOffset]::UtcNow.AddSeconds($HealthTimeoutSeconds)

  while ($true) {
    $id = (Invoke-Compose -Arguments @('ps', '-a', '-q', $Service) | Select-Object -First 1)

    if (-not $id) {
      throw "The container of $Service is not running."
    }

    $state = (Invoke-Docker -Arguments @('inspect', '--format', '{{.State.Status}} {{if .State.Health}}{{.State.Health.Status}}{{end}}', $id.Trim()) |
                Select-Object -First 1).Trim()
    $parts = $state.Split(' ', 2)
    $status = $parts[0]
    $health = if ($parts.Count -gt 1) { $parts[1] } else { '' }

    if ($status -in 'exited', 'dead', 'restarting') {
      throw "The container of $Service is $status."
    }

    if ($status -eq 'running' -and $health -eq 'healthy') {
      Write-Step "$Service is healthy"
      return
    }

    if ([DateTimeOffset]::UtcNow -ge $deadline) {
      throw "$Service is still '$state' after $HealthTimeoutSeconds seconds."
    }

    Start-Sleep -Seconds $HealthPollSeconds
  }
}

function Wait-DatabaseOverTcp {
  # The healthcheck of the db service uses the Unix socket, which the temporary server of the first
  # initialization also answers: the database is ready for the restore once it answers over TCP.
  $deadline = [DateTimeOffset]::UtcNow.AddSeconds($HealthTimeoutSeconds)

  while ($true) {
    Invoke-Compose -Arguments @('exec', '-T', 'db', 'pg_isready', '-h', '127.0.0.1', '-U', 'cc', '-d', 'codecoverage') -AllowFailure | Out-Null

    if ($LASTEXITCODE -eq 0) {
      return
    }

    if ([DateTimeOffset]::UtcNow -ge $deadline) {
      throw "The new database does not answer over TCP after $HealthTimeoutSeconds seconds."
    }

    Start-Sleep -Seconds $HealthPollSeconds
  }
}

function Get-RowCount {
  return ((Invoke-Compose -Arguments @('exec', '-T', 'db', 'psql', '-U', 'cc', '-d', 'codecoverage', '-At', '-v', 'ON_ERROR_STOP=1', '-c', $countSql) |
             Where-Object { $_ -match '=' }) -join "`n")
}

#endregion

$Directory = [System.IO.Path]::GetFullPath($Directory, (Get-Location -PSProvider FileSystem).ProviderPath)
$Source = [System.IO.Path]::GetFullPath($Source, (Get-Location -PSProvider FileSystem).ProviderPath)
$mode = if ($Execute) { 'EXECUTE' } else { 'DRY RUN' }
Write-Step "$mode of the PostgreSQL major upgrade of $Directory from $Source"

#region 1. Checks

$envPath = Join-Path $Directory '.env'

if (-not (Test-Path -LiteralPath $envPath -PathType Leaf)) {
  throw "$envPath is missing."
}

$settings = Read-DotEnv -Path $envPath

if (-not ($settings.ContainsKey('DATA_DIR') -and $settings['DATA_DIR'])) {
  throw "DATA_DIR is not set in ${envPath}: the migration handles the bind-mounted volumes of docker-compose.host.yml only (docs/operations.md, PostgreSQL major upgrade)."
}

$dataDir = $settings['DATA_DIR']
$dbData = Join-Path $dataDir 'db'

foreach ($file in $copiedFiles) {
  if (-not (Test-Path -LiteralPath (Join-Path $Source $file) -PathType Leaf)) {
    throw "$(Join-Path $Source $file) is missing from the checkout."
  }
}

$deployedCompose = Join-Path $Directory 'docker-compose.prod.yml'

if (-not (Test-Path -LiteralPath $deployedCompose -PathType Leaf)) {
  throw "$deployedCompose is missing: nothing is deployed in $Directory."
}

$sourceImage = Get-ComposeServiceImage -Path $deployedCompose -Service db
$sourceMajor = Get-PostgresMajor -Image $sourceImage -Path $deployedCompose
$targetCompose = Join-Path $Source 'docker-compose.prod.yml'
$targetImage = Get-ComposeServiceImage -Path $targetCompose -Service db
$targetMajor = Get-PostgresMajor -Image $targetImage -Path $targetCompose

if ($targetMajor -eq $sourceMajor) {
  Write-Step "the deployed db image is already PostgreSQL ${targetMajor}: nothing to migrate"
  exit 0
}

if ($targetMajor -lt $sourceMajor) {
  throw "The db image of $Source is PostgreSQL $targetMajor, older than the deployed PostgreSQL ${sourceMajor}: a return goes through the restore of a dump of that version (docs/operations.md, Restore)."
}

Write-Step "PostgreSQL $sourceMajor ($sourceImage) to PostgreSQL $targetMajor ($targetImage)"

$found = Get-PostgreSqlDataVersion -Image $sourceImage

if ($null -eq $found) {
  Write-Step "DRY RUN, would read the PostgreSQL version of $dbData through a container of the db image (unreadable by this user)"
}
else {
  $expected = @($found | Where-Object { $_.Major -eq "$sourceMajor" -and ($_.Path -eq 'PG_VERSION' -or $_.Path -eq "$sourceMajor/docker/PG_VERSION") })

  if ($found.Count -ne 1 -or $expected.Count -ne 1) {
    $description = if ($found.Count -eq 0) { 'no database' } else { ($found | ForEach-Object { "$($_.Path) $($_.Major)" }) -join ', ' }
    throw "$dbData holds $description, not one PostgreSQL $sourceMajor database: the migration stops before any change."
  }

  Write-Step "$dbData holds a PostgreSQL $sourceMajor database ($($expected[0].Path))"
}

if ($Execute) {
  $running = Invoke-Compose -Arguments @('ps', '--status', 'running', '-q', 'db') | Select-Object -First 1

  if (-not $running) {
    throw "The db service of $Directory is not running: start it on PostgreSQL $sourceMajor, the backup needs it. Nothing was changed."
  }
}
else {
  Write-Step 'DRY RUN, would check that the db service runs'
}

#endregion

$stamp = [DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssZ')
$oldData = "db-pg$sourceMajor-$stamp"
$savedFiles = Join-Path $Directory ".pg-upgrade-$stamp"
$dump = ''
$before = ''
$stopped = $false
$moved = $false
$copied = $false

#region 2. Backup and 3. Row count

Invoke-Change "back up the database with the backup service of PostgreSQL $sourceMajor (BACKUP_ONCE)" {
  $output = Invoke-Compose -Arguments @('run', '--rm', '-e', 'BACKUP_ONCE=true', 'backup')
  $match = $output | Select-String -Pattern '^backup: (\S+) written' | Select-Object -First 1

  if (-not $match) {
    $output | ForEach-Object { Write-Host "  $_" }
    throw 'The backup printed no dump path: the migration stops before any change.'
  }

  $script:dump = $match.Matches[0].Groups[1].Value
  Write-Step "dump $dump"
}

Invoke-Change 'count the rows of every table of the source database' {
  $script:before = Get-RowCount

  if (-not $before) {
    throw 'The source database has no table: the migration stops before any change.'
  }

  Write-Step "$(@($before -split "`n").Count) tables counted"
}

#endregion

try {
  #region 4. Stop and 5. Move aside

  Invoke-Change 'stop the Web, the Api and the backup service, then the database' {
    $script:stopped = $true
    Invoke-Compose -Arguments @('stop', 'web', 'api', 'backup') | Out-Null
    Invoke-Compose -Arguments @('stop', 'db') | Out-Null
  }

  Invoke-Change "keep the deployed files in $savedFiles and rename $dbData to $oldData" {
    foreach ($file in $copiedFiles) {
      $current = Join-Path $Directory $file

      if (Test-Path -LiteralPath $current -PathType Leaf) {
        $saved = Join-Path $savedFiles $file
        New-Item -ItemType Directory -Path (Split-Path -Parent $saved) -Force | Out-Null
        Copy-Item -LiteralPath $current -Destination $saved -Force
      }
    }

    Invoke-AsRoot -Image $targetImage -Command "mv /data/db /data/$oldData && mkdir /data/db && chown 70:70 /data/db && chmod 700 /data/db" | Out-Null
    $script:moved = $true
  }

  #endregion

  #region 6. Copy and 7. Restore

  Invoke-Change "copy $($copiedFiles.Count) files of $Source to $Directory" {
    $script:copied = $true

    foreach ($file in $copiedFiles) {
      $to = Join-Path $Directory $file
      New-Item -ItemType Directory -Path (Split-Path -Parent $to) -Force | Out-Null
      Copy-Item -LiteralPath (Join-Path $Source $file) -Destination $to -Force
    }
  }

  Invoke-Change "start the PostgreSQL $targetMajor database and restore the dump" {
    Invoke-Compose -Arguments @('up', '-d', 'db') | Out-Null
    Wait-Healthy -Service db
    Wait-DatabaseOverTcp
    Invoke-Compose -Arguments @('run', '--rm', '--no-deps', '--entrypoint', 'pg_restore', 'backup', '--no-owner', '--exit-on-error', '--dbname', 'codecoverage', $dump) | Out-Null
  }

  #endregion

  #region 8. Row count

  Invoke-Change 'compare the row counts of the new database with the source' {
    $after = Get-RowCount

    if ($after -cne $before) {
      $difference = Compare-Object @($before -split "`n") @($after -split "`n") | ForEach-Object { "$($_.SideIndicator) $($_.InputObject)" }
      throw "The restored database differs from the source: $($difference -join '; ')."
    }

    Write-Step 'the row counts are identical'
  }

  #endregion

  #region 9. Start

  Invoke-Change 'start the Api and wait for its health' {
    Invoke-Compose -Arguments @('up', '-d', 'api') | Out-Null
    Wait-Healthy -Service api
  }

  Invoke-Change 'start the Web and wait for its health' {
    Invoke-Compose -Arguments @('up', '-d', 'web') | Out-Null
    Wait-Healthy -Service web
  }

  Invoke-Change 'start the backup service' {
    Invoke-Compose -Arguments @('up', '-d', '--no-deps', 'backup') | Out-Null
  }

  #endregion
}
catch {
  $failure = $_.Exception.Message
  Write-Host "::error::$failure"

  if ($stopped) {
    Write-Step "going back to PostgreSQL $sourceMajor"

    try {
      if ($copied) {
        Invoke-Compose -Arguments @('stop', 'web', 'api', 'backup', 'db') -AllowFailure | Out-Null
      }

      if ($moved) {
        Invoke-AsRoot -Image $targetImage -Command "mv /data/db /data/db-pg$targetMajor-failed-$stamp && mv /data/$oldData /data/db" | Out-Null
        Write-Step "$dbData has its PostgreSQL $sourceMajor data back; the failed attempt is in db-pg$targetMajor-failed-$stamp"
      }

      if ($copied) {
        foreach ($file in $copiedFiles) {
          $saved = Join-Path $savedFiles $file
          $current = Join-Path $Directory $file

          if (Test-Path -LiteralPath $saved -PathType Leaf) {
            Copy-Item -LiteralPath $saved -Destination $current -Force
          }
          else {
            Remove-Item -LiteralPath $current -Force -ErrorAction SilentlyContinue
          }
        }

        Write-Step "the files of $Directory put back as they were"
      }

      Invoke-Compose -Arguments @('up', '-d', 'db', 'api', 'web', 'backup') | Out-Null
      Wait-Healthy -Service api
      Wait-Healthy -Service web
      Write-Step "back on PostgreSQL $sourceMajor; dump $dump kept"
    }
    catch {
      Write-Host @"
::error::Going back failed too: $($_.Exception.Message)
migrate: by hand, from the production directory (cd $Directory): docker compose --project-directory . stop db;
migrate: put the PostgreSQL $sourceMajor data back in place of $dbData (it is $(Join-Path $dataDir $oldData) if it was moved);
migrate: put back the files saved in $savedFiles; then docker compose --project-directory . up -d db api web backup.
migrate: the dump taken before the change is $dump (in the backups directory of DATA_DIR).
"@
    }
  }

  throw "The PostgreSQL major upgrade failed: $failure"
}

if ($Execute) {
  Write-Host @"
migrate: done: PostgreSQL $targetMajor runs with the data of PostgreSQL $sourceMajor, row counts identical.
migrate: kept until you remove them, once the new version runs: $(Join-Path $dataDir $oldData) and $savedFiles.
migrate: next: deploy the release of $Source ("Re-run job" of its deploy job, or scripts/Deploy-Production.ps1).
"@
}

Write-Step "$mode done"
exit 0
