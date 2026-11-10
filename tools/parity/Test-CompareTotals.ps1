#Requires -Version 7.2
<#
.SYNOPSIS
  Runs Compare-TotalsWithCodecov.ps1 on the frozen sample of samples/ and checks its reference outputs.

.DESCRIPTION
  No network and no database: Codecov is read from samples/codecov and CodeCoverage from
  samples/codecoverage. The report is compared with samples/expected/report.txt and its JSON with
  samples/expected/report.json. -Update rewrites both references from the current output, to review
  in the diff.

.PARAMETER Update
  Rewrites the reference outputs instead of checking them.

.EXAMPLE
  pwsh tools/parity/Test-CompareTotals.ps1
#>
[CmdletBinding()]
param([switch] $Update)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script = Join-Path $PSScriptRoot 'Compare-TotalsWithCodecov.ps1'
$samples = Join-Path $PSScriptRoot 'samples'
$expectedText = Join-Path $samples 'expected/report.txt'
$expectedJson = Join-Path $samples 'expected/report.json'
$sample = @{
  Slug              = 'octo-org/app'
  CommitsFile       = Join-Path $samples 'commits.txt'
  CodecovSamplePath = Join-Path $samples 'codecov'
  TotalsPath        = Join-Path $samples 'codecoverage/commit-flag-totals.json'
}
$failures = [System.Collections.Generic.List[string]]::new()

function ConvertTo-Lines {
  # Line ends and trailing spaces are not part of the reference.
  param([string] $Text)

  return @($Text -split "`r?`n" | ForEach-Object { $_.TrimEnd() }) -join "`n"
}

function Assert-Equal {
  param([string] $Name, $Actual, $Expected)

  if ("$Actual" -cne "$Expected") {
    $failures.Add("$Name`n--- expected`n$Expected`n--- actual`n$Actual")
  }
  else {
    Write-Host "  ok: $Name"
  }
}

function Invoke-Compare {
  # In a child process: the exit code is part of what is checked.
  param([string[]] $Arguments)

  $output = & pwsh -NoProfile -File $script @Arguments 2>&1 | Out-String

  return [pscustomobject]@{ Output = $output; ExitCode = $LASTEXITCODE }
}

$common = @(
  '-Slug', $sample.Slug, '-CommitsFile', $sample.CommitsFile,
  '-CodecovSamplePath', $sample.CodecovSamplePath, '-TotalsPath', $sample.TotalsPath
)
$jsonFile = Join-Path ([System.IO.Path]::GetTempPath()) "parity-$([guid]::NewGuid().ToString('N')).json"

try {
  Write-Host 'Report of the frozen sample'
  $report = Invoke-Compare ($common + @('-OutputJson', $jsonFile))
  $text = ConvertTo-Lines $report.Output.Trim()
  $json = ConvertTo-Lines (Get-Content -LiteralPath $jsonFile -Raw).Trim()

  if ($Update) {
    New-Item -ItemType Directory -Force -Path (Split-Path $expectedText) | Out-Null
    Set-Content -LiteralPath $expectedText -Value $text -Encoding utf8NoBOM
    Set-Content -LiteralPath $expectedJson -Value $json -Encoding utf8NoBOM
    Write-Host "References rewritten in $(Split-Path $expectedText): review them in the diff."
    return
  }

  Assert-Equal 'a report with gaps still exits with 0' $report.ExitCode 0
  Assert-Equal 'the report is the reference' $text (ConvertTo-Lines (Get-Content -LiteralPath $expectedText -Raw).Trim())
  Assert-Equal 'the JSON is the reference' $json (ConvertTo-Lines (Get-Content -LiteralPath $expectedJson -Raw).Trim())

  Write-Host 'Lines of the report'
  $rows = @(& $script @sample -PassThru)
  $byKey = @{}
  $rows | ForEach-Object { $byKey["$($_.Sha.Substring(0, 2))|$($_.Flag)"] = $_ }

  Assert-Equal 'one line per commit and per flag' $rows.Count 12
  Assert-Equal 'a rounding of Codecov is within the tolerance' $byKey['a1|'].Status 'ok'
  Assert-Equal 'a difference of exactly the tolerance is accepted' $byKey['b2|back'].Status 'ok'
  Assert-Equal 'a difference beyond the tolerance is a gap' $byKey['b2|front'].Status 'gap'
  Assert-Equal 'the gap of the project is signed' $byKey['c3|'].Delta ([decimal] -0.5)
  Assert-Equal 'a carried forward flag is marked' $byKey['c3|back'].IsCarriedForward $true
  Assert-Equal 'a commit unknown to Codecov is listed' $byKey['d4|'].Status 'missing-codecov'
  Assert-Equal 'a commit unknown to CodeCoverage is listed' $byKey['e5|'].Status 'missing-codecoverage'
  Assert-Equal 'a flag that only Codecov holds is listed' $byKey['e5|e2e'].Status 'missing-codecoverage'
  Assert-Equal 'no coverable line on both sides is equal' $byKey['f6|'].Status 'ok'
  Assert-Equal 'the rows of another organization, forge, repository or namespace are left out' $byKey['a1|'].CodeCoverageHits 12

  Write-Host 'Exit code and criterion'
  Assert-Equal '-FailOnGap exits with 1 on a gap' (Invoke-Compare ($common + @('-FailOnGap'))).ExitCode 1

  $met = Invoke-Compare ($common + @('-RequiredCommits', '2'))
  Assert-Equal 'the criterion is met at 2 commits within the tolerance' ($met.Output -match 'within the tolerance\): met\.') $true

  $clean = Invoke-Compare @(
    '-Slug', $sample.Slug, '-Sha', ('a1' * 20), '-CodecovSamplePath', $sample.CodecovSamplePath,
    '-TotalsPath', $sample.TotalsPath, '-FailOnGap'
  )
  Assert-Equal '-FailOnGap exits with 0 without gap' $clean.ExitCode 0
  Assert-Equal 'a report without gap says so' ($clean.Output -match 'No gap beyond 0\.01 point\.') $true

  $loose = @(& $script @sample -Tolerance 0.02 -PassThru | Where-Object { $_.Sha -eq ('b2' * 20) -and $_.Flag -eq 'front' })
  Assert-Equal 'the tolerance can be changed' $loose[0].Status 'ok'

  Write-Host 'Refusals'
  $badSha = Invoke-Compare @('-Slug', $sample.Slug, '-Sha', 'abc123', '-CodecovSamplePath', $sample.CodecovSamplePath, '-TotalsPath', $sample.TotalsPath)
  Assert-Equal 'a short SHA is refused' ($badSha.ExitCode -ne 0 -and $badSha.Output -match 'not a full SHA') $true

  $noSource = Invoke-Compare @('-Slug', $sample.Slug, '-Sha', ('a1' * 20), '-CodecovSamplePath', $sample.CodecovSamplePath)
  Assert-Equal 'no source for CodeCoverage is refused' ($noSource.ExitCode -ne 0 -and $noSource.Output -match '-TotalsPath') $true

  # No sample folder and no token: refused before any network call (the variable is emptied for the child).
  $savedToken = $env:CODECOV_API_TOKEN
  $env:CODECOV_API_TOKEN = $null

  try {
    $noCodecov = Invoke-Compare @('-Slug', $sample.Slug, '-Sha', ('a1' * 20), '-TotalsPath', $sample.TotalsPath)
  }
  finally {
    $env:CODECOV_API_TOKEN = $savedToken
  }

  Assert-Equal 'no source for Codecov is refused' ($noCodecov.ExitCode -ne 0 -and $noCodecov.Output -match '-CodecovSamplePath') $true
}
finally {
  Remove-Item -LiteralPath $jsonFile -ErrorAction SilentlyContinue
}

if ($failures.Count -gt 0) {
  $failures | ForEach-Object { Write-Host "FAILED: $_" -ForegroundColor Red }

  throw "$($failures.Count) check(s) failed."
}

Write-Host 'All checks passed.'

# The last child process was a refusal, checked above: its exit code is not the one of this script.
exit 0
