#Requires -Version 7.2
<#
.SYNOPSIS
  Opens, in each repository, a pull request that adds the CodeCoverage upload next to the Codecov one
  (M4.07, the dual upload of the pilot). A dry run by default.

.DESCRIPTION
  For each workflow of .github/workflows that uses codecov/codecov-action, a step that uses the
  composite action of CodeCoverage is inserted right after the Codecov step, with the flags, files and
  name of the Codecov step. A workflow that grants id-token: write uploads with OIDC; another one uses
  the imported CODECOV_TOKEN secret. A workflow that already uses the action, or has no Codecov step,
  is left as it is. The YAML is edited as text: only the added lines change.

  gh must be signed in with the right to push branches and open pull requests (GH_TOKEN or gh auth).
  Without -Execute, the script reads the workflows and prints what would change, and writes nothing.

.PARAMETER Repository
  One or more repositories, owner/name.

.PARAMETER RepositoriesFile
  A file with one owner/name per line.

.PARAMETER ActionRepository
  The repository of the composite action, owner/code-coverage.

.PARAMETER Version
  The release of the uploader the step pins (1.2.3).

.PARAMETER Sha256
  The SHA-256 of coverage-upload-linux-x64 of that release, pinned in the step (recommended).

.PARAMETER Url
  The public address of the Api of the instance.

.PARAMETER Branch
  The branch of the pull requests.

.PARAMETER Execute
  Creates the branches and opens the pull requests.

.EXAMPLE
  pwsh tools/rollout/Add-CoverageUpload.ps1 -RepositoriesFile pilot.txt -ActionRepository my-org/code-coverage -Version 1.2.3 -Url https://coverage-api.example.com
#>
[CmdletBinding()]
param(
  [string[]] $Repository = @(),
  [string] $RepositoriesFile,
  [Parameter(Mandatory)]
  [ValidatePattern('^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$')]
  [string] $ActionRepository,
  [Parameter(Mandatory)]
  [ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+([-+][0-9A-Za-z.-]+)?$')]
  [string] $Version,
  [ValidatePattern('^([0-9a-fA-F]{64})?$')]
  [string] $Sha256 = '',
  [Parameter(Mandatory)]
  [ValidatePattern('^https?://')]
  [string] $Url,
  [string] $Branch = 'coverage/add-codecoverage-upload',
  [switch] $Execute
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'RolloutTools.psm1') -Force

$title = 'Upload coverage to CodeCoverage next to Codecov'
$body = @"
This adds an upload to the CodeCoverage instance ($Url) next to the Codecov one, for the pilot: both
services see every commit. CodeCoverage publishes its statuses as ``coverage/*`` until the switch, so
nothing changes for the branch protections. The step never fails the build.

The step reuses the token of the Codecov step (the imported ``CODECOV_TOKEN``), or OIDC when the job grants ``id-token: write``.
"@

$edit = {
  param([string] $Text)
  Add-CoverageUploadStep -Text $Text -ActionRepository $ActionRepository -Version $Version -Url $Url -Sha256 $Sha256
}

# One failed repository (an error of gh, a leftover branch) does not stop the batch: it is reported, and
# the script exits with 1 at the end. A rerun skips the repositories already done.
$failed = Invoke-ForEachRepository -Names (Get-RepositoryList -Repository $Repository -RepositoriesFile $RepositoriesFile) -Action {
  param([string] $Name)
  Update-WorkflowsByPullRequest -Name $Name -Edit $edit -Branch $Branch -Title $title -Body $body -Execute:$Execute
}
if ($failed -gt 0) {
  Write-Output "$failed repository(ies) failed."
  exit 1
}
