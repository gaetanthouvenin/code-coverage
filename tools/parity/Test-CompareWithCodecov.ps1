#Requires -Version 7.2
<#
.SYNOPSIS
  Runs Compare-WithCodecov.ps1 on the frozen sample of samples/ and checks its reference outputs.

.DESCRIPTION
  No network: both APIs are read from the synthetic answers of samples/codecov and
  samples/codecoverage. The report is compared with samples/expected/report.txt and its JSON with
  samples/expected/report.json; the dry run must read nothing, and -Anonymize must leave no name.
  -Update rewrites both references from the current output, to review in the diff.

.PARAMETER Update
  Rewrites the reference outputs instead of checking them.

.EXAMPLE
  pwsh tools/parity/Test-CompareWithCodecov.ps1
#>
[CmdletBinding()]
param([switch] $Update)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script = Join-Path $PSScriptRoot 'Compare-WithCodecov.ps1'
$samples = Join-Path $PSScriptRoot 'samples'
$expectedText = Join-Path $samples 'expected/report.txt'
$expectedJson = Join-Path $samples 'expected/report.json'
$work = Join-Path ([System.IO.Path]::GetTempPath()) "parity-test-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $work | Out-Null
$failures = [System.Collections.Generic.List[string]]::new()

function ConvertTo-Lines([string] $Text) { return @($Text -split "`r?`n" | ForEach-Object { $_.TrimEnd() }) -join "`n" }

function Assert-True {
  param([string] $Name, [bool] $Condition, [string] $Detail = '')

  if ($Condition) { Write-Host "  ok: $Name" } else { $failures.Add("$Name $Detail") }
}

function Invoke-Compare([string[]] $Arguments) {
  $saved = @{ COVERAGE_API_TOKEN = $env:COVERAGE_API_TOKEN; CODECOV_API_TOKEN = $env:CODECOV_API_TOKEN }
  $env:COVERAGE_API_TOKEN = $null
  $env:CODECOV_API_TOKEN = $null
  try {
    $output = & pwsh -NoProfile -NonInteractive -File $script @Arguments 2>&1 | Out-String
    return [pscustomobject]@{ Output = $output; ExitCode = $LASTEXITCODE }
  }
  finally {
    foreach ($key in $saved.Keys) { [Environment]::SetEnvironmentVariable($key, $saved[$key]) }
  }
}

$common = @('-Slug', 'octo-org/app', '-CommitsFile', (Join-Path $samples 'commits.txt'), '-Pulls', '7,8,9',
  '-CodecovSamplePath', (Join-Path $samples 'codecov'), '-CodeCoverageSamplePath', (Join-Path $samples 'codecoverage'))

try {
  $text = Join-Path $work 'report.txt'
  $json = Join-Path $work 'report.json'
  $run = Invoke-Compare ($common + @('-OutputText', $text, '-OutputJson', $json))
  Assert-True 'the comparison exits with 0, gaps included' ($run.ExitCode -eq 0) $run.Output

  if ($Update) {
    Copy-Item -LiteralPath $text -Destination $expectedText -Force
    Copy-Item -LiteralPath $json -Destination $expectedJson -Force
    Write-Host 'References updated: review them in the diff.'
  }
  else {
    Assert-True 'the text report matches its reference' ((ConvertTo-Lines (Get-Content -Raw $text)) -ceq (ConvertTo-Lines (Get-Content -Raw $expectedText)))
    Assert-True 'the JSON report matches its reference' ((ConvertTo-Lines (Get-Content -Raw $json)) -ceq (ConvertTo-Lines (Get-Content -Raw $expectedJson)))
  }

  $report = Get-Content -Raw $json | ConvertFrom-Json
  Assert-True 'a coverage within 0.01 point is ok' (@($report.totals | Where-Object { $_.status -eq 'ok' }).Count -eq 2)
  Assert-True 'a coverage beyond it is a gap' (@($report.totals | Where-Object { $_.status -eq 'gap' }).Count -eq 1)
  Assert-True 'a commit Codecov lacks is reported' (@($report.totals | Where-Object { $_.status -eq 'missing-codecov' }).Count -eq 1)
  Assert-True 'per-file patch totals are compared' (@($report.patches | Where-Object { $_.pull -eq 7 }).status -join ',' -eq 'ok,gap,missing-codecoverage')
  Assert-True 'different bases are not judged' (@($report.patches | Where-Object { $_.pull -eq 8 }).status -join ',' -eq 'base-differs')
  Assert-True 'different heads are not judged' (@($report.patches | Where-Object { $_.pull -eq 9 }).status -join ',' -eq 'head-differs')
  $paths = @($report.patches | Where-Object { $_.pull -eq 7 }).path -join ','
  Assert-True 'files are matched on the head name, else the base name' ($paths -eq 'src/a.cs,src/b.cs,src/old/c.cs') $paths

  $anonymous = Invoke-Compare ($common + '-Anonymize')
  Assert-True 'an anonymized report names no repository nor file' ($anonymous.Output -notmatch 'octo-org|src/') $anonymous.Output

  # An API that answers an error: anonymized, the message names neither the repository nor the address.
  $listener = [System.Net.HttpListener]::new()
  $port = Get-Random -Minimum 20000 -Maximum 40000
  $listener.Prefixes.Add("http://127.0.0.1:$port/")
  $listener.Start()
  $server = Start-ThreadJob -ArgumentList $listener -ScriptBlock {
    param($listener)
    while ($listener.IsListening) {
      try { $context = $listener.GetContext() } catch { break }
      $context.Response.StatusCode = 500
      $context.Response.Close()
    }
  }
  $savedTokens = @{ COVERAGE_API_TOKEN = $env:COVERAGE_API_TOKEN; CODECOV_API_TOKEN = $env:CODECOV_API_TOKEN }
  try {
    $env:COVERAGE_API_TOKEN = 'cvp_synthetic'
    $env:CODECOV_API_TOKEN = 'synthetic'
    $failed = & pwsh -NoProfile -NonInteractive -File $script -ApiUrl "http://127.0.0.1:$port" -Slug 'octo-org/app' `
      -CommitsFile (Join-Path $samples 'commits.txt') -Execute -Anonymize 2>&1 | Out-String
    $failedCode = $LASTEXITCODE
  }
  finally {
    foreach ($key in $savedTokens.Keys) { [Environment]::SetEnvironmentVariable($key, $savedTokens[$key]) }
    $listener.Stop()
    $server | Wait-Job -Timeout 5 | Out-Null
    $server | Remove-Job -Force
  }
  Assert-True 'an error answer fails the run' ($failedCode -ne 0) $failed
  Assert-True 'anonymized, an error names the side and the status only' ($failed -match 'the API of codecoverage answered HTTP 500' -and $failed -notmatch 'octo-org|127\.0\.0\.1') $failed

  $dry = Invoke-Compare @('-Slug', 'octo-org/app')
  Assert-True 'the default is a dry run that reads nothing' ($dry.ExitCode -eq 0 -and $dry.Output -match 'nothing read') $dry.Output

  $noToken = Invoke-Compare @('-Slug', 'octo-org/app', '-Execute')
  Assert-True 'executing without the tokens fails' ($noToken.ExitCode -ne 0 -and $noToken.Output -match 'COVERAGE_API_TOKEN') $noToken.Output
}
finally {
  Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}

if ($failures.Count -gt 0) {
  Write-Host ''
  $failures | ForEach-Object { Write-Host "FAILED: $_" }
  exit 1
}

Write-Host 'All parity checks passed.'
exit 0
