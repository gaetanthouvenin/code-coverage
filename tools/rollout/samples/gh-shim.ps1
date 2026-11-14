#Requires -Version 7.2
# A stand-in for gh, for Test-Rollout.ps1: answers "gh api <path>" from the JSON files of
# ROLLOUT_SHIM_DIR (the path with "/" turned into "__"), records every call in ROLLOUT_SHIM_LOG, and
# answers the writes (api -X POST|PUT, pr create) without doing anything. Synthetic data only.
# No param block: every argument, -X and -f included, stays in $args.
$Arguments = [string[]] $args

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Content -LiteralPath $env:ROLLOUT_SHIM_LOG -Value ($Arguments | ConvertTo-Json -Compress)

# The open pull requests of a branch: "pr-list__<owner>__<repo>.json" when the test wrote one, else none.
if ($Arguments[0] -eq 'pr' -and $Arguments[1] -eq 'list') {
  $repo = $Arguments[[Array]::IndexOf($Arguments, '--repo') + 1]
  $file = Join-Path $env:ROLLOUT_SHIM_DIR ('pr-list__' + ($repo -replace '/', '__') + '.json')
  if (Test-Path -LiteralPath $file) { Get-Content -LiteralPath $file -Raw } else { Write-Output '[]' }
  exit 0
}

if ($Arguments[0] -eq 'pr' -and $Arguments[1] -eq 'create') {
  $repo = $Arguments[[Array]::IndexOf($Arguments, '--repo') + 1]
  Write-Output "https://github.com/$repo/pull/42"
  exit 0
}

if ($Arguments[0] -ne 'api') {
  Write-Error "gh-shim: unsupported command $($Arguments -join ' ')"
  exit 1
}

if ($Arguments -contains '-X') {
  Write-Output '{}'
  exit 0
}

$path = $Arguments[1]
$file = Join-Path $env:ROLLOUT_SHIM_DIR (($path -replace '/', '__') + '.json')

# A recorded refusal: "<path>.error" holds the message gh prints (e.g. "Branch not protected (HTTP 404)").
$refusal = [System.IO.Path]::ChangeExtension($file, '.error')
if (Test-Path -LiteralPath $refusal) {
  [Console]::Error.WriteLine((Get-Content -LiteralPath $refusal -Raw).Trim())
  exit 1
}

if (-not (Test-Path -LiteralPath $file)) {
  [Console]::Error.WriteLine('gh: Not Found (HTTP 404)')
  exit 1
}

Get-Content -LiteralPath $file -Raw
