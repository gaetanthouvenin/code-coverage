#Requires -Version 7.4
<#
.SYNOPSIS
  Deploys a release of CodeCoverage to the production directory of the Docker host (M4.02b).

.DESCRIPTION
  Run by the deploy job of ci-cd.yml on the self-hosted runner of the production host, from the
  checkout of the v* tag, with -Execute. Without -Execute it is a dry run: the checks run, and each
  command that would change something is printed instead of run. It can be run by hand on the host.

  Every compose command is `docker compose --project-directory <Directory>`, without -f: the .env of
  the directory names the files in COMPOSE_FILE (docker-compose.prod.yml:docker-compose.host.yml).

   1. Checks: .env writable, secrets/dp.pfx present, the data directories of DATA_DIR present, the
      current TAG read from .env and compared with -Version by SemVer 2.0 precedence. The same
      version is a redeployment; an older one is refused without -AllowDowngrade. With -Execute, the
      Docker network of PROXY_NETWORK must exist.
   2. Copy, in place, of an explicit list of deploy/vm files. .env and secrets/ are never touched.
   3. Pull of the api and web images of -Version (the TAG of the process wins over .env).
   4. Revision: the org.opencontainers.image.revision label of both images must be -Revision (an
      image tag is mutable).
   5. On-demand backup (BACKUP_ONCE=true): the path of the dump is required.
   6. The new TAG is written to .env, the previous one to .env.previous-tag (left as it is by a
      redeployment, so that it still names the version to return to). Nothing is written before a
      successful pull and backup. TAG and IMAGE_PREFIX of .env win over the environment of the job.
   7. Start of the Api (it migrates the schema), wait for its health, then the same for the Web.
      Caddy is reloaded when its Caddyfile changed and it runs; the backup service is recreated when
      backup/ changed.
   8. Verification through the proxy listening on the port 443 of the host (curl --resolve, no NAT
      loop): /health/ready of both hosts, and the served linux-x64.sha256 against the release.
   9. On a failure before the start of the Api, the previous TAG and the replaced deploy/vm files are
      put back (a dump taken meanwhile stays in the backups). After it, nothing is
      rolled back automatically (the schema may have migrated): the error names the dump and the
      procedure.
  10. Cleanup of the -api and -web images whose tag is neither the current version, nor the previous
      TAG, nor the version recorded in .env.previous-tag. Never a global prune: the host is not
      dedicated.
  11. Summary in $GITHUB_STEP_SUMMARY when it is set.

.PARAMETER Version
  The version to deploy, without the v: the SemVer of GitVersion (1.2.3, 1.3.0-beta.4).

.PARAMETER Revision
  The commit of the tag: the revision label both images must carry.

.PARAMETER Directory
  The production directory of the host (DEPLOY_DIR), e.g. /data03/docker/code-coverage.

.PARAMETER Source
  The deploy/vm directory to copy from; deploy/vm of this checkout by default.

.PARAMETER ExpectedUploaderSha256
  The SHA-256 of coverage-upload-linux-x64 published by the release of the tag. The check is skipped,
  with a warning, when it is empty.

.PARAMETER AllowDowngrade
  Accepts a version older than the current TAG (a manual return, with a restore of the database).

.PARAMETER HealthTimeoutSeconds
  How long the Api and the Web may stay starting or unhealthy before the deployment fails.

.PARAMETER HealthPollSeconds
  The interval between two health reads.

.PARAMETER Execute
  Runs the deployment. Without it, a dry run.

.EXAMPLE
  pwsh scripts/Deploy-Production.ps1 -Version 1.2.3 -Revision 0123abc... -Directory /data03/docker/code-coverage

.EXAMPLE
  pwsh scripts/Deploy-Production.ps1 -Version 1.2.2 -Revision 89abcd... -Directory /data03/docker/code-coverage -AllowDowngrade -Execute
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory)]
  [string] $Version,
  [Parameter(Mandatory)]
  [string] $Revision,
  [Parameter(Mandatory)]
  [string] $Directory,
  [string] $Source = (Join-Path (Split-Path -Parent $PSScriptRoot) 'deploy/vm'),
  [string] $ExpectedUploaderSha256 = '',
  [switch] $AllowDowngrade,
  [ValidateRange(1, 3600)]
  [int] $HealthTimeoutSeconds = 300,
  [ValidateRange(1, 60)]
  [int] $HealthPollSeconds = 5,
  [switch] $Execute
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Every failure ends as one GitHub Actions error line, unwrapped, and the exit code 1.
trap {
  Write-Host "::error::$($_.Exception.Message)"
  exit 1
}

# The files copied from deploy/vm, and those whose change restarts a service. .env and secrets/ never.
$copiedFiles = @(
  'docker-compose.prod.yml',
  'docker-compose.host.yml',
  'Caddyfile',
  '.env.example',
  'backup/backup.sh',
  'backup/healthcheck.sh',
  'backup/Dockerfile'
)
$dataDirectories = 'db', 'backups', 'api', 'web'
$semVerPattern = '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$'

#region Helpers

function Write-Step {
  param([string] $Message)

  Write-Host "deploy: $Message"
}

function Compare-SemVer {
  # SemVer 2.0 precedence: -1, 0 or 1. Build metadata is ignored; a pre-release is lower than its
  # release; pre-release identifiers compare numerically when both are numeric, else ordinally, and a
  # numeric one is lower than an alphanumeric one; a shorter list of equal identifiers is lower.
  param([string] $Left, [string] $Right)

  $l = [regex]::Match($Left, $semVerPattern)
  $r = [regex]::Match($Right, $semVerPattern)

  foreach ($group in 1..3) {
    $difference = [bigint]::Parse($l.Groups[$group].Value).CompareTo([bigint]::Parse($r.Groups[$group].Value))

    if ($difference -ne 0) {
      return [math]::Sign($difference)
    }
  }

  $leftPre = $l.Groups[4].Success
  $rightPre = $r.Groups[4].Success

  if (-not $leftPre -and -not $rightPre) { return 0 }
  if (-not $leftPre) { return 1 }
  if (-not $rightPre) { return -1 }

  $leftIds = $l.Groups[4].Value.Split('.')
  $rightIds = $r.Groups[4].Value.Split('.')

  for ($i = 0; $i -lt [math]::Min($leftIds.Count, $rightIds.Count); $i++) {
    $a = $leftIds[$i]
    $b = $rightIds[$i]
    $aNumeric = $a -match '^\d+$'
    $bNumeric = $b -match '^\d+$'

    $difference = if ($aNumeric -and $bNumeric) { [bigint]::Parse($a).CompareTo([bigint]::Parse($b)) }
                  elseif ($aNumeric) { -1 }
                  elseif ($bNumeric) { 1 }
                  else { [string]::CompareOrdinal($a, $b) }

    if ($difference -ne 0) {
      return [math]::Sign($difference)
    }
  }

  return [math]::Sign($leftIds.Count.CompareTo($rightIds.Count))
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

function Set-DotEnvTag {
  # Replaces the TAG line, every other line kept as it is.
  param([string] $Path, [string] $Tag)

  $text = [System.IO.File]::ReadAllText($Path)
  $newLine = if ($text.Contains("`r`n")) { "`r`n" } else { "`n" }
  $pattern = [regex]::new('(?m)^TAG=.*?(?=\r?$)')

  $text = if ($pattern.IsMatch($text)) { $pattern.Replace($text, "TAG=$Tag", 1) }
          else { $text.TrimEnd("`r", "`n") + $newLine + "TAG=$Tag" + $newLine }

  [System.IO.File]::WriteAllText($Path, $text)
}

function Get-FileHashOrEmpty {
  param([string] $Path)

  if (Test-Path -LiteralPath $Path -PathType Leaf) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
  }

  return ''
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
  param([string[]] $Arguments, [switch] $AllowFailure)

  return Invoke-Docker -Arguments (@('compose', '--project-directory', $Directory) + $Arguments) -AllowFailure:$AllowFailure
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

function Wait-Healthy {
  # Polls the health of the container of a service. starting and unhealthy are tolerated until the
  # timeout (a long migration); exited, dead and restarting fail at once.
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

function Invoke-Curl {
  param([string] $HostName, [string] $Path)

  $output = & curl --fail --silent --show-error --max-time 10 --retry 6 --retry-all-errors --resolve "${HostName}:443:127.0.0.1" "https://$HostName$Path" 2>&1 |
            ForEach-Object { "$_" }

  if ($LASTEXITCODE -ne 0) {
    throw "https://$HostName$Path did not answer through the proxy of the host (curl exit code $LASTEXITCODE): $($output -join ' ')"
  }

  return ($output -join "`n")
}

#endregion

$mode = if ($Execute) { 'EXECUTE' } else { 'DRY RUN' }
Write-Step "$mode of $Version ($Revision) to $Directory"

#region 1. Checks

if ($Version -notmatch $semVerPattern) {
  throw "The version '$Version' is not a SemVer 2.0 version (without the v)."
}

if ($Revision -notmatch '^[0-9a-f]{40}$') {
  throw "The revision '$Revision' is not a full lower-case commit SHA."
}

$envPath = Join-Path $Directory '.env'

if (-not (Test-Path -LiteralPath $envPath -PathType Leaf)) {
  throw "$envPath is missing: the first deployment is manual (docs/operations.md, Continuous deployment)."
}

try {
  [System.IO.File]::Open($envPath, 'Open', 'ReadWrite', 'ReadWrite').Dispose()
}
catch {
  throw "$envPath is not writable by this user: $($_.Exception.Message)"
}

$settings = Read-DotEnv -Path $envPath
$certificate = if ($settings.ContainsKey('DP_CERT_FILE') -and $settings['DP_CERT_FILE']) { $settings['DP_CERT_FILE'] } else { './secrets/dp.pfx' }
$certificatePath = if ([System.IO.Path]::IsPathRooted($certificate)) { $certificate } else { Join-Path $Directory $certificate }

if (-not (Test-Path -LiteralPath $certificatePath -PathType Leaf)) {
  throw "The Data Protection certificate $certificatePath is missing."
}

if ($settings.ContainsKey('DATA_DIR') -and $settings['DATA_DIR']) {
  foreach ($name in $dataDirectories) {
    $path = Join-Path $settings['DATA_DIR'] $name

    if (-not (Test-Path -LiteralPath $path -PathType Container)) {
      throw "The data directory $path is missing: create it with its owner before the first up (docs/operations.md)."
    }
  }
}

foreach ($key in 'IMAGE_PREFIX', 'CC_API_HOST', 'CC_WEB_HOST') {
  if (-not ($settings.ContainsKey($key) -and $settings[$key])) {
    throw "$key is not set in $envPath."
  }
}

$previousTag = if ($settings.ContainsKey('TAG')) { $settings['TAG'] } else { '' }

if ($previousTag -notmatch $semVerPattern) {
  throw "The current TAG of $envPath ('$previousTag') is missing or is not a SemVer version: set it by hand to the running version."
}

$comparison = Compare-SemVer -Left $Version -Right $previousTag

if ($comparison -lt 0 -and -not $AllowDowngrade) {
  throw "$Version is older than the deployed ${previousTag}: refused without -AllowDowngrade (a return needs a restore, docs/operations.md)."
}

$kind = switch ($comparison) { 0 { 'redeployment' } 1 { 'upgrade' } default { 'downgrade' } }
Write-Step "$kind from $previousTag to $Version"

# The version to return to. A redeployment keeps the one recorded by the deployment that brought the
# current version: the TAG of .env is already the version deployed.
$previousTagPath = Join-Path $Directory '.env.previous-tag'
$recordedTag = ''

if (Test-Path -LiteralPath $previousTagPath -PathType Leaf) {
  $recordedTag = [System.IO.File]::ReadAllText($previousTagPath).Trim()

  if ($recordedTag -notmatch $semVerPattern) {
    $recordedTag = ''
  }
}

$returnTag = if ($comparison -eq 0 -and $recordedTag) { $recordedTag } else { $previousTag }

$imagePrefix = $settings['IMAGE_PREFIX']
$apiHost = $settings['CC_API_HOST']
$webHost = $settings['CC_WEB_HOST']

# A missing proxy network would make the Api fail to start after the TAG is written: checked before
# anything changes. Read only, but run only with -Execute: a dry run calls no docker.
$proxyNetwork = if ($settings.ContainsKey('PROXY_NETWORK')) { $settings['PROXY_NETWORK'] } else { '' }

if ($proxyNetwork) {
  if ($Execute) {
    Invoke-Docker -Arguments @('network', 'inspect', '--format', '{{.Name}}', $proxyNetwork) -AllowFailure | Out-Null

    if ($LASTEXITCODE -ne 0) {
      throw "The Docker network '$proxyNetwork' of PROXY_NETWORK does not exist: create it or fix .env. Nothing was changed."
    }
  }
  else {
    Write-Step "DRY RUN, would check that the Docker network $proxyNetwork exists"
  }
}

#endregion

# The TAG and the IMAGE_PREFIX of the process win over .env for every compose command from here on:
# the images compose pulls are those the revision check and the cleanup read.
$env:TAG = $Version
$env:IMAGE_PREFIX = $imagePrefix
$dump = ''
$tagWritten = $false
$apiStarting = $false
$filesReplaced = $false
$savedFiles = Join-Path ([System.IO.Path]::GetTempPath()) "cc-deploy-$([guid]::NewGuid().ToString('N'))"

try {
  #region 2. Copy

  $watched = 'Caddyfile', 'backup/backup.sh', 'backup/healthcheck.sh', 'backup/Dockerfile'
  $before = @{}
  foreach ($file in $watched) { $before[$file] = Get-FileHashOrEmpty -Path (Join-Path $Directory $file) }

  foreach ($file in $copiedFiles) {
    $from = Join-Path $Source $file

    if (-not (Test-Path -LiteralPath $from -PathType Leaf)) {
      throw "$from is missing from the checkout."
    }
  }

  Invoke-Change "copy $($copiedFiles.Count) files of $Source to $Directory" {
    # The files replaced are kept aside, to be put back on a failure before the start of the Api.
    foreach ($file in $copiedFiles) {
      $current = Join-Path $Directory $file

      if (Test-Path -LiteralPath $current -PathType Leaf) {
        $saved = Join-Path $savedFiles $file
        New-Item -ItemType Directory -Path (Split-Path -Parent $saved) -Force | Out-Null
        Copy-Item -LiteralPath $current -Destination $saved -Force
      }
    }

    $script:filesReplaced = $true

    foreach ($file in $copiedFiles) {
      $to = Join-Path $Directory $file
      New-Item -ItemType Directory -Path (Split-Path -Parent $to) -Force | Out-Null
      Copy-Item -LiteralPath (Join-Path $Source $file) -Destination $to -Force
    }
  }

  $caddyChanged = $before['Caddyfile'] -ne (Get-FileHashOrEmpty -Path (Join-Path $Source 'Caddyfile'))
  $backupChanged = @($watched | Where-Object { $_ -like 'backup/*' } |
                     Where-Object { $before[$_] -ne (Get-FileHashOrEmpty -Path (Join-Path $Source $_)) }).Count -gt 0

  #endregion

  #region 3. Pull and 4. Revision

  Invoke-Change "pull the api and web images of $Version" {
    Invoke-Compose -Arguments @('pull', 'api', 'web') | Out-Null

    foreach ($image in "$imagePrefix-api:$Version", "$imagePrefix-web:$Version") {
      $label = (Invoke-Docker -Arguments @('image', 'inspect', '--format', '{{ index .Config.Labels "org.opencontainers.image.revision" }}', $image) |
                  Select-Object -First 1).Trim()

      if ($label -cne $Revision) {
        throw "$image carries the revision '$label', not ${Revision}: an image tag is mutable, the deployment is refused."
      }
    }
  }

  #endregion

  #region 5. Backup

  Invoke-Change 'back up the database (BACKUP_ONCE)' {
    $output = Invoke-Compose -Arguments @('run', '--rm', '-e', 'BACKUP_ONCE=true', 'backup')
    $match = $output | Select-String -Pattern '^backup: (\S+) written' | Select-Object -First 1

    if (-not $match) {
      $output | ForEach-Object { Write-Host "  $_" }
      throw 'The backup printed no dump path: the deployment stops before any change.'
    }

    $script:dump = $match.Matches[0].Groups[1].Value
    Write-Step "dump $dump"
  }

  #endregion

  #region 6. TAG

  Invoke-Change "write TAG=$Version to .env (version to return to: $returnTag)" {
    if ($comparison -ne 0) {
      [System.IO.File]::WriteAllText($previousTagPath, "$previousTag`n")
    }

    Set-DotEnvTag -Path $envPath -Tag $Version
    $script:tagWritten = $true
  }

  #endregion

  #region 7. Start

  Invoke-Change 'start the Api and wait for its health' {
    $script:apiStarting = $true
    Invoke-Compose -Arguments @('up', '-d', '--pull', 'never', 'api') | Out-Null
    Wait-Healthy -Service api
  }

  Invoke-Change 'start the Web and wait for its health' {
    Invoke-Compose -Arguments @('up', '-d', '--pull', 'never', 'web') | Out-Null
    Wait-Healthy -Service web
  }

  if ($caddyChanged) {
    Invoke-Change 'reload Caddy (its Caddyfile changed)' {
      $caddy = Invoke-Compose -Arguments @('ps', '-q', 'caddy') | Select-Object -First 1

      if ($caddy) {
        Invoke-Compose -Arguments @('exec', '-T', 'caddy', 'caddy', 'reload', '--config', '/etc/caddy/Caddyfile') | Out-Null
      }
      else {
        Write-Step 'Caddy does not run here (the proxy of the host is used): nothing to reload'
      }
    }
  }

  if ($backupChanged) {
    Invoke-Change 'recreate the backup service (backup/ changed)' {
      Invoke-Compose -Arguments @('up', '-d', '--force-recreate', '--no-deps', 'backup') | Out-Null
    }
  }

  #endregion

  #region 8. Verification

  Invoke-Change "check /health/ready of $apiHost and $webHost through the proxy of the host" {
    Invoke-Curl -HostName $apiHost -Path '/health/ready' | Out-Null
    Invoke-Curl -HostName $webHost -Path '/health/ready' | Out-Null
  }

  if ($ExpectedUploaderSha256) {
    Invoke-Change 'check the served uploader against the release' {
      $served = (Invoke-Curl -HostName $apiHost -Path '/api/v1/uploader/linux-x64.sha256').Trim().Split()[0].ToLowerInvariant()
      $expected = $ExpectedUploaderSha256.Trim().Split()[0].ToLowerInvariant()

      if ($served -cne $expected) {
        throw "The served linux-x64 uploader has the sum $served, the release publishes $expected."
      }
    }
  }
  else {
    Write-Warning 'No -ExpectedUploaderSha256: the served uploader is not compared with the release.'
  }

  #endregion
}
catch {
  if ($tagWritten -and -not $apiStarting) {
    Set-DotEnvTag -Path $envPath -Tag $previousTag
    Write-Step "TAG put back to $previousTag"
  }

  if ($filesReplaced -and -not $apiStarting) {
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

  if ($apiStarting) {
    $volume = if ($settings.ContainsKey('DATA_DIR') -and $settings['DATA_DIR']) { Join-Path $settings['DATA_DIR'] 'db' } else { 'the db volume' }
    $dumpText = if ($dump) { $dump } else { 'none' }
    Write-Host @"
::error::The deployment of $Version failed after the start of the Api: nothing is rolled back, the schema may have migrated.
deploy: database: $volume; dump taken before the change: $dumpText (in the backups directory of DATA_DIR).
deploy: to return to ${returnTag}: stop the Api and the Web (docker compose --project-directory $Directory stop web api),
deploy: restore the dump with pg_restore (docs/operations.md, Restore), set TAG=$returnTag in .env,
deploy: then docker compose --project-directory $Directory up -d api, then up -d web.
"@
  }

  throw
}
finally {
  Remove-Item -LiteralPath $savedFiles -Recurse -Force -ErrorAction SilentlyContinue
}

#region 10. Cleanup

$keptTags = @($Version, $previousTag, $returnTag, $recordedTag) | Where-Object { $_ } | Select-Object -Unique

Invoke-Change "remove the -api and -web images other than $($keptTags -join ', ')" {
  foreach ($repository in "$imagePrefix-api", "$imagePrefix-web") {
    $tags = Invoke-Docker -Arguments @('image', 'ls', '--format', '{{.Tag}}', $repository) -AllowFailure

    foreach ($tag in $tags) {
      $tag = $tag.Trim()

      if ($tag -and $tag -ne '<none>' -and $tag -notin $keptTags) {
        Invoke-Docker -Arguments @('image', 'rm', "${repository}:$tag") -AllowFailure | Out-Null

        if ($LASTEXITCODE -ne 0) {
          Write-Warning "${repository}:$tag could not be removed."
        }
        else {
          Write-Step "removed ${repository}:$tag"
        }
      }
    }
  }
}

#endregion

#region 11. Summary

if ($env:GITHUB_STEP_SUMMARY) {
  $lines = @(
    "## Production deployment",
    "",
    "| | |",
    "|---|---|",
    "| Deployed version | $Version |",
    "| Version to return to | $returnTag |",
    "| Revision | $Revision |",
    "| Dump before the change | $(if ($dump) { $dump } else { '-' }) |"
  )

  if (-not $Execute) {
    $lines += '', '**DRY RUN**: nothing was changed.'
  }

  Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value $lines
}

Write-Step "$mode of $Version done"
# A tolerated native failure leaves $LASTEXITCODE non-zero; the pwsh shell of GitHub Actions exits with it.
exit 0

#endregion
