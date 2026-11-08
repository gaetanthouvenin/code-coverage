#Requires -Version 7.2
<#
.SYNOPSIS
  Smoke test of the docker compose stack, on PostgreSQL and on SQL Server.

.DESCRIPTION
  For each database provider, starts docker-compose.yml (plus docker-compose.sqlserver.yml for SQL
  Server) under its own compose project, then checks that:
    - the Api and the Web both answer 200 on /health/ready (database reachable, migrations applied,
      RCSI on under SQL Server);
    - the Api serves Swagger UI (/swagger) and the OpenAPI document (/openapi/v1.json);
    - both containers run as the non-root user 1001.
  The stack is always removed afterwards (volumes included), unless -KeepRunning is given.

  Throwaway passwords and a throwaway Data Protection certificate are generated for the run and
  written to a temporary env file, passed with --env-file to every docker compose command: the .env
  file of the repository is then not loaded. The variables of the compose files are also removed
  from this process for the run (and restored afterwards), so that the shell of the developer cannot
  override them either.
  The images are built once from the working tree, unless -NoBuild is given (images already built,
  e.g. by the CI).

  Run by the compose-smoke job of the CI (M0.28). Requires Docker with Compose 2.24 or later.

.PARAMETER Provider
  PostgreSql, SqlServer or All (default).

.PARAMETER NoBuild
  Uses the images already tagged <ImagePrefix>-{api,web}:<Tag>.

.PARAMETER ImagePrefix
  The image name prefix, as IMAGE_PREFIX of docker-compose.yml. Default: code-coverage (local build);
  ghcr.io/<owner>/code-coverage for the images of the CI.

.PARAMETER Tag
  The image tag to build and run. Default: smoke.

.PARAMETER ApiPort
  The loopback port of the Api. Default: 18081 (not the 8081 of a local stack).

.PARAMETER WebPort
  The loopback port of the Web. Default: 18080 (not the 8080 of a local stack).

.PARAMETER TimeoutSeconds
  How long to wait for both hosts to be ready, per provider. Default: 300.

.PARAMETER KeepRunning
  Leaves the last stack running for inspection (docker compose -p cc-smoke-<provider> down -v).

.EXAMPLE
  pwsh scripts/smoke-compose.ps1

.EXAMPLE
  pwsh scripts/smoke-compose.ps1 -Provider SqlServer -NoBuild -Tag 1.2.3
#>
[CmdletBinding()]
param(
  [ValidateSet('PostgreSql', 'SqlServer', 'All')]
  [string] $Provider = 'All',
  [switch] $NoBuild,
  [string] $Tag = 'smoke',
  [string] $ImagePrefix = 'code-coverage',
  [int] $ApiPort = 18081,
  [int] $WebPort = 18080,
  [int] $TimeoutSeconds = 300,
  [switch] $KeepRunning
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path $PSScriptRoot -Parent
$composeFile = Join-Path $repositoryRoot 'docker-compose.yml'
$sqlServerFile = Join-Path $repositoryRoot 'docker-compose.sqlserver.yml'
$readyPath = '/health/ready'
$nonRootUid = '1001'

function Invoke-Docker {
  param([Parameter(Mandatory)] [string[]] $Arguments)

  & docker @Arguments

  if ($LASTEXITCODE -ne 0) {
    throw "docker $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
  }
}

function New-Password {
  # Letters and digits only (safe in connection strings and shell commands), with at least one
  # upper case letter, one lower case letter and one digit for the SQL Server complexity policy.
  $upper = 'ABCDEFGHJKLMNPQRSTUVWXYZ'
  $lower = 'abcdefghijkmnopqrstuvwxyz'
  $digits = '23456789'
  $all = $upper + $lower + $digits
  $characters = @(
    $upper[[System.Security.Cryptography.RandomNumberGenerator]::GetInt32($upper.Length)]
    $lower[[System.Security.Cryptography.RandomNumberGenerator]::GetInt32($lower.Length)]
    $digits[[System.Security.Cryptography.RandomNumberGenerator]::GetInt32($digits.Length)]
  )
  $characters += 1..21 | ForEach-Object {
    $all[[System.Security.Cryptography.RandomNumberGenerator]::GetInt32($all.Length)]
  }

  return -join $characters
}

function Wait-Ready {
  param(
    [Parameter(Mandatory)] [string] $Name,
    [Parameter(Mandatory)] [string] $Url,
    [Parameter(Mandatory)] [datetime] $Deadline
  )

  $last = 'no answer'

  while ([datetime]::UtcNow -lt $Deadline) {
    try {
      $response = Invoke-WebRequest -Uri $Url -SkipHttpErrorCheck -TimeoutSec 5
      if ($response.StatusCode -eq 200) {
        Write-Host "  $Name ready ($Url)."
        return
      }

      $last = "HTTP $($response.StatusCode)"
    }
    catch {
      $last = $_.Exception.Message
    }

    Start-Sleep -Seconds 3
  }

  throw "$Name was not ready in time on $Url (last answer: $last)."
}

function Assert-Ok {
  param(
    [Parameter(Mandatory)] [string] $Name,
    [Parameter(Mandatory)] [string] $Url,
    [Parameter(Mandatory)] [string] $ExpectedContent
  )

  $response = Invoke-WebRequest -Uri $Url -SkipHttpErrorCheck -TimeoutSec 15

  if ($response.StatusCode -ne 200) {
    throw "$Name answered HTTP $($response.StatusCode) on $Url."
  }

  if (-not $response.Content.Contains($ExpectedContent, [StringComparison]::OrdinalIgnoreCase)) {
    throw "$Name on $Url does not contain '$ExpectedContent'."
  }

  Write-Host "  $Name OK ($Url)."
}

function Assert-NonRoot {
  param(
    [Parameter(Mandatory)] [string[]] $Compose,
    [Parameter(Mandatory)] [string] $Service
  )

  $uid = (& docker @Compose exec -T $Service id -u | Out-String).Trim()

  if ($LASTEXITCODE -ne 0) {
    throw "Could not read the user of the $Service container."
  }

  if ($uid -ne $nonRootUid) {
    throw "The $Service container runs as uid '$uid' instead of $nonRootUid."
  }

  Write-Host "  $Service runs as uid $uid (non-root)."
}

function Test-Stack {
  param(
    [Parameter(Mandatory)] [string] $Name,
    [Parameter(Mandatory)] [string] $EnvFile
  )

  $project = "cc-smoke-$($Name.ToLowerInvariant())"
  $compose = @('compose', '--env-file', $EnvFile, '-p', $project, '-f', $composeFile)

  if ($Name -eq 'SqlServer') {
    $compose += @('-f', $sqlServerFile)
  }

  Write-Host "== $Name (compose project $project)"
  $succeeded = $false

  try {
    Invoke-Docker ($compose + @('up', '-d', '--no-build'))

    $deadline = [datetime]::UtcNow.AddSeconds($TimeoutSeconds)
    Wait-Ready -Name 'Api' -Url "http://127.0.0.1:$ApiPort$readyPath" -Deadline $deadline
    Wait-Ready -Name 'Web' -Url "http://127.0.0.1:$WebPort$readyPath" -Deadline $deadline

    Assert-Ok -Name 'Swagger UI' -Url "http://127.0.0.1:$ApiPort/swagger" -ExpectedContent 'swagger'
    Assert-Ok -Name 'OpenAPI document' -Url "http://127.0.0.1:$ApiPort/openapi/v1.json" `
              -ExpectedContent '"openapi"'

    Assert-NonRoot -Compose $compose -Service 'api'
    Assert-NonRoot -Compose $compose -Service 'web'

    $succeeded = $true
    Write-Host "== $Name passed."
  }
  finally {
    if (-not $succeeded) {
      Write-Host "== $Name failed: container logs follow."
      & docker @compose ps --all
      & docker @compose logs --no-color --tail 200
    }

    if ($KeepRunning) {
      Write-Host "Left running: docker compose -p $project down -v to remove it."
    }
    else {
      & docker @compose down -v --remove-orphans
    }
  }
}

& docker info --format '{{.ServerVersion}}' *> $null

if ($LASTEXITCODE -ne 0) {
  throw 'Docker is not available: start the Docker daemon to run the compose smoke test.'
}

$providers = if ($Provider -eq 'All') { @('PostgreSql', 'SqlServer') } else { @($Provider) }
$workDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "cc-smoke-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $workDirectory | Out-Null

# The variables read by the compose files, written to the env file of the run. Forward slashes keep
# the certificate path literal in the env file and valid for docker on Windows.
$envFile = Join-Path $workDirectory 'smoke.env'
$variables = [ordered]@{
  IMAGE_PREFIX                = $ImagePrefix
  TAG                         = $Tag
  DB_PASSWORD                 = New-Password
  DP_CERT_PASSWORD            = New-Password
  DP_CERT_FILE                = (Join-Path $workDirectory 'dp.pfx').Replace('\', '/')
  ENABLE_API_DOCS             = 'true'
  API_PORT                    = "$ApiPort"
  WEB_PORT                    = "$WebPort"
  FORWARDED_KNOWN_PROXIES     = ''
  OTEL_EXPORTER_OTLP_ENDPOINT = ''
}
$variables.Keys | ForEach-Object { "$_=$($variables[$_])" } | Set-Content -LiteralPath $envFile -Encoding utf8NoBOM

# A variable of the shell wins over the env file in docker compose, even an empty one: remove them for
# the run. Remove-Item, not SetEnvironmentVariable($key, $null): PowerShell turns that $null into an
# empty string, which leaves the variable defined.
$previous = @{}

foreach ($key in $variables.Keys) {
  $previous[$key] = [Environment]::GetEnvironmentVariable($key)
  Remove-Item -LiteralPath "Env:$key" -ErrorAction SilentlyContinue
}

try {
  & (Join-Path $PSScriptRoot 'new-dp-certificate.ps1') -Path $variables.DP_CERT_FILE `
                                                       -Password $variables.DP_CERT_PASSWORD

  if (-not $NoBuild) {
    Write-Host "== Building the images (tag $Tag)"
    Invoke-Docker @('compose', '--env-file', $envFile, '-f', $composeFile, 'build')
  }

  $failures = @()

  foreach ($name in $providers) {
    try {
      Test-Stack -Name $name -EnvFile $envFile
    }
    catch {
      Write-Host "ERROR: $($_.Exception.Message)"
      $failures += $name
    }
  }

  if ($failures.Count -gt 0) {
    Write-Host "Compose smoke test FAILED on: $($failures -join ', ')."
    exit 1
  }

  Write-Host "Compose smoke test passed on: $($providers -join ', ')."
}
finally {
  foreach ($key in $previous.Keys) {
    if ($null -ne $previous[$key]) {
      Set-Item -LiteralPath "Env:$key" -Value $previous[$key]
    }
  }

  Remove-Item -LiteralPath $workDirectory -Recurse -Force -ErrorAction SilentlyContinue
}
