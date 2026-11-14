#Requires -Version 7.2
<#
.SYNOPSIS
  Opens, in each repository, a pull request that removes the codecov/codecov-action steps (M4.08, after
  the switch). A dry run by default.

.DESCRIPTION
  Every step of .github/workflows that uses codecov/codecov-action is removed; the CodeCoverage step
  stays. The CODECOV_TOKEN secret is kept: imported as an upload token, it may still authenticate
  the CodeCoverage step. The YAML is edited as text: only the removed lines change.

  Run it only after the cut-over of the organization (docs/cutover-runbook.md): Codecov's
  notifications stopped, the prefix switched, the branch protections checked.

.PARAMETER Repository
  One or more repositories, owner/name.

.PARAMETER RepositoriesFile
  A file with one owner/name per line.

.PARAMETER Branch
  The branch of the pull requests.

.PARAMETER Execute
  Creates the branches and opens the pull requests.

.EXAMPLE
  pwsh tools/rollout/Remove-CodecovUpload.ps1 -RepositoriesFile switched.txt
#>
[CmdletBinding()]
param(
  [string[]] $Repository = @(),
  [string] $RepositoriesFile,
  [string] $Branch = 'coverage/remove-codecov-upload',
  [switch] $Execute
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'RolloutTools.psm1') -Force

$title = 'Remove the Codecov upload'
$body = @'
The organization switched to CodeCoverage: its statuses carry the codecov/* names the branch
protections require. This removes the codecov/codecov-action steps; the CodeCoverage upload stays.
The CODECOV_TOKEN secret is kept on purpose: imported as an upload token, it may still authenticate the
CodeCoverage step.
'@

$edit = {
  param([string] $Text)
  Remove-CodecovUploadStep -Text $Text
}

# One failed repository does not stop the batch: it is reported, and the script exits with 1 at the end.
# A rerun skips the repositories already done (no Codecov step left, or the pull request still open).
$failed = Invoke-ForEachRepository -Names (Get-RepositoryList -Repository $Repository -RepositoriesFile $RepositoriesFile) -Action {
  param([string] $Name)
  Update-WorkflowsByPullRequest -Name $Name -Edit $edit -Branch $Branch -Title $title -Body $body -Execute:$Execute
}
if ($failed -gt 0) {
  Write-Output "$failed repository(ies) failed."
  exit 1
}
