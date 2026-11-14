#Requires -Version 7.2
<#
.SYNOPSIS
  Imports the CODECOV_TOKEN values of an organization and of its repositories as upload tokens of
  CodeCoverage (M4.07), so that pipelines keep their secret. A dry run by default.

.DESCRIPTION
  Reads a CSV kept out of any repository, with the columns scope (organization or repository), owner,
  repo (empty for the organization), label and token. Each row is sent to the routes of M3.11:
    organization: POST /api/v1/orgs/{forge}/{owner}/upload-tokens
    repository:   POST /api/v1/repos/{forge}/{owner}/{repo}/upload-tokens
  with {"label": ..., "importedValue": ...}. A 409 means the value was imported already: done.

  COVERAGE_API_TOKEN, a cvp_ token of an administrator of the organization, comes from the
  environment. No token value is ever printed: a row is named by its scope, owner, repo and label.

.PARAMETER ApiUrl
  The public address of the Api of the instance.

.PARAMETER CsvPath
  The CSV of the values, out of any repository.

.PARAMETER Forge
  The forge segment of the addresses (github).

.PARAMETER Execute
  Sends the imports. Without it, the rows are checked and listed, and nothing is sent.

.EXAMPLE
  pwsh tools/rollout/Import-CodecovTokens.ps1 -ApiUrl https://coverage-api.example.com -CsvPath ~/codecov-tokens.csv -Execute
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory)]
  [string] $ApiUrl,
  [Parameter(Mandatory)]
  [string] $CsvPath,
  [ValidateSet('github')]
  [string] $Forge = 'github',
  [switch] $Execute
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$rows = @(Import-Csv -LiteralPath $CsvPath)
if ($rows.Count -eq 0) { throw "No row in '$CsvPath'." }

$base = $ApiUrl.TrimEnd('/')
$plans = foreach ($row in $rows) {
  $scope = "$($row.scope)".Trim().ToLowerInvariant()
  $owner = "$($row.owner)".Trim()
  $repo = "$($row.repo)".Trim()
  $label = "$($row.label)".Trim()
  $token = "$($row.token)".Trim()

  if ($scope -notin 'organization', 'repository') { throw "Unknown scope '$scope' (organization or repository)." }
  if ($owner -notmatch '^[A-Za-z0-9_.-]+$') { throw "Not an owner: '$owner'." }
  if ($scope -eq 'repository' -and $repo -notmatch '^[A-Za-z0-9_.-]+$') { throw "Not a repository for $owner : '$repo'." }
  if (-not $label) { throw "A label is required ($owner/$repo)." }
  if (-not $token) { throw "An empty token ($owner/$repo, $label)." }

  $uri = if ($scope -eq 'organization') { "$base/api/v1/orgs/$Forge/$owner/upload-tokens" } else { "$base/api/v1/repos/$Forge/$owner/$repo/upload-tokens" }
  $name = if ($scope -eq 'organization') { "organization $owner" } else { "repository $owner/$repo" }
  [pscustomobject]@{ Name = $name; Label = $label; Uri = $uri; Token = $token }
}

if (-not $Execute) {
  foreach ($plan in $plans) { Write-Output "dry run: import '$($plan.Label)' for $($plan.Name) (POST $($plan.Uri))" }
  Write-Output "dry run: $(@($plans).Count) tokens, nothing sent (add -Execute, with COVERAGE_API_TOKEN set)."
  exit 0
}

if ([string]::IsNullOrWhiteSpace($env:COVERAGE_API_TOKEN)) { throw 'Set COVERAGE_API_TOKEN in the environment.' }

$headers = @{ Authorization = "Bearer $env:COVERAGE_API_TOKEN" }
$refused = 0

foreach ($plan in $plans) {
  $body = @{ label = $plan.Label; importedValue = $plan.Token } | ConvertTo-Json -Compress
  $response = Invoke-WebRequest -Method Post -Uri $plan.Uri -Headers $headers -Body $body -ContentType 'application/json' -SkipHttpErrorCheck

  switch ($response.StatusCode) {
    201 { Write-Output "imported: '$($plan.Label)' for $($plan.Name)" }
    409 { Write-Output "already imported: '$($plan.Label)' for $($plan.Name)" }
    default {
      $refused++
      $code = 'unknown'
      try { $code = ($response.Content | ConvertFrom-Json).errorCode } catch { $code = 'unknown' }
      Write-Output "refused: '$($plan.Label)' for $($plan.Name) (HTTP $($response.StatusCode), $code)"
    }
  }
}

exit ([int]($refused -gt 0))
