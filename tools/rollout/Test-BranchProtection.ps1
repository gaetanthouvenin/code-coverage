#Requires -Version 7.2
<#
.SYNOPSIS
  Reads the required status checks of the protected branch of each repository and says whether the
  statuses of CodeCoverage will satisfy them after the switch (M4.08). Read-only.

.DESCRIPTION
  For each repository, reads the classic branch protection of the branch
  (GET repos/{owner}/{repo}/branches/{branch}/protection/required_status_checks) and the rulesets that
  apply to it (GET repos/{owner}/{repo}/rules/branches/{branch}). Each required check whose context
  starts with the prefix (codecov/ by default) is listed with the application it is pinned to:
    - any application (app_id or integration_id absent or -1): ok, the statuses of CodeCoverage under
      the codecov prefix satisfy it;
    - the App of CodeCoverage (-ExpectedAppId): ok;
    - another application (Codecov's): pinned, the protection must be edited before the switch, or the
      pull requests stay blocked.
  Only GitHub's "Branch not protected" (or "Required status checks not enabled") means no classic
  protection. Any other 404 or 403 is reported as unknown: GitHub answers 404 to a token without admin
  access to the repository, which must not pass for an all-clear.
  Nothing is written. Exits with 1 when a check is pinned to another application, or when the
  protection of a repository could not be read.

.PARAMETER Repository
  One or more repositories, owner/name.

.PARAMETER RepositoriesFile
  A file with one owner/name per line.

.PARAMETER Branch
  The protected branch; the default branch of each repository when empty.

.PARAMETER Prefix
  The prefix of the contexts to check (codecov/).

.PARAMETER ExpectedAppId
  The id of the GitHub App of CodeCoverage, which may be the pinned source.

.EXAMPLE
  pwsh tools/rollout/Test-BranchProtection.ps1 -RepositoriesFile pilot.txt -ExpectedAppId 123456
#>
[CmdletBinding()]
param(
  [string[]] $Repository = @(),
  [string] $RepositoriesFile,
  [string] $Branch = '',
  [string] $Prefix = 'codecov/',
  [long] $ExpectedAppId = 0
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'RolloutTools.psm1') -Force

$unknown = 0

# Returns the answer, $null when GitHub says the branch has no such protection, or 'unknown' when the
# answer cannot be read (no admin access, missing repository): never a silent all-clear.
function Read-Json([string] $Path) {
  try {
    return Invoke-Gh api $Path | ConvertFrom-Json -Depth 16
  }
  catch {
    if ($_.Exception.Message -match 'Branch not protected|Required status checks not enabled') { return $null }
    if ($_.Exception.Message -match '\b(403|404)\b|Not Found|Forbidden') { return 'unknown' }
    throw
  }
}

function Get-Verdict([object] $AppId) {
  if ($null -eq $AppId -or [long] $AppId -eq -1) { return 'ok (any application)' }
  if ($ExpectedAppId -gt 0 -and [long] $AppId -eq $ExpectedAppId) { return 'ok (CodeCoverage)' }
  return "pinned to application $AppId"
}

$pinned = 0
Write-Output ('{0,-30} {1,-12} {2,-10} {3,-28} {4}' -f 'Repository', 'Branch', 'Source', 'Context', 'Verdict')

foreach ($name in Get-RepositoryList -Repository $Repository -RepositoriesFile $RepositoriesFile) {
  $target = $Branch
  if (-not $target) { $target = (Invoke-Gh api "repos/$name" | ConvertFrom-Json).default_branch }

  $found = 0
  $classic = Read-Json "repos/$name/branches/$target/protection/required_status_checks"
  if ($classic -is [string]) {
    $unknown++
    $found++
    Write-Output ('{0,-30} {1,-12} {2,-10} {3,-28} {4}' -f $name, $target, 'protection', '-', 'unknown: not readable (admin access needed)')
  }
  elseif ($null -ne $classic -and $classic.PSObject.Properties['checks']) {
    foreach ($check in @($classic.checks) | Where-Object { $_.context.StartsWith($Prefix, [StringComparison]::Ordinal) }) {
      $appId = $check.PSObject.Properties['app_id'] ? $check.app_id : $null
      $verdict = Get-Verdict $appId
      if ($verdict.StartsWith('pinned')) { $pinned++ }
      $found++
      Write-Output ('{0,-30} {1,-12} {2,-10} {3,-28} {4}' -f $name, $target, 'protection', $check.context, $verdict)
    }
  }

  $rules = Read-Json "repos/$name/rules/branches/$target"
  if ($rules -is [string]) {
    $unknown++
    $found++
    Write-Output ('{0,-30} {1,-12} {2,-10} {3,-28} {4}' -f $name, $target, 'ruleset', '-', 'unknown: not readable')
    $rules = @()
  }
  foreach ($rule in @($rules) | Where-Object { $null -ne $_ -and $_.type -eq 'required_status_checks' }) {
    foreach ($check in @($rule.parameters.required_status_checks) | Where-Object { $_.context.StartsWith($Prefix, [StringComparison]::Ordinal) }) {
      $appId = $check.PSObject.Properties['integration_id'] ? $check.integration_id : $null
      $verdict = Get-Verdict $appId
      if ($verdict.StartsWith('pinned')) { $pinned++ }
      $found++
      Write-Output ('{0,-30} {1,-12} {2,-10} {3,-28} {4}' -f $name, $target, 'ruleset', $check.context, $verdict)
    }
  }

  if ($found -eq 0) {
    Write-Output ('{0,-30} {1,-12} {2,-10} {3,-28} {4}' -f $name, $target, '-', '-', "no required $Prefix* check")
  }
}

if ($unknown -gt 0) {
  Write-Output "$unknown protection(s) could not be read: run again with an account that administers the repositories."
}

if ($pinned -gt 0) {
  Write-Output "$pinned required check(s) pinned to another application: edit them before the switch (docs/cutover-runbook.md)."
}

if ($pinned -gt 0 -or $unknown -gt 0) {
  exit 1
}

Write-Output 'Every required check will be satisfied by the statuses of CodeCoverage after the switch.'
exit 0
