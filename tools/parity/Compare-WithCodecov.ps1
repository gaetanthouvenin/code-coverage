#Requires -Version 7.2
<#
.SYNOPSIS
  Compares the totals and the per-file patch totals of CodeCoverage with those of Codecov, through
  both APIs (M4.06). A dry run by default.

.DESCRIPTION
  For each commit, the project totals and the totals of each flag of CodeCoverage
  (GET /api/v1/repos/{forge}/{owner}/{repo}/commits/{sha}) are compared with the report of Codecov
  (GET /api/v2/{service}/{owner}/repos/{repo}/report/?sha={sha}, then &flag={flag}): within
  -Tolerance points (0.01) the line is ok.

  For each pull request, the patch totals of each file of CodeCoverage
  (GET /api/v1/repos/{forge}/{owner}/{repo}/pulls/{number}) are compared with those of Codecov
  (GET /api/v2/{service}/{owner}/repos/{repo}/compare/?pullid={number}): the hits, misses and
  partials must be identical. When the two services compared the head to different bases, the pull
  request is reported base-differs (head-differs when Codecov compared another head) and its files
  are not judged. CodeCoverage does not serve the
  patch lines (M3.10): a line-by-line gap is explained in docs/parity-log.md.

  One organization per run: COVERAGE_API_TOKEN is a cvp_ token of a reader of the repository in that
  organization (never an instance-wide credential, never SQL). CODECOV_API_TOKEN is an API token of
  Codecov. Both come from the environment and are never printed.

  Modes:
    - default: a dry run, which lists the calls it would make, and reads nothing;
    - -Execute: reads both APIs;
    - -CodecovSamplePath and -CodeCoverageSamplePath: reads frozen answers, offline (the self-test).

.PARAMETER ApiUrl
  The public address of the Api of the instance.

.PARAMETER Slug
  The repository, owner/name.

.PARAMETER Forge
  The forge segment of the address (github), also the service of Codecov.

.PARAMETER CommitsFile
  A file with one full SHA per line. Without it, the -Count most recent commits CodeCoverage lists.

.PARAMETER Pulls
  The pull requests to compare, e.g. 7,8. Without them, the -Count most recent open ones CodeCoverage
  lists.

.PARAMETER Count
  How many commits and pull requests are discovered when none is given (20).

.PARAMETER Tolerance
  The largest coverage gap, in points, that counts as ok (0.01, bounds included).

.PARAMETER CodecovSamplePath
  Frozen answers of Codecov: report-{sha}.json, report-{sha}.{flag}.json, compare-{number}.json.

.PARAMETER CodeCoverageSamplePath
  Frozen answers of CodeCoverage: commit-{sha}.json, pull-{number}.json.

.PARAMETER OutputJson
  Where the report is written as JSON.

.PARAMETER OutputText
  Where the report is written as text (it is always printed).

.PARAMETER Execute
  Reads both APIs. Without it (and without samples), nothing is read.

.PARAMETER Anonymize
  Writes no repository name nor file path in the reports: the repository becomes <repository> and
  each path a short hash of it. For a report published where others can read it (the artifacts of a
  public repository).

.EXAMPLE
  pwsh tools/parity/Compare-WithCodecov.ps1 -ApiUrl https://coverage-api.example.com -Slug my-org/my-repo -Execute -OutputJson parity.json
#>
[CmdletBinding()]
param(
  [string] $ApiUrl = 'http://localhost',
  [Parameter(Mandatory)]
  [ValidatePattern('^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$')]
  [string] $Slug,
  [ValidateSet('github')]
  [string] $Forge = 'github',
  [string] $CommitsFile,
  [string[]] $Pulls = @(),
  [ValidateRange(1, 100)]
  [int] $Count = 20,
  [decimal] $Tolerance = 0.01,
  [string] $CodecovSamplePath,
  [string] $CodeCoverageSamplePath,
  [string] $OutputJson,
  [string] $OutputText,
  [switch] $Execute,
  [switch] $Anonymize
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$owner, $repo = $Slug.Split('/')
$offline = [bool] ($CodecovSamplePath -and $CodeCoverageSamplePath)
$base = $ApiUrl.TrimEnd('/')
$coverageRoot = "$base/api/v1/repos/$Forge/$owner/$repo"
$codecovRoot = "https://api.codecov.io/api/v2/$Forge/$owner/repos/$repo"

if (-not $offline -and -not $Execute) {
  Write-Output "dry run: GET $coverageRoot/commits?pageSize=$Count (unless -CommitsFile)"
  Write-Output "dry run: GET $coverageRoot/pulls?state=open&pageSize=$Count (unless -Pulls)"
  Write-Output "dry run: GET $coverageRoot/commits/{sha}, GET $codecovRoot/report/?sha={sha} and &flag={flag}"
  Write-Output "dry run: GET $coverageRoot/pulls/{number}, GET $codecovRoot/compare/?pullid={number}"
  Write-Output 'dry run: nothing read (add -Execute, with COVERAGE_API_TOKEN and CODECOV_API_TOKEN set).'
  exit 0
}

if (-not $offline) {
  if ([string]::IsNullOrWhiteSpace($env:COVERAGE_API_TOKEN) -or [string]::IsNullOrWhiteSpace($env:CODECOV_API_TOKEN)) {
    throw 'Set COVERAGE_API_TOKEN (a cvp_ token of the organization) and CODECOV_API_TOKEN in the environment.'
  }
}

function Read-Answer {
  # A frozen answer offline, else the API with its token. A missing answer is $null.
  param([string] $Side, [string] $Sample, [string] $Uri)

  if ($offline) {
    $folder = if ($Side -eq 'codecov') { $CodecovSamplePath } else { $CodeCoverageSamplePath }
    $path = Join-Path $folder $Sample
    if (-not (Test-Path -LiteralPath $path)) { return $null }
    return Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -Depth 32
  }

  $token = if ($Side -eq 'codecov') { $env:CODECOV_API_TOKEN } else { $env:COVERAGE_API_TOKEN }
  $response = Invoke-WebRequest -Uri $Uri -Headers @{ Authorization = "Bearer $token"; Accept = 'application/json' } -SkipHttpErrorCheck
  if ($response.StatusCode -eq 404) { return $null }
  if ($response.StatusCode -ge 400) {
    # Anonymized, the address (which names the repository) stays out of the message.
    $what = $Anonymize ? "the API of $Side" : "GET $Uri"
    throw "$what answered HTTP $($response.StatusCode)."
  }
  return $response.Content | ConvertFrom-Json -Depth 32
}

function Get-Property {
  param($Object, [string] $Name)

  if ($null -eq $Object) { return $null }
  $property = $Object.PSObject.Properties[$Name]
  return $property ? $property.Value : $null
}

function Compare-Totals {
  # Coverage within the tolerance, and the same counters, for one commit and one flag.
  param([string] $Sha, [string] $Flag, $Codecov, $Coverage)

  $codecovCoverage = Get-Property $Codecov 'coverage'
  $coverageCoverage = Get-Property $Coverage 'coverage'
  $status = if ($null -eq $Codecov -and $null -eq $Coverage) { 'missing-both' }
            elseif ($null -eq $Codecov -or $null -eq $codecovCoverage) { 'missing-codecov' }
            elseif ($null -eq $Coverage -or $null -eq $coverageCoverage) { 'missing-codecoverage' }
            else { $null }

  $delta = $null
  $countsSame = $null
  if ($null -eq $status) {
    $delta = [decimal] $coverageCoverage - [decimal] $codecovCoverage
    $countsSame = ((Get-Property $Codecov 'hits') -eq (Get-Property $Coverage 'hits')) -and
                  ((Get-Property $Codecov 'misses') -eq (Get-Property $Coverage 'misses')) -and
                  ((Get-Property $Codecov 'partials') -eq (Get-Property $Coverage 'partials'))
    $status = ([Math]::Abs($delta) -le $Tolerance) ? 'ok' : 'gap'
  }

  return [ordered]@{
    sha          = $Sha
    flag         = $Flag
    codecov      = $codecovCoverage
    codeCoverage = $coverageCoverage
    delta        = $delta
    countsSame   = $countsSame
    status       = $status
  }
}

function Format-Patch {
  param($Patch)

  if ($null -eq $Patch) { return $null }
  return '{0}/{1}/{2}' -f (Get-Property $Patch 'hits'), (Get-Property $Patch 'misses'), (Get-Property $Patch 'partials')
}

function Get-Commits {
  if ($CommitsFile) {
    return @(Get-Content -LiteralPath $CommitsFile | ForEach-Object { $_.Trim() } | Where-Object { $_ -and -not $_.StartsWith('#') })
  }
  $page = Read-Answer 'codecoverage' 'commits.json' "$coverageRoot/commits?pageSize=$Count"
  return @((Get-Property $page 'items') | ForEach-Object { Get-Property $_ 'sha' })
}

function Get-Pulls {
  $given = @($Pulls | ForEach-Object { $_ -split ',' } | Where-Object { $_.Trim() } | ForEach-Object { [int] $_.Trim() })
  if ($given.Count -gt 0) { return $given }
  $page = Read-Answer 'codecoverage' 'pulls.json' "$coverageRoot/pulls?state=open&pageSize=$Count"
  return @((Get-Property $page 'items') | ForEach-Object { [int] (Get-Property $_ 'number') })
}

$totals = [System.Collections.Generic.List[object]]::new()
$patches = [System.Collections.Generic.List[object]]::new()

foreach ($sha in Get-Commits) {
  if ($sha -notmatch '^[0-9a-f]{40}$') { throw "Not a full SHA: '$sha'." }

  $commit = Read-Answer 'codecoverage' "commit-$sha.json" "$coverageRoot/commits/$sha"
  $report = Read-Answer 'codecov' "report-$sha.json" "$codecovRoot/report/?sha=$sha"
  $totals.Add((Compare-Totals $sha '' (Get-Property $report 'totals') (Get-Property $commit 'totals')))

  foreach ($flag in @(Get-Property $commit 'flags') | Where-Object { $_ -and (Get-Property $_ 'flag') }) {
    $name = Get-Property $flag 'flag'
    $flagReport = Read-Answer 'codecov' "report-$sha.$name.json" "$codecovRoot/report/?sha=$sha&flag=$([Uri]::EscapeDataString($name))"
    $totals.Add((Compare-Totals $sha $name (Get-Property $flagReport 'totals') (Get-Property $flag 'totals')))
  }
}

foreach ($number in Get-Pulls) {
  $pull = Read-Answer 'codecoverage' "pull-$number.json" "$coverageRoot/pulls/$number"
  $compare = Read-Answer 'codecov' "compare-$number.json" "$codecovRoot/compare/?pullid=$number"

  if ($null -eq $pull -or $null -eq $compare) {
    $patches.Add([ordered]@{ pull = $number; path = $null; status = ($null -eq $pull) ? 'missing-codecoverage' : 'missing-codecov'; diffIncomplete = $null })
    continue
  }

  # The compare serializer of Codecov names the pair base_commit and head_commit (commit ids), and
  # each file name is a {base, head} pair: the head name, or the base name for a file the head lacks.
  $ourBase = Get-Property (Get-Property $pull 'bases') 'comparedToSha'
  $ourHead = Get-Property (Get-Property $pull 'bases') 'headSha'
  $theirBase = Get-Property $compare 'base_commit'
  $theirHead = Get-Property $compare 'head_commit'
  $incomplete = Get-Property $pull 'diffIncomplete'

  if ($ourBase -ne $theirBase) {
    $patches.Add([ordered]@{ pull = $number; path = $null; status = 'base-differs'; codeCoverageBase = $ourBase; codecovBase = $theirBase; diffIncomplete = $incomplete })
    continue
  }

  if ($ourHead -ne $theirHead) {
    $patches.Add([ordered]@{ pull = $number; path = $null; status = 'head-differs'; codeCoverageHead = $ourHead; codecovHead = $theirHead; diffIncomplete = $incomplete })
    continue
  }

  $theirs = @{}
  foreach ($file in @(Get-Property $compare 'files')) {
    $patch = Get-Property (Get-Property $file 'totals') 'patch'
    $name = Get-Property $file 'name'
    $key = (Get-Property $name 'head') ?? (Get-Property $name 'base')
    if ($null -ne $patch -and $null -ne $key) { $theirs[$key] = $patch }
  }

  $ours = @{}
  foreach ($file in @(Get-Property $pull 'files')) {
    $patch = Get-Property $file 'patch'
    if ($null -ne $patch -and [int] (Get-Property $patch 'lines') -gt 0) { $ours[(Get-Property $file 'path')] = $patch }
  }

  foreach ($path in @($ours.Keys + $theirs.Keys | Sort-Object -Unique)) {
    $a = $ours[$path]
    $b = $theirs[$path]
    $status = if ($null -eq $a) { 'missing-codecoverage' }
              elseif ($null -eq $b) { 'missing-codecov' }
              elseif ((Get-Property $a 'hits') -eq (Get-Property $b 'hits') -and (Get-Property $a 'misses') -eq (Get-Property $b 'misses') -and (Get-Property $a 'partials') -eq (Get-Property $b 'partials')) { 'ok' }
              else { 'gap' }

    $patches.Add([ordered]@{
      pull           = $number
      path           = $path
      codeCoverage   = Format-Patch $a
      codecov        = Format-Patch $b
      status         = $status
      diffIncomplete = $incomplete
    })
  }
}

function Get-Label([string] $Path) {
  if (-not $Anonymize -or -not $Path) { return $Path }
  $hash = [System.Security.Cryptography.SHA256]::HashData([System.Text.Encoding]::UTF8.GetBytes($Path))
  return 'file-' + [Convert]::ToHexString($hash).Substring(0, 8).ToLowerInvariant()
}

foreach ($row in $patches) { $row.path = Get-Label $row.path }
$reportedSlug = $Anonymize ? '<repository>' : $Slug

function Format-Number($Value) {
  if ($null -eq $Value) { return '' }
  return ([decimal] $Value).ToString('0.00000', [Globalization.CultureInfo]::InvariantCulture)
}

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add("Parity of $reportedSlug with Codecov")
$lines.Add('')
$lines.Add('Totals')
$lines.Add(('{0,-10} {1,-12} {2,10} {3,13} {4,9} {5,-6} {6}' -f 'Commit', 'Flag', 'Codecov', 'CodeCoverage', 'Delta', 'Counts', 'Status'))
foreach ($row in $totals) {
  $delta = ($null -eq $row.delta) ? '' : $row.delta.ToString('+0.00000;-0.00000;0.00000', [Globalization.CultureInfo]::InvariantCulture)
  $counts = ($null -eq $row.countsSame) ? '' : ($row.countsSame ? 'same' : 'differ')
  $flag = $row.flag ? $row.flag : '(project)'
  $lines.Add(('{0,-10} {1,-12} {2,10} {3,13} {4,9} {5,-6} {6}' -f $row.sha.Substring(0, 10), $flag, (Format-Number $row.codecov), (Format-Number $row.codeCoverage), $delta, $counts, $row.status))
}
$lines.Add('')
$lines.Add('Patch totals per file (hits/misses/partials)')
$lines.Add(('{0,-6} {1,-40} {2,-12} {3,-12} {4}' -f 'Pull', 'File', 'Codecov', 'CodeCoverage', 'Status'))
foreach ($row in $patches) {
  $lines.Add(('{0,-6} {1,-40} {2,-12} {3,-12} {4}' -f $row.pull, $row.path, $row['codecov'], $row['codeCoverage'], $row.status))
}

$gaps = @($totals | Where-Object { $_.status -ne 'ok' }).Count + @($patches | Where-Object { $_.status -ne 'ok' }).Count
$lines.Add('')
$lines.Add("$($totals.Count) totals and $($patches.Count) patch lines compared, $gaps not ok.")

$text = $lines -join "`n"
Write-Output $text
if ($OutputText) { Set-Content -LiteralPath $OutputText -Value $text -Encoding utf8NoBOM }
if ($OutputJson) {
  [ordered]@{ slug = $reportedSlug; tolerance = $Tolerance; totals = $totals; patches = $patches } |
    ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $OutputJson -Encoding utf8NoBOM
}

# A gap is a finding to explain in docs/parity-log.md, not a failure of the script.
exit 0
