#Requires -Version 7.2
<#
.SYNOPSIS
  Compares the totals of CodeCoverage with those of Codecov, per commit and per flag (exit criterion of M1).

.DESCRIPTION
  The exit criterion of the M1 pilot is « totals within ±0.01 % of Codecov on 20 commits ». For each
  commit, this script reads:
    - the totals of Codecov: the answers of its API v2 (report of a commit, then one per flag), either
      from a folder of frozen answers (-CodecovSamplePath, no network) or from the API itself
      (-CodecovToken);
    - the totals of CodeCoverage: the rows of CommitFlagTotals, either from the frozen output of the
      query of sql/ (-TotalsPath) or from the database of a compose stack (-Container), read only.
  It prints one line per commit and per flag, lists the gaps beyond the tolerance, and tells whether
  the criterion is met.

  It will be replaced by a version that reads the API of CodeCoverage in M4.06: until then the totals
  are read from the database by an operator, across the organizations, and filtered here on -Slug.

  Nothing is written anywhere but -OutputJson. The database is read by one SELECT, in a read-only
  transaction on PostgreSQL and with the ReadOnly application intent on SQL Server; use a login that
  can only read (README.md). psql and sqlcmd run inside the database container, with the credentials
  it already holds; only the password of a SQL Server login other than sa goes through this script,
  from the SQLCMDPASSWORD variable or typed without echo, and never in an argument nor in the output.

.PARAMETER Slug
  The repository, owner/name (owner:namespace/name on a forge with a level under the organization).

.PARAMETER Sha
  The commits to compare, in the order of the report.

.PARAMETER CommitsFile
  A text file with one SHA per line (blank lines and lines starting with # are skipped), instead of -Sha.

.PARAMETER Forge
  The forge of the repository in CodeCoverage: github (default), bitbucket, azure-devops or gitlab.

.PARAMETER CodecovSamplePath
  A folder of frozen answers of the Codecov API v2: {sha}.json for the project and {sha}.{flag}.json
  for a flag. No network call is made.

.PARAMETER CodecovToken
  The API token of Codecov, to call the API v2 itself. Default: the CODECOV_API_TOKEN variable. Never
  an upload token.

.PARAMETER CodecovApiUrl
  The address of the Codecov API. Default: https://api.codecov.io.

.PARAMETER CodecovService
  The service of the repository at Codecov: github (default), bitbucket, gitlab…

.PARAMETER Flags
  Flags to ask Codecov for, besides those CodeCoverage holds for the commit. A flag that neither side
  holds for a commit is not applicable to it: its line is not a gap.

.PARAMETER TotalsPath
  The frozen output of sql/commit-flag-totals.{postgresql,sqlserver}.sql: a JSON array.

.PARAMETER Provider
  PostgreSql or SqlServer: the database of -Container.

.PARAMETER Container
  The name of the running database container: the query runs inside it (docker exec).

.PARAMETER Database
  The name of the database. Default: codecoverage.

.PARAMETER DatabaseUser
  The login of the query. Default: cc on PostgreSQL, sa on SQL Server (its password is read from the
  MSSQL_SA_PASSWORD variable of the container). The password of another SQL Server login is read from
  the SQLCMDPASSWORD variable, or asked without echo when it is not set; docker passes it to sqlcmd by
  the name of the variable.

.PARAMETER Tolerance
  The largest accepted difference of coverage, in percentage points. Default: 0.01.

.PARAMETER RequiredCommits
  The number of commits within the tolerance that meets the criterion. Default: 20.

.PARAMETER OutputJson
  Writes the lines of the report and its summary to this file, as JSON.

.PARAMETER FailOnGap
  Exits with 1 when a line is not within the tolerance or misses one side (a not applicable flag is
  not a gap).

.PARAMETER PassThru
  Returns the lines of the report as objects instead of printing them.

.EXAMPLE
  pwsh tools/parity/Compare-TotalsWithCodecov.ps1 -Slug octo-org/app -CommitsFile tools/parity/samples/commits.txt `
    -CodecovSamplePath tools/parity/samples/codecov -TotalsPath tools/parity/samples/codecoverage/commit-flag-totals.json

.EXAMPLE
  pwsh tools/parity/Compare-TotalsWithCodecov.ps1 -Slug my-org/my-repo -CommitsFile pilot-commits.txt `
    -Provider PostgreSql -Container code-coverage-db-1 -OutputJson parity.json
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory)] [string] $Slug,
  [string[]] $Sha = @(),
  [string] $CommitsFile,
  [ValidateSet('github', 'bitbucket', 'azure-devops', 'gitlab')] [string] $Forge = 'github',
  [string] $CodecovSamplePath,
  [string] $CodecovToken = $env:CODECOV_API_TOKEN,
  [string] $CodecovApiUrl = 'https://api.codecov.io',
  [string] $CodecovService = 'github',
  [string[]] $Flags = @(),
  [string] $TotalsPath,
  [ValidateSet('PostgreSql', 'SqlServer')] [string] $Provider,
  [string] $Container,
  [string] $Database = 'codecoverage',
  [string] $DatabaseUser,
  [decimal] $Tolerance = 0.01,
  [int] $RequiredCommits = 20,
  [string] $OutputJson,
  [switch] $FailOnGap,
  [switch] $PassThru
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$invariant = [System.Globalization.CultureInfo]::InvariantCulture
$projectLabel = '(project)'

function ConvertTo-Number {
  # A JSON number may come back as a long, a double or a decimal: totals are compared as decimals.
  param($Value)

  if ($null -eq $Value) {
    return $null
  }

  return [decimal]::Parse(([System.IConvertible] $Value).ToString($invariant), [System.Globalization.NumberStyles]::Float, $invariant)
}

function Get-FlagFileName {
  # The name of the frozen answer of a flag: what is not a letter, a digit, a dot, a dash or an underscore becomes _.
  param([Parameter(Mandatory)] [string] $CommitSha, [Parameter(Mandatory)] [AllowEmptyString()] [string] $Flag)

  if ($Flag -eq '') {
    return "$CommitSha.json"
  }

  return "$CommitSha.$($Flag -replace '[^A-Za-z0-9._-]', '_').json"
}

function Read-Commits {
  if ($Sha.Count -gt 0 -and $CommitsFile) {
    throw 'Give -Sha or -CommitsFile, not both.'
  }

  $list = if ($CommitsFile) {
    Get-Content -LiteralPath $CommitsFile | ForEach-Object { $_.Trim() } | Where-Object { $_ -and -not $_.StartsWith('#') }
  }
  else {
    $Sha
  }

  $list = @($list | ForEach-Object { $_.ToLowerInvariant() } | Select-Object -Unique)

  if ($list.Count -eq 0) {
    throw 'No commit to compare: give -Sha or -CommitsFile.'
  }

  foreach ($commit in $list) {
    if ($commit -notmatch '^[0-9a-f]{40}$') {
      throw "'$commit' is not a full SHA of 40 hexadecimal characters."
    }
  }

  return $list
}

function Invoke-ReadOnlyQuery {
  # One SELECT, piped to the client of the database inside its container: nothing is installed on the host and
  # no password goes through this process.
  if (-not $Provider) {
    throw 'Give -Provider with -Container.'
  }

  $sqlFile = Join-Path $PSScriptRoot "sql/commit-flag-totals.$($Provider.ToLowerInvariant()).sql"
  $sql = Get-Content -LiteralPath $sqlFile -Raw
  $restorePassword = $false
  $passwordToRestore = $null

  if ($Provider -eq 'PostgreSql') {
    $user = if ($DatabaseUser) { $DatabaseUser } else { 'cc' }
    $arguments = @(
      'exec', '-i', '-e', 'PGOPTIONS=-c default_transaction_read_only=on', $Container,
      'psql', '-U', $user, '-d', $Database, '-At', '-q', '-v', 'ON_ERROR_STOP=1', '-f', '-'
    )
  }
  else {
    $user = if ($DatabaseUser) { $DatabaseUser } else { 'sa' }
    # sqlcmd reads the password from SQLCMDPASSWORD. -y 0: the JSON is not cut at 256 characters; -K ReadOnly:
    # the read-only application intent.
    $command = '/opt/mssql-tools18/bin/sqlcmd -S localhost -C -U "$1" -d "$2" -K ReadOnly -b -y 0 -i /dev/stdin'

    if ($user -eq 'sa') {
      # The password of sa is the MSSQL_SA_PASSWORD variable of the container, read inside it.
      $arguments = @('exec', '-i', $Container, 'bash', '-c', "SQLCMDPASSWORD=`"`$MSSQL_SA_PASSWORD`" $command", 'sqlcmd', $user, $Database)
    }
    else {
      # The password of another login comes from SQLCMDPASSWORD, or is typed without echo: docker passes the
      # variable by its name only, so the password is neither in the arguments nor in the output.
      $passwordToRestore = $env:SQLCMDPASSWORD
      $restorePassword = $true

      if (-not $env:SQLCMDPASSWORD) {
        try {
          $typed = Read-Host -AsSecureString -Prompt "Password of the SQL Server login '$user'"
        }
        catch {
          throw "Give the password of the login '$user' in the SQLCMDPASSWORD variable, or run the script in a console to type it."
        }

        $env:SQLCMDPASSWORD = [System.Net.NetworkCredential]::new('', $typed).Password
      }

      $arguments = @('exec', '-i', '-e', 'SQLCMDPASSWORD', $Container, 'bash', '-c', $command, 'sqlcmd', $user, $Database)
    }

    $sql = "SET NOCOUNT ON;`n$sql"
  }

  try {
    $output = $sql | & docker @arguments
  }
  finally {
    if ($restorePassword) {
      $env:SQLCMDPASSWORD = $passwordToRestore
    }
  }

  if ($LASTEXITCODE -ne 0) {
    throw "The query failed in the container '$Container' (exit code $LASTEXITCODE)."
  }

  # sqlcmd cuts a long value over several lines: the JSON holds no line break of its own.
  return (@($output) -join '').Trim()
}

function Read-LocalTotals {
  $json = if ($TotalsPath) {
    Get-Content -LiteralPath $TotalsPath -Raw
  }
  elseif ($Container) {
    Invoke-ReadOnlyQuery
  }
  else {
    throw 'Give -TotalsPath (frozen output of the query) or -Container with -Provider (database of a compose stack).'
  }

  $rows = @($json | ConvertFrom-Json)
  $forgeName = $Forge.Replace('-', '')
  # Ordinal, unlike @{}: Backend and backend are two flags, as in CommitFlagTotals.
  $totals = [System.Collections.Generic.Dictionary[string, object]]::new([System.StringComparer]::Ordinal)

  foreach ($row in $rows) {
    $owner = if ($row.namespace) { "$($row.owner):$($row.namespace)" } else { "$($row.owner)" }

    if ("$owner/$($row.name)" -ne $Slug -or "$($row.forge)" -ne $forgeName) {
      # -ne ignores the case, like the logins of a forge.
      continue
    }

    $totals["$("$($row.sha)".ToLowerInvariant())|$($row.flag)"] = [pscustomobject]@{
      Lines            = [int] $row.lines
      Hits             = [int] $row.hits
      Misses           = [int] $row.misses
      Partials         = [int] $row.partials
      Coverage         = ConvertTo-Number $row.coverage
      IsCarriedForward = [bool] $row.isCarriedForward
    }
  }

  return $totals
}

function ConvertTo-CodecovTotals {
  param($Answer)

  if ($null -eq $Answer -or -not ($Answer.PSObject.Properties.Name -contains 'totals') -or $null -eq $Answer.totals) {
    return $null
  }

  $totals = $Answer.totals

  return [pscustomobject]@{
    Lines    = [int] $totals.lines
    Hits     = [int] $totals.hits
    Misses   = [int] $totals.misses
    Partials = [int] $totals.partials
    Coverage = ConvertTo-Number $totals.coverage
  }
}

function Read-CodecovTotals {
  param([Parameter(Mandatory)] [string] $CommitSha, [Parameter(Mandatory)] [AllowEmptyString()] [string] $Flag)

  if ($CodecovSamplePath) {
    $file = Join-Path $CodecovSamplePath (Get-FlagFileName -CommitSha $CommitSha -Flag $Flag)

    if (-not (Test-Path -LiteralPath $file)) {
      return $null
    }

    return ConvertTo-CodecovTotals (Get-Content -LiteralPath $file -Raw | ConvertFrom-Json)
  }

  if (-not $CodecovToken) {
    throw 'Give -CodecovSamplePath (frozen answers) or -CodecovToken (API v2 of Codecov).'
  }

  $owner, $name = $Slug.Split('/', 2)
  $url = "$($CodecovApiUrl.TrimEnd('/'))/api/v2/$CodecovService/$([uri]::EscapeDataString($owner))/repos/$([uri]::EscapeDataString($name))/report/?sha=$CommitSha"

  if ($Flag -ne '') {
    $url += "&flag=$([uri]::EscapeDataString($Flag))"
  }

  $response = Invoke-WebRequest -Uri $url -Headers @{ Authorization = "Bearer $CodecovToken"; Accept = 'application/json' } -SkipHttpErrorCheck -TimeoutSec 60

  if ($response.StatusCode -eq 404) {
    return $null
  }

  if ($response.StatusCode -ne 200) {
    throw "Codecov answered HTTP $($response.StatusCode) for $CommitSha$(if ($Flag) { " (flag $Flag)" })."
  }

  return ConvertTo-CodecovTotals ($response.Content | ConvertFrom-Json)
}

function Get-SampleFlags {
  # The flags Codecov holds for a commit, read from the names of the frozen answers.
  param([Parameter(Mandatory)] [string] $CommitSha)

  if (-not $CodecovSamplePath) {
    return @()
  }

  return @(
    Get-ChildItem -LiteralPath $CodecovSamplePath -Filter "$CommitSha.*.json" -File |
      ForEach-Object { $_.BaseName.Substring($CommitSha.Length + 1) }
  )
}

function Format-Coverage {
  param($Value)

  if ($null -eq $Value) {
    return '-'
  }

  return ([decimal] $Value).ToString('0.00000', $invariant)
}

function Compare-Totals {
  param(
    [Parameter(Mandatory)] [string] $CommitSha,
    [Parameter(Mandatory)] [AllowEmptyString()] [string] $Flag,
    $Codecov,
    $Local
  )

  $delta = $null
  $status = 'ok'

  if ($null -eq $Codecov -and $null -eq $Local) {
    # A flag neither side holds for the commit (asked with -Flags) is not one of its flags.
    $status = if ($Flag -eq '') { 'missing-both' } else { 'not-applicable' }
  }
  elseif ($null -eq $Codecov) {
    $status = 'missing-codecov'
  }
  elseif ($null -eq $Local) {
    $status = 'missing-codecoverage'
  }
  elseif ($null -eq $Codecov.Coverage -or $null -eq $Local.Coverage) {
    # No coverable line on one side: equal only when the other side has none either.
    if ($null -ne $Codecov.Coverage -or $null -ne $Local.Coverage) {
      $status = 'gap'
    }
  }
  else {
    $delta = $Local.Coverage - $Codecov.Coverage

    if ([Math]::Abs($delta) -gt $Tolerance) {
      $status = 'gap'
    }
  }

  $sameCounts = $null -ne $Codecov -and $null -ne $Local -and
                $Codecov.Lines -eq $Local.Lines -and $Codecov.Hits -eq $Local.Hits -and
                $Codecov.Misses -eq $Local.Misses -and $Codecov.Partials -eq $Local.Partials

  return [pscustomobject]@{
    Sha                  = $CommitSha
    Flag                 = $Flag
    Status               = $status
    CodecovCoverage      = if ($Codecov) { $Codecov.Coverage } else { $null }
    CodeCoverageCoverage = if ($Local) { $Local.Coverage } else { $null }
    Delta                = $delta
    CodecovLines         = if ($Codecov) { $Codecov.Lines } else { $null }
    CodeCoverageLines    = if ($Local) { $Local.Lines } else { $null }
    CodecovHits          = if ($Codecov) { $Codecov.Hits } else { $null }
    CodeCoverageHits     = if ($Local) { $Local.Hits } else { $null }
    SameCounts           = $sameCounts
    IsCarriedForward     = if ($Local) { $Local.IsCarriedForward } else { $false }
  }
}

$commits = @(Read-Commits)
$local = Read-LocalTotals
$rows = [System.Collections.Generic.List[object]]::new()

foreach ($commit in $commits) {
  $localFlags = @($local.Keys | Where-Object { $_.StartsWith("$commit|") } | ForEach-Object { $_.Substring($commit.Length + 1) })
  $flagNames = @(@($localFlags) + @($Flags) + @(Get-SampleFlags -CommitSha $commit) | Where-Object { $_ -ne '' } | Sort-Object -Unique -CaseSensitive)

  # The project first, then the flags in ordinal order.
  foreach ($flag in @('') + $flagNames) {
    $key = "$commit|$flag"
    $localTotals = if ($local.ContainsKey($key)) { $local[$key] } else { $null }

    $rows.Add((Compare-Totals -CommitSha $commit -Flag $flag -Codecov (Read-CodecovTotals -CommitSha $commit -Flag $flag) -Local $localTotals))
  }
}

$accepted = @('ok', 'not-applicable')
$gaps = @($rows | Where-Object { $_.Status -notin $accepted })
$compared = @($commits | Where-Object { $sha = $_; @($rows | Where-Object { $_.Sha -eq $sha -and $_.Flag -eq '' -and $_.Status -notlike 'missing-*' }).Count -gt 0 })
$within = @($compared | Where-Object { $sha = $_; @($rows | Where-Object { $_.Sha -eq $sha -and $_.Status -notin $accepted }).Count -eq 0 })
$met = $within.Count -ge $RequiredCommits

$summary = [pscustomobject]@{
  Slug                   = $Slug
  Tolerance              = $Tolerance
  Commits                = $commits.Count
  CommitsCompared        = $compared.Count
  CommitsWithinTolerance = $within.Count
  RequiredCommits        = $RequiredCommits
  CriterionMet           = $met
  Gaps                   = $gaps.Count
}

if ($OutputJson) {
  [pscustomobject]@{ summary = $summary; rows = $rows } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $OutputJson -Encoding utf8NoBOM
}

if ($PassThru) {
  $rows
}
else {
  $table = $rows | ForEach-Object {
    [pscustomobject]@{
      Commit       = $_.Sha.Substring(0, 10)
      Flag         = if ($_.Flag -eq '') { $projectLabel } else { $_.Flag }
      Codecov      = Format-Coverage $_.CodecovCoverage
      CodeCoverage = Format-Coverage $_.CodeCoverageCoverage
      Delta        = if ($null -eq $_.Delta) { '-' } else { ([decimal] $_.Delta).ToString('+0.00000;-0.00000;0.00000', $invariant) }
      Lines        = "$(if ($null -eq $_.CodecovLines) { '-' } else { $_.CodecovLines })/$(if ($null -eq $_.CodeCoverageLines) { '-' } else { $_.CodeCoverageLines })"
      Counts       = if ($_.Status -like 'missing-*' -or $_.Status -eq 'not-applicable') { '-' } elseif ($_.SameCounts) { 'same' } else { 'differ' }
      Carried      = if ($_.IsCarriedForward) { 'yes' } else { '' }
      Status       = $_.Status
    }
  }

  Write-Output "Totals of $Slug, Codecov against CodeCoverage (coverage in %, lines as Codecov/CodeCoverage):"
  Write-Output (($table | Format-Table -AutoSize | Out-String -Width 200).TrimEnd())
  Write-Output ''

  if ($gaps.Count -eq 0) {
    Write-Output "No gap beyond $($Tolerance.ToString($invariant)) point."
  }
  else {
    Write-Output "Gaps beyond $($Tolerance.ToString($invariant)) point, or totals missing on one side ($($gaps.Count)):"

    foreach ($gap in $gaps) {
      $flagName = if ($gap.Flag -eq '') { $projectLabel } else { $gap.Flag }
      $detail = if ($null -ne $gap.Delta) { "delta $(([decimal] $gap.Delta).ToString('+0.00000;-0.00000;0.00000', $invariant))" } else { $gap.Status }

      Write-Output "  $($gap.Sha) $flagName`: $detail"
    }
  }

  Write-Output ''
  Write-Output "$($within.Count) of $($compared.Count) compared commits within the tolerance ($($commits.Count) asked)."
  Write-Output "Exit criterion of M1 ($RequiredCommits commits within the tolerance): $(if ($met) { 'met' } else { 'not met' })."
}

if ($FailOnGap -and $gaps.Count -gt 0) {
  exit 1
}
