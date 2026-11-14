#Requires -Version 7.4
<#
.SYNOPSIS
  Runs scripts/Deploy-Production.ps1 offline, against fake docker and curl commands.

.DESCRIPTION
  No Docker, no network: docker and curl are replaced by a pwsh script placed first in the PATH
  (`docker` and `curl` with a shebang on Linux and macOS, `docker.ps1` and `curl.ps1` on Windows). The
  fake records each call with the TAG of the .env and the IMAGE_PREFIX of its environment at that
  moment, and answers from the rules of the
  case: the first rule whose regular expression matches the command line gives its output and its
  exit code, a list of outputs being consumed one call at a time. Each case runs the deployment
  script in a child pwsh on a throwaway production directory. Exits with 1 when a check fails.

  Run by the build-and-test job of ci-cd.yml, next to tools/rollout/Test-Rollout.ps1.

.EXAMPLE
  pwsh scripts/Test-DeployProduction.ps1
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$deployScript = Join-Path $PSScriptRoot 'Deploy-Production.ps1'
$source = Join-Path (Split-Path -Parent $PSScriptRoot) 'deploy/vm'
$revision = '0123456789abcdef0123456789abcdef01234567'
$sum = 'aa' * 32
$failures = [System.Collections.Generic.List[string]]::new()
$workRoot = Join-Path ([System.IO.Path]::GetTempPath()) "deploy-test-$([guid]::NewGuid().ToString('N'))"

$fake = @'
#!/usr/bin/env pwsh
# Fake docker or curl of Test-DeployProduction.ps1: records the call, answers from rules.json.
$ErrorActionPreference = 'Stop'
# The name of the tool is written into each copy: $PSCommandPath is empty in a script run by its shebang on Linux.
$tool = '__TOOL__'
$line = "$tool " + ($args -join ' ')
$tag = Get-Content -LiteralPath $env:FAKE_DOTENV | Where-Object { $_ -like 'TAG=*' } | Select-Object -First 1
Add-Content -LiteralPath (Join-Path $env:FAKE_DIR 'calls.log') -Value "$line | $tag | IMAGE_PREFIX=$env:IMAGE_PREFIX"
$rules = @(Get-Content -Raw -LiteralPath (Join-Path $env:FAKE_DIR 'rules.json') | ConvertFrom-Json)

for ($i = 0; $i -lt $rules.Count; $i++) {
  $rule = $rules[$i]

  if ($rule.Tool -eq $tool -and $line -match $rule.Match) {
    $outputs = @($rule.Outputs)
    $counter = Join-Path $env:FAKE_DIR "rule-$i.count"
    $n = if (Test-Path -LiteralPath $counter) { [int](Get-Content -LiteralPath $counter) } else { 0 }
    Set-Content -LiteralPath $counter -Value ($n + 1)
    $output = if ($outputs.Count) { $outputs[[math]::Min($n, $outputs.Count - 1)] } else { '' }

    if ($output) {
      $output -split "`n" | Write-Output
    }

    exit [int]$rule.Exit
  }
}

exit 0
'@

#region Helpers

function Assert-True {
  param([string] $Name, [bool] $Condition, [string] $Detail = '')

  if ($Condition) {
    Write-Host "  ok: $Name"
  }
  else {
    Write-Host "  FAILED: $Name $Detail"
    $failures.Add("$Name $Detail")
  }
}

function New-Rule {
  param([string] $Tool, [string] $Match, [string[]] $Outputs = @(), [int] $Exit = 0)

  return [ordered]@{ Tool = $Tool; Match = $Match; Outputs = @($Outputs); Exit = $Exit }
}

function Get-DefaultRules {
  return @(
    (New-Rule docker 'compose .* ps -a -q api$' 'id-api'),
    (New-Rule docker 'compose .* ps -a -q web$' 'id-web'),
    (New-Rule docker 'compose .* ps -q caddy$'),
    (New-Rule docker '^docker inspect .* id-' 'running healthy'),
    (New-Rule docker '^docker image inspect ' $revision),
    (New-Rule docker 'compose .* run --rm -e BACKUP_ONCE=true backup$' "backup: /backups/codecoverage-20261114T000000Z.dump written (10 bytes)"),
    (New-Rule docker '^docker image ls .*-api$' "1.2.3`n1.2.2`n1.2.1`n<none>"),
    (New-Rule docker '^docker image ls .*-web$' "1.2.3`n1.2.2`n1.2.1"),
    (New-Rule curl 'linux-x64\.sha256$' $sum),
    (New-Rule curl '.' 'Healthy')
  )
}

function Invoke-Case {
  # Prepares a production directory and runs the deployment in a child pwsh with the fakes first in
  # the PATH. Returns the exit code, the output, the calls and the directory.
  param(
    [string] $Name,
    [string] $CurrentTag = '1.2.2',
    [string] $Version = '1.2.3',
    [hashtable[]] $Rules = @(),
    [string[]] $Arguments = @('-Execute'),
    [switch] $ChangedCaddyfile,
    [switch] $ChangedBackup,
    [switch] $NoDataDirectories,
    [string] $RecordedPreviousTag = '',
    [string] $ImageTags = '',
    # The health timeout of the deployment: short for the timeout case, long where a slow sequence of
    # states must end healthy (each fake docker call starts a pwsh, slower on a CI runner).
    [int] $HealthTimeoutSeconds = 2,
    # A second deployment on the directory the previous run of the same case left.
    [switch] $KeepDirectory
  )

  $case = Join-Path $workRoot ($Name -replace '[^A-Za-z0-9]+', '-')
  $bin = Join-Path $case 'bin'
  $directory = Join-Path $case 'production'
  $keep = $KeepDirectory -and (Test-Path -LiteralPath $directory)
  New-Item -ItemType Directory -Path $bin, (Join-Path $directory 'secrets'), (Join-Path $directory 'backup') -Force | Out-Null

  foreach ($tool in 'docker', 'curl') {
    if ($IsWindows) {
      [System.IO.File]::WriteAllText((Join-Path $bin "$tool.ps1"), $fake.Replace('__TOOL__', $tool))
    }
    else {
      $path = Join-Path $bin $tool
      [System.IO.File]::WriteAllText($path, $fake.Replace('__TOOL__', $tool).Replace("`r`n", "`n"))
      & chmod +x $path
    }
  }

  $data = Join-Path $directory 'data'

  if (-not $keep) {
    if (-not $NoDataDirectories) {
      foreach ($name in 'db', 'backups', 'api', 'web') { New-Item -ItemType Directory -Path (Join-Path $data $name) -Force | Out-Null }
    }

    # The files of a previous deployment: identical to deploy/vm unless the case changes them.
    foreach ($file in 'Caddyfile', 'backup/backup.sh', 'backup/healthcheck.sh', 'backup/Dockerfile') {
      Copy-Item -LiteralPath (Join-Path $source $file) -Destination (Join-Path $directory $file)
    }

    if ($ChangedCaddyfile) { Add-Content -LiteralPath (Join-Path $directory 'Caddyfile') -Value '# older' }
    if ($ChangedBackup) { Add-Content -LiteralPath (Join-Path $directory 'backup/backup.sh') -Value '# older' }

    $tagLine = if ($null -eq $CurrentTag) { '' } else { "TAG=$CurrentTag" }
    $dotEnv = @(
      'COMPOSE_FILE=docker-compose.prod.yml:docker-compose.host.yml',
      'IMAGE_PREFIX=ghcr.io/owner/code-coverage',
      $tagLine,
      'CC_API_HOST=coverage-api.example.test',
      'CC_WEB_HOST=coverage.example.test',
      'DB_PASSWORD=synthetic',
      'PROXY_NETWORK=npm_default',
      "DATA_DIR=$data"
    ) -join "`n"
    [System.IO.File]::WriteAllText((Join-Path $directory '.env'), "$dotEnv`n")
    [System.IO.File]::WriteAllText((Join-Path $directory 'secrets/dp.pfx'), 'synthetic certificate')

    if ($RecordedPreviousTag) {
      [System.IO.File]::WriteAllText((Join-Path $directory '.env.previous-tag'), "$RecordedPreviousTag`n")
    }
  }

  $imageRules = if ($ImageTags) {
    @((New-Rule docker '^docker image ls .*-api$' $ImageTags), (New-Rule docker '^docker image ls .*-web$' $ImageTags))
  }
  else {
    @()
  }

  Get-ChildItem -LiteralPath $case -Filter 'rule-*.count' | Remove-Item
  $allRules = @($Rules) + $imageRules + (Get-DefaultRules)
  ConvertTo-Json -InputObject $allRules -Depth 5 | Set-Content -LiteralPath (Join-Path $case 'rules.json')
  New-Item -ItemType File -Path (Join-Path $case 'calls.log') -Force | Out-Null
  $summary = Join-Path $case 'summary.md'

  $saved = @{ PATH = $env:PATH; FAKE_DIR = $env:FAKE_DIR; FAKE_DOTENV = $env:FAKE_DOTENV; GITHUB_STEP_SUMMARY = $env:GITHUB_STEP_SUMMARY; IMAGE_PREFIX = $env:IMAGE_PREFIX }

  try {
    $env:PATH = $bin + [System.IO.Path]::PathSeparator + $env:PATH
    $env:FAKE_DIR = $case
    $env:FAKE_DOTENV = Join-Path $directory '.env'
    $env:GITHUB_STEP_SUMMARY = $summary
    $env:IMAGE_PREFIX = 'ghcr.io/job-level/code-coverage'

    $output = & pwsh -NoProfile -NonInteractive -File $deployScript -Version $Version -Revision $revision -Directory $directory `
                -ExpectedUploaderSha256 $sum -HealthTimeoutSeconds $HealthTimeoutSeconds -HealthPollSeconds 1 @Arguments 2>&1 | ForEach-Object { "$_" }
    $exitCode = $LASTEXITCODE
  }
  finally {
    foreach ($key in $saved.Keys) { Set-Item -Path "env:$key" -Value $saved[$key] }
  }

  return [pscustomobject]@{
    ExitCode  = $exitCode
    # One line: the error view of pwsh wraps a long message at the width of the console, behind "| ".
    Output    = ($output -join "`n") -replace '\x1b\[[0-9;]*m', '' -replace '\s*\n\s*(\|\s*)?', ' '
    Calls     = @(Get-Content -LiteralPath (Join-Path $case 'calls.log'))
    Directory = $directory
    Summary   = if (Test-Path -LiteralPath $summary) { Get-Content -Raw -LiteralPath $summary } else { '' }
  }
}

function Get-Tag {
  param([string] $Directory)

  return (Get-Content -LiteralPath (Join-Path $Directory '.env') | Where-Object { $_ -like 'TAG=*' } | Select-Object -First 1)
}

function Find-Call {
  # The index of the first call matching a pattern, -1 when none.
  param([object[]] $Calls, [string] $Pattern)

  for ($i = 0; $i -lt $Calls.Count; $i++) {
    if ($Calls[$i] -match $Pattern) { return $i }
  }

  return -1
}

#endregion

try {
  Write-Host 'Upgrade: order, TAG, files, cleanup, summary'
  $r = Invoke-Case -Name 'upgrade'
  $pull = Find-Call $r.Calls 'compose .* pull api web \| TAG=1\.2\.2 \|'
  $backup = Find-Call $r.Calls 'run --rm -e BACKUP_ONCE=true backup \| TAG=1\.2\.2 \|'
  $upApi = Find-Call $r.Calls 'up -d --pull never api \| TAG=1\.2\.3 \|'
  $upWeb = Find-Call $r.Calls 'up -d --pull never web \| TAG=1\.2\.3 \|'
  Assert-True 'the deployment succeeds' ($r.ExitCode -eq 0) $r.Output
  Assert-True 'pull, then backup, then the Api, then the Web' ($pull -ge 0 -and $pull -lt $backup -and $backup -lt $upApi -and $upApi -lt $upWeb) ($r.Calls -join "`n")
  Assert-True 'every compose command names the production directory without -f' (@($r.Calls | Where-Object { $_ -match '^docker compose ' -and ($_ -notmatch "--project-directory " -or $_ -match ' -f ') }).Count -eq 0)
  Assert-True 'both images are checked for their revision' (@($r.Calls | Where-Object { $_ -match 'image inspect .*org\.opencontainers\.image\.revision.*code-coverage-(api|web):1\.2\.3' }).Count -eq 2)
  Assert-True 'the TAG of .env is the new version' ((Get-Tag $r.Directory) -eq 'TAG=1.2.3')
  Assert-True 'the previous TAG is kept' ((Get-Content -Raw -LiteralPath (Join-Path $r.Directory '.env.previous-tag')).Trim() -eq '1.2.2')
  Assert-True 'the other lines of .env are kept' ((Get-Content -Raw -LiteralPath (Join-Path $r.Directory '.env')) -match 'DB_PASSWORD=synthetic')
  Assert-True 'secrets/ is untouched' ((Get-Content -Raw -LiteralPath (Join-Path $r.Directory 'secrets/dp.pfx')) -eq 'synthetic certificate')
  Assert-True 'the host override is copied' (Test-Path -LiteralPath (Join-Path $r.Directory 'docker-compose.host.yml'))
  Assert-True 'both hosts are checked through the proxy of the host' ((Find-Call $r.Calls '--resolve coverage-api\.example\.test:443:127\.0\.0\.1 https://coverage-api\.example\.test/health/ready') -ge 0 -and (Find-Call $r.Calls '--resolve coverage\.example\.test:443:127\.0\.0\.1 https://coverage\.example\.test/health/ready') -ge 0)
  Assert-True 'only the images neither current nor previous are removed' ((@($r.Calls | Where-Object { $_ -match '^docker image rm ' }) -replace ' \|.*$', '' -join ',') -eq 'docker image rm ghcr.io/owner/code-coverage-api:1.2.1,docker image rm ghcr.io/owner/code-coverage-web:1.2.1')
  Assert-True 'no global prune' ((Find-Call $r.Calls 'prune') -lt 0)
  Assert-True 'the proxy network is checked before anything changes' ((Find-Call $r.Calls '^docker network inspect .* npm_default') -ge 0 -and (Find-Call $r.Calls '^docker network inspect') -lt $pull)
  Assert-True 'compose sees the IMAGE_PREFIX of .env, not the one of the job' (@($r.Calls | Where-Object { $_ -match '^docker compose ' -and $_ -notmatch 'IMAGE_PREFIX=ghcr\.io/owner/code-coverage$' }).Count -eq 0)
  Assert-True 'no Caddy reload nor backup recreation when nothing changed' ((Find-Call $r.Calls 'caddy reload|--force-recreate') -lt 0)
  Assert-True 'the summary names both versions and the dump' ($r.Summary -match '1\.2\.3' -and $r.Summary -match '1\.2\.2' -and $r.Summary -match 'codecoverage-20261114T000000Z\.dump' -and $r.Summary -notmatch 'DRY RUN')

  Write-Host 'Dry run'
  $r = Invoke-Case -Name 'dry run' -Arguments @()
  Assert-True 'the dry run succeeds' ($r.ExitCode -eq 0) $r.Output
  Assert-True 'the dry run calls neither docker nor curl' ($r.Calls.Count -eq 0) ($r.Calls -join "`n")
  Assert-True 'the dry run leaves .env and the files' ((Get-Tag $r.Directory) -eq 'TAG=1.2.2' -and -not (Test-Path -LiteralPath (Join-Path $r.Directory 'docker-compose.prod.yml')))
  Assert-True 'the dry run says so in the summary' ($r.Summary -match 'DRY RUN')

  Write-Host 'SemVer 2.0 precedence'
  $precedence = @(
    @{ Current = '1.2.0-beta.9'; Version = '1.2.0-beta.10'; Accepted = $true },
    @{ Current = '1.2.0-beta.10'; Version = '1.2.0-beta.9'; Accepted = $false },
    @{ Current = '1.2.0-beta.10'; Version = '1.2.0-rc.1'; Accepted = $true },
    @{ Current = '1.2.0-rc.1'; Version = '1.2.0'; Accepted = $true },
    @{ Current = '1.2.0'; Version = '1.2.0-rc.1'; Accepted = $false },
    @{ Current = '1.9.0'; Version = '1.10.0'; Accepted = $true },
    @{ Current = '1.2.0-alpha'; Version = '1.2.0-alpha.1'; Accepted = $true },
    @{ Current = '1.2.0-alpha.beta'; Version = '1.2.0-alpha.1'; Accepted = $false },
    @{ Current = '1.2.3'; Version = '1.2.3'; Accepted = $true }
  )

  foreach ($p in $precedence) {
    $r = Invoke-Case -Name "semver $($p.Current) $($p.Version)" -CurrentTag $p.Current -Version $p.Version -Arguments @()
    $verdict = if ($p.Accepted) { 'accepted' } else { 'refused' }
    Assert-True "$($p.Version) over $($p.Current) is $verdict" (($r.ExitCode -eq 0) -eq $p.Accepted) $r.Output
  }

  $r = Invoke-Case -Name 'redeployment' -CurrentTag '1.2.3' -Arguments @()
  Assert-True 'the same version is a redeployment' ($r.Output -match 'redeployment from 1\.2\.3 to 1\.2\.3')
  $r = Invoke-Case -Name 'downgrade allowed' -CurrentTag '1.3.0' -Arguments @('-AllowDowngrade')
  Assert-True 'an older version is accepted with -AllowDowngrade' ($r.ExitCode -eq 0 -and $r.Output -match 'downgrade from 1\.3\.0') $r.Output
  $r = Invoke-Case -Name 'downgrade refused' -CurrentTag '1.3.0'
  Assert-True 'an older version is refused before any command' ($r.ExitCode -ne 0 -and $r.Calls.Count -eq 0 -and $r.Output -match 'AllowDowngrade')
  $r = Invoke-Case -Name 'missing tag' -CurrentTag $null
  Assert-True 'a missing TAG is refused' ($r.ExitCode -ne 0 -and $r.Output -match 'current TAG')
  $r = Invoke-Case -Name 'invalid tag' -CurrentTag 'latest'
  Assert-True 'an invalid TAG is refused' ($r.ExitCode -ne 0 -and $r.Output -match 'current TAG')
  $r = Invoke-Case -Name 'missing data' -NoDataDirectories
  Assert-True 'a missing data directory is refused' ($r.ExitCode -ne 0 -and $r.Output -match 'data directory' -and $r.Calls.Count -eq 0)

  Write-Host 'Redeployment keeps the version to return to'
  $r = Invoke-Case -Name 'redeploy keeps previous' -CurrentTag '1.2.0' -Version '1.3.0' -ImageTags "1.3.0`n1.2.0`n1.1.0"
  Assert-True 'the upgrade from 1.2.0 to 1.3.0 succeeds' ($r.ExitCode -eq 0) $r.Output
  $r = Invoke-Case -Name 'redeploy keeps previous' -Version '1.3.0' -ImageTags "1.3.0`n1.2.0`n1.1.0" -KeepDirectory
  Assert-True 'the redeployment of 1.3.0 succeeds' ($r.ExitCode -eq 0 -and $r.Output -match 'redeployment from 1\.3\.0 to 1\.3\.0') $r.Output
  Assert-True 'a redeployment leaves .env.previous-tag' ((Get-Content -Raw -LiteralPath (Join-Path $r.Directory '.env.previous-tag')).Trim() -eq '1.2.0')
  Assert-True 'a redeployment keeps the images of the version to return to' ((@($r.Calls | Where-Object { $_ -match '^docker image rm ' }) -replace ' \|.*$', '' -join ',') -eq 'docker image rm ghcr.io/owner/code-coverage-api:1.1.0,docker image rm ghcr.io/owner/code-coverage-web:1.1.0')
  Assert-True 'a redeployment names the version to return to' ($r.Summary -match 'Version to return to \| 1\.2\.0')
  $r = Invoke-Case -Name 'redeploy failure' -CurrentTag '1.3.0' -Version '1.3.0' -RecordedPreviousTag '1.2.0' -Rules @(New-Rule docker '^docker inspect .* id-api' 'exited ')
  Assert-True 'a failed redeployment points the return to the recorded version' ($r.ExitCode -ne 0 -and $r.Output -match 'to return to 1\.2\.0' -and $r.Output -match 'TAG=1\.2\.0')

  Write-Host 'Failures before the start of the Api'
  $r = Invoke-Case -Name 'network missing' -Rules @(New-Rule docker '^docker network inspect' 'Error: No such network: npm_default' 1)
  Assert-True 'a missing proxy network fails before anything changes' ($r.ExitCode -ne 0 -and $r.Output -match "network 'npm_default'" -and (Get-Tag $r.Directory) -eq 'TAG=1.2.2' -and (Find-Call $r.Calls ' pull ') -lt 0 -and -not (Test-Path -LiteralPath (Join-Path $r.Directory 'docker-compose.prod.yml')))

  $r = Invoke-Case -Name 'pull failure' -ChangedCaddyfile -Rules @(New-Rule docker 'compose .* pull api web' 'manifest unknown' 1)
  Assert-True 'a failed pull fails the deployment' ($r.ExitCode -ne 0)
  Assert-True 'a failed pull puts back the replaced files' ((Get-Content -Raw -LiteralPath (Join-Path $r.Directory 'Caddyfile')) -match '# older')
  Assert-True 'a failed pull removes the files that were not there' (-not (Test-Path -LiteralPath (Join-Path $r.Directory 'docker-compose.prod.yml')))
  Assert-True 'a failed pull leaves .env' ((Get-Tag $r.Directory) -eq 'TAG=1.2.2' -and -not (Test-Path -LiteralPath (Join-Path $r.Directory '.env.previous-tag')))
  Assert-True 'a failed pull starts nothing' ((Find-Call $r.Calls ' up -d ') -lt 0)

  $r = Invoke-Case -Name 'backup failure' -Rules @(New-Rule docker 'BACKUP_ONCE=true backup' 'backup: pg_dump failed' 1)
  Assert-True 'a failed backup fails the deployment and leaves .env' ($r.ExitCode -ne 0 -and (Get-Tag $r.Directory) -eq 'TAG=1.2.2' -and (Find-Call $r.Calls ' up -d ') -lt 0)
  $r = Invoke-Case -Name 'backup without dump' -Rules @(New-Rule docker 'BACKUP_ONCE=true backup' 'nothing useful')
  Assert-True 'a backup without a dump path fails the deployment' ($r.ExitCode -ne 0 -and (Get-Tag $r.Directory) -eq 'TAG=1.2.2' -and $r.Output -match 'no dump path')

  $r = Invoke-Case -Name 'revision' -Rules @(New-Rule docker '^docker image inspect .*-web:' ('f' * 40))
  Assert-True 'an image of another revision is refused' ($r.ExitCode -ne 0 -and $r.Output -match 'image tag is mutable' -and (Get-Tag $r.Directory) -eq 'TAG=1.2.2' -and (Find-Call $r.Calls 'BACKUP_ONCE') -lt 0)

  Write-Host 'Health'
  $r = Invoke-Case -Name 'slow start' -HealthTimeoutSeconds 60 -Rules @(New-Rule docker '^docker inspect .* id-api' @('running starting', 'running unhealthy', 'running healthy'))
  Assert-True 'starting and unhealthy are tolerated until healthy' ($r.ExitCode -eq 0) $r.Output

  $r = Invoke-Case -Name 'health timeout' -Rules @(New-Rule docker '^docker inspect .* id-api' 'running unhealthy')
  Assert-True 'a health timeout fails the deployment' ($r.ExitCode -ne 0 -and $r.Output -match 'after 2 seconds')
  Assert-True 'after the start of the Api, the copied files stay' (Test-Path -LiteralPath (Join-Path $r.Directory 'docker-compose.prod.yml'))
  Assert-True 'after the start of the Api, nothing is rolled back' ((Get-Tag $r.Directory) -eq 'TAG=1.2.3')
  Assert-True 'the error names the dump and the restore procedure' ($r.Output -match 'codecoverage-20261114T000000Z\.dump' -and $r.Output -match 'pg_restore' -and $r.Output -match 'TAG=1\.2\.2')
  Assert-True 'the Web is not started after an unhealthy Api' ((Find-Call $r.Calls 'up -d --pull never web') -lt 0)

  $r = Invoke-Case -Name 'exited' -Rules @(New-Rule docker '^docker inspect .* id-web' 'exited ')
  Assert-True 'an exited container fails at once' ($r.ExitCode -ne 0 -and $r.Output -match 'web is exited')

  Write-Host 'Changed files'
  $r = Invoke-Case -Name 'caddy changed' -ChangedCaddyfile -Rules @(New-Rule docker 'compose .* ps -q caddy$' 'id-caddy')
  Assert-True 'a changed Caddyfile reloads a running Caddy' ($r.ExitCode -eq 0 -and (Find-Call $r.Calls 'exec -T caddy caddy reload') -ge 0) $r.Output
  $r = Invoke-Case -Name 'caddy changed not running' -ChangedCaddyfile
  Assert-True 'a changed Caddyfile without Caddy reloads nothing' ($r.ExitCode -eq 0 -and (Find-Call $r.Calls 'caddy reload') -lt 0)
  $r = Invoke-Case -Name 'backup changed' -ChangedBackup
  Assert-True 'a changed backup/ recreates the backup service' ($r.ExitCode -eq 0 -and (Find-Call $r.Calls 'up -d --force-recreate --no-deps backup') -ge 0)

  Write-Host 'Uploader'
  $r = Invoke-Case -Name 'uploader' -Rules @(New-Rule curl 'linux-x64\.sha256$' ('bb' * 32))
  Assert-True 'a served uploader other than the release fails the deployment' ($r.ExitCode -ne 0 -and $r.Output -match 'the release publishes') $r.Output
}
finally {
  # KEEP_DEPLOY_TEST=1 keeps the directories of the cases, to read their calls.log and rules.json.
  if (-not $env:KEEP_DEPLOY_TEST) { Remove-Item -LiteralPath $workRoot -Recurse -Force -ErrorAction SilentlyContinue }
}

if ($failures.Count -gt 0) {
  Write-Host "$($failures.Count) check(s) failed:"
  $failures | ForEach-Object { Write-Host "  - $_" }
  exit 1
}

Write-Host 'All the deployment checks passed.'
# A tolerated native failure leaves $LASTEXITCODE non-zero; the pwsh shell of GitHub Actions exits with it.
exit 0
