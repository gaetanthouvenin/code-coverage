#Requires -Version 7.2
<#
.SYNOPSIS
  Starts the import of the Codecov history of one or more repositories (M5.06). Dry run by default.

.DESCRIPTION
  Calls POST /api/v1/repos/{forge}/{owner}/{repo}/codecov-import of a CodeCoverage instance for each
  repository. The instance stores the Codecov API token protected, runs the import in a job, and
  deletes the token when the import ends (or after 7 days at most).

  Tokens are read from environment variables only, never from the command line:
    COVERAGE_API_TOKEN  a cvp_ API token of an administrator of the repositories (one organization);
    CODECOV_API_TOKEN   a Codecov API token that can read the repositories on Codecov.
  Neither value is ever written to the output.

  Without -Execute, the script only prints what it would call. With -Execute, it calls the instance
  and prints one line per repository: started, or the error code of the refusal. It exits with 1
  when a call was refused, 0 otherwise.

.PARAMETER ApiUrl
  The public address of the Api of the instance, e.g. https://coverage-api.example.com.

.PARAMETER Repository
  One or more repositories, written owner/name.

.PARAMETER RepositoriesFile
  A file with one owner/name per line (blank lines and lines starting with # are skipped).

.PARAMETER Forge
  The forge segment of the address: github (default).

.PARAMETER Execute
  Calls the instance. Without it, nothing is sent.

.EXAMPLE
  $env:COVERAGE_API_TOKEN = '...'; $env:CODECOV_API_TOKEN = '...'
  pwsh tools/rollout/Start-CodecovImport.ps1 -ApiUrl https://coverage-api.example.com -Repository octo-org/app -Execute
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory)]
  [string] $ApiUrl,
  [string[]] $Repository = @(),
  [string] $RepositoriesFile,
  [ValidateSet('github')]
  [string] $Forge = 'github',
  [switch] $Execute
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$names = [System.Collections.Generic.List[string]]::new()
$Repository | ForEach-Object { $names.Add($_.Trim()) }

if ($RepositoriesFile) {
  Get-Content -LiteralPath $RepositoriesFile |
    ForEach-Object { $_.Trim() } |
    Where-Object { $_ -and -not $_.StartsWith('#') } |
    ForEach-Object { $names.Add($_) }
}

if ($names.Count -eq 0) {
  throw 'Give at least one repository, with -Repository or -RepositoriesFile.'
}

foreach ($name in $names) {
  if ($name -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$') {
    throw "Not an owner/name: '$name'."
  }
}

$base = $ApiUrl.TrimEnd('/')

if (-not $Execute) {
  foreach ($name in $names) {
    $owner, $repo = $name.Split('/')
    Write-Output "dry run: POST $base/api/v1/repos/$Forge/$owner/$repo/codecov-import"
  }

  Write-Output "dry run: $($names.Count) repositories, nothing sent (add -Execute)."
  exit 0
}

$apiToken = $env:COVERAGE_API_TOKEN
$codecovToken = $env:CODECOV_API_TOKEN

if ([string]::IsNullOrWhiteSpace($apiToken) -or [string]::IsNullOrWhiteSpace($codecovToken)) {
  throw 'Set COVERAGE_API_TOKEN and CODECOV_API_TOKEN in the environment.'
}

$headers = @{ Authorization = "Bearer $apiToken" }
$body = @{ token = $codecovToken } | ConvertTo-Json -Compress
$refused = 0

foreach ($name in $names) {
  $owner, $repo = $name.Split('/')
  $uri = "$base/api/v1/repos/$Forge/$([Uri]::EscapeDataString($owner))/$([Uri]::EscapeDataString($repo))/codecov-import"
  $response = Invoke-WebRequest -Method Post -Uri $uri -Headers $headers -Body $body -ContentType 'application/json' -SkipHttpErrorCheck

  if ($response.StatusCode -eq 202) {
    Write-Output "started: $name"
    continue
  }

  $refused++
  $code = 'unknown'

  try {
    $problem = $response.Content | ConvertFrom-Json
    if ($problem.PSObject.Properties['errorCode']) { $code = $problem.errorCode }
  }
  catch {
    # Not a problem document: the status code is enough.
  }

  Write-Output "refused: $name (HTTP $($response.StatusCode), $code)"
}

exit ([int]($refused -gt 0))
