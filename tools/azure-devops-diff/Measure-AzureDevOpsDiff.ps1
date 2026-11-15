#Requires -Version 7.2
<#
.SYNOPSIS
  Measures, on real pull requests of an Azure DevOps test organization, three ways of getting the
  changed lines of a pull request, against git diff -U0 on the same SHAs (spike M7.01). A dry run by
  default.

.DESCRIPTION
  For each pull request: its head (lastMergeSourceCommit, never the merge commit), its last
  iteration and the base the API gives (commonCommit of Diffs - Get), then:
    - path A, Diffs - Get with diffCommonCommit=true: the changed files, paged with $top/$skip, and
      allChangesIncluded; no lines (the API has none);
    - path B, FileDiffs (preview): the lineDiffBlocks of each file, in batches;
    - path C, Blobs - Get of originalObjectId and objectId, diffed locally by the port of git's
      xdiff of AzureDevOpsDiff.psm1 (with and without the indent heuristic);
  and compares the lines of B and C with git diff -U0 -M run in a local clone of the same
  repository, between the same base and head. Every request records its time, its size and the
  X-RateLimit-* headers (TSTU), which Azure DevOps sends only when they apply.

  It also gathers the evidence of the four side questions: comment ids of the threads (read only, or
  a probe thread with -ProbeThreadPull), the organization id of _apis/connectionData, the
  pullrequestquery of the head commit, and the membership of Project Collection Administrators read
  with the token of the run.

  The token is read from the environment variable named by -TokenVariable (AZURE_DEVOPS_TOKEN),
  set by the person who runs the script; it is never printed nor written. The report names no
  organization, project, repository, person, e-mail, SHA nor file path (paths become short hashes).
  Fixtures are written only with -FixturePath, scrubbed, and refused when anything forbidden is left.

.PARAMETER Organization
  The Azure DevOps organization (dev.azure.com/<organization>).

.PARAMETER Project
  The project of the repository.

.PARAMETER Repository
  The repository, by name or id.

.PARAMETER ClonePath
  A local clone of the same repository, for the git diff reference. Missing commits are fetched from
  -Remote, with the git credentials of the person who runs the script.

.PARAMETER Remote
  The remote of the clone that points to the Azure DevOps repository (origin).

.PARAMETER Pulls
  The pull request ids to measure. Without them, the -Count most recent ones of -Status.

.PARAMETER Count
  How many pull requests are measured when none is given (10, the minimum of the acceptance).

.PARAMETER Status
  Which pull requests are listed without -Pulls: active (default), completed or all.

.PARAMETER PageSize
  The $top of the paged lists (100, the default of the API, so that a pull request of more than 100
  files exercises the pagination).

.PARAMETER FileDiffBatchSize
  How many files one FileDiffs request carries (50).

.PARAMETER MaxFileBytes
  Above this size a blob is not diffed locally: its patch is unknown (1 MiB).

.PARAMETER TokenVariable
  The environment variable that holds the token (AZURE_DEVOPS_TOKEN).

.PARAMETER TokenKind
  Pat (a personal access token, sent as Basic) or Bearer (a Microsoft Entra access token, for
  example the one of the application's service principal).

.PARAMETER OutputPath
  The folder of the report (a new folder under the temporary folder by default).

.PARAMETER FixturePath
  Where the fixtures of M7.46 are written, tests/fixtures/azure-devops for instance. Nothing is
  written without it.

.PARAMETER FixturePulls
  The pull requests whose fixtures are written: the first gives diffs/*.json, each one gives
  patches/pr-<id>.diff.

.PARAMETER ProbeThreadPull
  A pull request of the test organization where two probe threads are created, their first comment
  updated, then deleted: the only write of the script. Needs a token allowed to write threads.

.PARAMETER ServiceRoot
  For the offline self-test only: a local address that stands for dev.azure.com (<root>/dev),
  vssps.dev.azure.com (<root>/vssps) and app.vssps.visualstudio.com (<root>/app).

.PARAMETER Execute
  Calls Azure DevOps. Without it, the script prints the calls it would make and reads nothing.

.EXAMPLE
  $env:AZURE_DEVOPS_TOKEN = '<a PAT with Code (Read) and Graph (Read)>'
  pwsh tools/azure-devops-diff/Measure-AzureDevOpsDiff.ps1 -Organization my-test-org -Project Spike -Repository code-coverage -ClonePath ../spike-clone -Execute
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory)] [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9-]{0,49}$')] [string] $Organization,
  [Parameter(Mandatory)] [string] $Project,
  [Parameter(Mandatory)] [string] $Repository,
  [string] $ClonePath,
  [string] $Remote = 'origin',
  [int[]] $Pulls = @(),
  [ValidateRange(1, 100)] [int] $Count = 10,
  [ValidateSet('active', 'completed', 'all')] [string] $Status = 'active',
  [ValidateRange(1, 2000)] [int] $PageSize = 100,
  [ValidateRange(1, 1000)] [int] $FileDiffBatchSize = 50,
  [ValidateRange(1, 104857600)] [long] $MaxFileBytes = 1048576,
  [string] $TokenVariable = 'AZURE_DEVOPS_TOKEN',
  [ValidateSet('Pat', 'Bearer')] [string] $TokenKind = 'Pat',
  [string] $OutputPath,
  [string] $FixturePath,
  [int[]] $FixturePulls = @(),
  [int] $ProbeThreadPull = 0,
  [ValidatePattern('^http://(localhost|127\.0\.0\.1):\d+$')] [string] $ServiceRoot,
  [switch] $Execute
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'AzureDevOpsDiff.psm1') -Force

$orgSegment = [Uri]::EscapeDataString($Organization)
$projectSegment = [Uri]::EscapeDataString($Project)
$repoSegment = [Uri]::EscapeDataString($Repository)
$orgUrl = if ($ServiceRoot) { "$ServiceRoot/dev/$orgSegment" } else { "https://dev.azure.com/$orgSegment" }
$graphUrl = if ($ServiceRoot) { "$ServiceRoot/vssps/$orgSegment" } else { "https://vssps.dev.azure.com/$orgSegment" }
$appUrl = if ($ServiceRoot) { "$ServiceRoot/app" } else { 'https://app.vssps.visualstudio.com' }
$repoUrl = "$orgUrl/$projectSegment/_apis/git/repositories/$repoSegment"
$fileDiffsLocation = 'c4c5a7e6-e9f3-4730-a92b-84baacff694b'

if (-not $Execute) {
  $calls = @(
    "GET  $orgUrl/_apis/connectionData",
    "OPTIONS $orgUrl/_apis/git (route of FileDiffs, location $fileDiffsLocation)",
    "GET  $repoUrl/pullrequests?searchCriteria.status=$Status (unless -Pulls)",
    "GET  $repoUrl/pullRequests/{id}, /iterations, /iterations/{last}/changes?`$top=$PageSize&`$skip=..",
    "GET  $repoUrl/diffs/commits?baseVersion={target}&targetVersion={head}&diffCommonCommit=true&`$top=$PageSize&`$skip=..  (path A)",
    "POST $repoUrl/FileDiffs?api-version=7.1-preview.1 in batches of $FileDiffBatchSize  (path B)",
    "GET  $repoUrl/blobs/{objectId}?`$format=octetstream for both sides of each file  (path C)",
    "GET  $repoUrl/pullRequests/{id}/threads; POST $repoUrl/pullrequestquery (active pull requests)",
    "GET  $graphUrl/_apis/graph/groups, /graph/descriptors/{id}; HEAD /graph/memberships/{subject}/{group}",
    'git  diff -U0 -M {base} {head} in -ClonePath (the reference)')
  if ($ProbeThreadPull -gt 0) { $calls += "POST/PATCH/DELETE $repoUrl/pullRequests/$ProbeThreadPull/threads (two probe threads, then deleted)" }
  $calls | ForEach-Object { Write-Output "dry run: $_" }
  Write-Output "dry run: nothing read (add -Execute and -ClonePath, with `$env:$TokenVariable set)."
  exit 0
}

$token = [Environment]::GetEnvironmentVariable($TokenVariable)
if ([string]::IsNullOrWhiteSpace($token)) { throw "Set the environment variable $TokenVariable (never on the command line, never in a file of the repository)." }
if (-not $ClonePath -or -not (Test-Path -LiteralPath (Join-Path $ClonePath '.git'))) { throw '-ClonePath must be a local clone of the same repository.' }
$ClonePath = (Resolve-Path -LiteralPath $ClonePath).Path
if (-not $OutputPath) { $OutputPath = Join-Path ([System.IO.Path]::GetTempPath()) "azure-devops-diff-$(Get-Date -Format 'yyyyMMdd-HHmmss')" }
New-Item -ItemType Directory -Force -Path $OutputPath | Out-Null

$authorization = if ($TokenKind -eq 'Pat') { 'Basic ' + [Convert]::ToBase64String([System.Text.Encoding]::ASCII.GetBytes(":$token")) } else { "Bearer $token" }
$http = [System.Net.Http.HttpClient]::new()
$http.Timeout = [TimeSpan]::FromMinutes(5)

# Strings that must never reach a written file. Values are added as they are learnt.
$forbidden = @{ organization = @($Organization); project = @($Project); repository = @($Repository); token = @($token); identity = @() }
$stats = [ordered]@{}
$objectIdFallbacks = 0

function Get-Field {
  # Reads a dotted property path of a JSON object, $null when any part is missing (StrictMode-safe).
  param($Object, [string] $Path)

  $current = $Object
  foreach ($part in $Path.Split('.')) {
    if ($null -eq $current) { return $null }
    $property = $current.PSObject.Properties[$part]
    if ($null -eq $property) { return $null }
    $current = $property.Value
  }
  return $current
}

function Add-Identity {
  # Remembers the names and e-mails of a person or of the application, so that they never get written.
  param($Identity)

  foreach ($field in @('displayName', 'uniqueName', 'providerDisplayName', 'mailAddress')) {
    $value = Get-Field $Identity $field
    if ($value -is [string] -and $value.Length -ge 3) { $script:forbidden.identity = @($script:forbidden.identity + $value | Select-Object -Unique) }
  }
}

function Invoke-Ado {
  <#
    One call to Azure DevOps, with its cost recorded under -Category. A 429 or a Retry-After is
    honoured (three tries). Returns { Status; Bytes; Json; Headers }.
  #>
  param(
    [string] $Method = 'GET',
    [string] $Uri,
    $Body,
    [string] $Category,
    [string] $Accept = 'application/json')

  if (-not $script:stats.Contains($Category)) {
    $script:stats[$Category] = [ordered]@{ requests = 0; bytes = 0; milliseconds = 0; costSum = 0.0; costHeaders = 0; minRemaining = $null; limit = $null; delaySeconds = 0.0; throttled = 0; failures = 0 }
  }
  $entry = $script:stats[$Category]

  for ($attempt = 1; ; $attempt++) {
    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::new($Method), $Uri)
    $request.Headers.TryAddWithoutValidation('Authorization', $authorization) | Out-Null
    $request.Headers.TryAddWithoutValidation('Accept', $Accept) | Out-Null
    if ($null -ne $Body) {
      $request.Content = [System.Net.Http.StringContent]::new(($Body | ConvertTo-Json -Depth 20 -Compress), [System.Text.Encoding]::UTF8, 'application/json')
    }
    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    $response = $http.SendAsync($request).GetAwaiter().GetResult()
    $bytes = $response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
    $watch.Stop()

    $headers = @{}
    foreach ($pair in @($response.Headers) + @($response.Content.Headers)) { $headers[$pair.Key.ToLowerInvariant()] = ($pair.Value -join ',') }
    $entry.requests++
    $entry.bytes += $bytes.Length
    $entry.milliseconds += $watch.ElapsedMilliseconds
    if ($headers.ContainsKey('x-ratelimit-cost')) { $entry.costSum += [double]::Parse($headers['x-ratelimit-cost'], [Globalization.CultureInfo]::InvariantCulture); $entry.costHeaders++ }
    if ($headers.ContainsKey('x-ratelimit-remaining')) {
      $remaining = [double]::Parse($headers['x-ratelimit-remaining'], [Globalization.CultureInfo]::InvariantCulture)
      if ($null -eq $entry.minRemaining -or $remaining -lt $entry.minRemaining) { $entry.minRemaining = $remaining }
    }
    if ($headers.ContainsKey('x-ratelimit-limit')) { $entry.limit = $headers['x-ratelimit-limit'] }
    if ($headers.ContainsKey('x-ratelimit-delay')) { $entry.delaySeconds += [double]::Parse($headers['x-ratelimit-delay'], [Globalization.CultureInfo]::InvariantCulture) }

    $status = [int] $response.StatusCode
    if (($status -eq 429 -or $headers.ContainsKey('retry-after')) -and $attempt -lt 3) {
      $entry.throttled++
      $wait = 5
      if ($headers.ContainsKey('retry-after')) { [int]::TryParse($headers['retry-after'], [ref] $wait) | Out-Null }
      if ($status -eq 429) { Start-Sleep -Seconds ([Math]::Min([Math]::Max($wait, 1), 60)); continue }
    }
    if ($status -ge 400) { $entry.failures++ }

    $json = $null
    if ($bytes.Length -gt 0 -and "$($response.Content.Headers.ContentType)" -match 'json') {
      try { $json = ConvertFrom-Json -InputObject ([System.Text.Encoding]::UTF8.GetString($bytes)) -Depth 64 -NoEnumerate } catch { $json = $null }
    }
    return [pscustomobject]@{ Status = $status; Bytes = $bytes; Json = $json; Headers = $headers }
  }
}

function Invoke-Git {
  # Runs git in the clone; returns its exit code (output discarded unless -Capture).
  param([string[]] $Arguments, [switch] $Capture)

  $output = & git -C $ClonePath @Arguments 2>$null
  if ($Capture) { return [pscustomobject]@{ Code = $LASTEXITCODE; Output = ($output -join "`n").Trim() } }
  return $LASTEXITCODE
}

function Confirm-Commit {
  # Makes sure a commit is in the clone, fetching it (or the merge ref of the pull request) if needed.
  param([string] $Sha, [int] $PullId)

  if ((Invoke-Git @('cat-file', '-e', "$Sha^{commit}")) -eq 0) { return $true }
  Invoke-Git @('fetch', '--quiet', '--no-tags', $Remote, $Sha) | Out-Null
  if ((Invoke-Git @('cat-file', '-e', "$Sha^{commit}")) -eq 0) { return $true }
  Invoke-Git @('fetch', '--quiet', '--no-tags', $Remote, "+refs/pull/$PullId/merge:refs/spike/pull/$PullId") | Out-Null
  return ((Invoke-Git @('cat-file', '-e', "$Sha^{commit}")) -eq 0)
}

function Get-GitReference {
  # git diff -U0 between base and head, default options, written to a file (exact bytes) and read.
  param([string] $Base, [string] $Head, [string] $File, [switch] $NoIndentHeuristic)

  $options = @('-c', 'core.quotePath=false', '-c', 'diff.noprefix=false', '-c', 'diff.mnemonicPrefix=false', '-c', 'diff.algorithm=default',
    '-c', 'diff.renames=true', 'diff', '-U0', '--no-color', '--no-ext-diff', '--no-textconv', '-M', '--src-prefix=a/', '--dst-prefix=b/', "--output=$File")
  $options += if ($NoIndentHeuristic) { '--no-indent-heuristic' } else { '--indent-heuristic' }
  if ((Invoke-Git ($options + @($Base, $Head))) -ne 0) { return $null }
  return [System.IO.File]::ReadAllText($File, [System.Text.Encoding]::UTF8)
}

function Get-Blob {
  # Path C: one blob by its object id, as bytes. $null when it cannot be read.
  param([string] $ObjectId)

  if ([string]::IsNullOrEmpty($ObjectId) -or $ObjectId -match '^0+$') { return , [byte[]] @() }
  $answer = Invoke-Ado -Uri "$repoUrl/blobs/$ObjectId`?`$format=octetstream&api-version=7.1" -Category 'C-blobs' -Accept 'application/octet-stream'
  if ($answer.Status -ne 200) { return $null }
  return , $answer.Bytes
}

function Get-ItemBytes {
  # Path C fallback when Diffs - Get gives no object id: the file by path at a commit.
  param([string] $Path, [string] $Commit)

  $script:objectIdFallbacks++
  $answer = Invoke-Ado -Uri "$repoUrl/items?path=$([Uri]::EscapeDataString("/$Path"))&versionDescriptor.versionType=commit&versionDescriptor.version=$Commit&`$format=octetStream&api-version=7.1" -Category 'C-blobs' -Accept 'application/octet-stream'
  if ($answer.Status -ne 200) { return $null }
  return , $answer.Bytes
}

function Resolve-FileDiffsUrl {
  # The route of FileDiffs, read from the location service of the git area (fallback: the known form).
  $answer = Invoke-Ado -Method 'OPTIONS' -Uri "$orgUrl/_apis/git" -Category 'common'
  $location = @(Get-Field $answer.Json 'value') | Where-Object { $_ -and (Get-Field $_ 'id') -eq $fileDiffsLocation } | Select-Object -First 1
  if ($location -and "$(Get-Field $location 'routeTemplate')" -match '^\{project\}/_apis/') {
    $route = (Get-Field $location 'routeTemplate') -replace '\{project\}', $projectSegment -replace '\{area\}', 'git' -replace '\{resource\}', (Get-Field $location 'resourceName') -replace '\{repositoryId\}', $repoSegment
    return [pscustomobject]@{ Url = "$orgUrl/$route"; Source = 'location service'; ReleasedVersion = (Get-Field $location 'releasedVersion'); MaxVersion = (Get-Field $location 'maxVersion') }
  }
  return [pscustomobject]@{ Url = "$repoUrl/FileDiffs"; Source = 'fallback'; ReleasedVersion = $null; MaxVersion = $null }
}

function Get-Pages {
  # Reads every page of a list paged with $top/$skip. -Items names the array, -Next reads nextSkip.
  param([string] $Uri, [string] $Items, [string] $Category, [switch] $UseNextSkip)

  $pages = [System.Collections.Generic.List[object]]::new()
  $skip = 0
  while ($true) {
    $separator = if ($Uri.Contains('?')) { '&' } else { '?' }
    $answer = Invoke-Ado -Uri "$Uri$separator`$top=$PageSize&`$skip=$skip" -Category $Category
    if ($answer.Status -ne 200) { return [pscustomobject]@{ Pages = $pages; Error = $answer.Status } }
    $pages.Add($answer.Json)
    $count = @(Get-Field $answer.Json $Items).Count
    if ($UseNextSkip) {
      $next = Get-Field $answer.Json 'nextSkip'
      if (-not $next) { break }
      $skip = [int] $next
    }
    else {
      if ($count -lt $PageSize) { break }
      $skip += $count
    }
    if ($pages.Count -ge 200) { break }
  }
  return [pscustomobject]@{ Pages = $pages; Error = $null }
}

function ConvertTo-RepoPath([string] $Path) { if ($null -eq $Path) { return $null }; return $Path.TrimStart('/') }

# ------------------------------------------------------------------------------------------------
# Organization-level evidence (question 2) and the route of FileDiffs.
# ------------------------------------------------------------------------------------------------
$side = [ordered]@{}
$connection = Invoke-Ado -Uri "$orgUrl/_apis/connectionData" -Category 'common'
$instanceId = Get-Field $connection.Json 'instanceId'
$userId = Get-Field $connection.Json 'authenticatedUser.id'
Add-Identity (Get-Field $connection.Json 'authenticatedUser')
$second = Invoke-Ado -Uri "$orgUrl/_apis/connectionData" -Category 'common'
$side.organizationId = [ordered]@{
  connectionDataStatus = $connection.Status
  instanceIdIsGuid = [bool] ("$instanceId" -match '^[0-9a-fA-F-]{36}$')
  instanceIdStableAcrossCalls = ($instanceId -and $instanceId -eq (Get-Field $second.Json 'instanceId'))
  deploymentType = Get-Field $connection.Json 'deploymentType'
  authenticatedUserIdIsGuid = [bool] ("$userId" -match '^[0-9a-fA-F-]{36}$')
  accountIdEqualsInstanceId = $null
  accountLookupStatus = $null
}
# The documented organization id: Accounts - List for the member of the token (a PAT of a person).
$me = Invoke-Ado -Uri "$appUrl/_apis/profile/profiles/me?api-version=7.1" -Category 'common'
$side.organizationId.accountLookupStatus = $me.Status
if ($me.Status -eq 200) {
  $accounts = Invoke-Ado -Uri "$appUrl/_apis/accounts?memberId=$(Get-Field $me.Json 'id')&api-version=7.1" -Category 'common'
  $account = @(Get-Field $accounts.Json 'value') | Where-Object { $_ -and "$(Get-Field $_ 'accountName')" -ieq $Organization } | Select-Object -First 1
  if ($account) { $side.organizationId.accountIdEqualsInstanceId = ("$(Get-Field $account 'accountId')" -ieq "$instanceId") }
  $side.organizationId.accountLookupStatus = $accounts.Status
}

$fileDiffs = Resolve-FileDiffsUrl
$repositoryAnswer = Invoke-Ado -Uri "$repoUrl`?api-version=7.1" -Category 'common'
if ($repositoryAnswer.Status -ne 200) { throw "The repository could not be read (HTTP $($repositoryAnswer.Status))." }
$forbidden.repository = @($Repository, (Get-Field $repositoryAnswer.Json 'name')) | Where-Object { $_ } | Select-Object -Unique

# ------------------------------------------------------------------------------------------------
# The pull requests.
# ------------------------------------------------------------------------------------------------
if ($Pulls.Count -eq 0) {
  $list = Invoke-Ado -Uri "$repoUrl/pullrequests?searchCriteria.status=$Status&`$top=$Count&api-version=7.1" -Category 'common'
  $Pulls = @(Get-Field $list.Json 'value' | ForEach-Object { [int] (Get-Field $_ 'pullRequestId') })
}
if ($Pulls.Count -eq 0) { throw 'No pull request to measure.' }

$results = [System.Collections.Generic.List[object]]::new()
$threadEvidence = [System.Collections.Generic.List[object]]::new()
$queryEvidence = [System.Collections.Generic.List[object]]::new()
$work = Join-Path $OutputPath 'work'
New-Item -ItemType Directory -Force -Path $work | Out-Null

foreach ($pullId in $Pulls) {
  Write-Host "pull request $pullId"
  $row = [ordered]@{ pull = $pullId; outcome = $null }
  $pull = Invoke-Ado -Uri "$repoUrl/pullRequests/$pullId`?api-version=7.1" -Category 'common'
  if ($pull.Status -ne 200) { $row.outcome = "pull request not readable (HTTP $($pull.Status))"; $results.Add($row); continue }
  Add-Identity (Get-Field $pull.Json 'createdBy')
  $head = Get-Field $pull.Json 'lastMergeSourceCommit.commitId'
  $target = Get-Field $pull.Json 'lastMergeTargetCommit.commitId'
  $row.status = Get-Field $pull.Json 'status'

  # Iterations: the last one, its commonRefCommit, and its changes (paged with nextSkip).
  $iterations = Invoke-Ado -Uri "$repoUrl/pullRequests/$pullId/iterations?api-version=7.1" -Category 'common'
  $last = @(Get-Field $iterations.Json 'value') | Sort-Object { [int] (Get-Field $_ 'id') } | Select-Object -Last 1
  $commonRef = Get-Field $last 'commonRefCommit.commitId'
  $row.iterations = @(Get-Field $iterations.Json 'value').Count
  $row.lastIterationHeadIsPullHead = ((Get-Field $last 'sourceRefCommit.commitId') -eq $head)
  $iterationChanges = Get-Pages -Uri "$repoUrl/pullRequests/$pullId/iterations/$(Get-Field $last 'id')/changes?api-version=7.1" -Items 'changeEntries' -Category 'common' -UseNextSkip
  $iterationFiles = @($iterationChanges.Pages | ForEach-Object { @(Get-Field $_ 'changeEntries') } | Where-Object { $_ -and -not (Get-Field $_ 'item.isFolder') })
  $row.iterationChangePages = $iterationChanges.Pages.Count
  $row.iterationFiles = $iterationFiles.Count

  # Path A: Diffs - Get between the target and the head, from their common commit.
  $diffs = Get-Pages -Uri "$repoUrl/diffs/commits?baseVersion=$target&baseVersionType=commit&targetVersion=$head&targetVersionType=commit&diffCommonCommit=true&api-version=7.1" -Items 'changes' -Category 'A-diffs'
  $base = Get-Field ($diffs.Pages | Select-Object -First 1) 'commonCommit'
  $aChanges = @($diffs.Pages | ForEach-Object { @(Get-Field $_ 'changes') } | Where-Object { $_ -and (Get-Field $_ 'item.gitObjectType') -eq 'blob' })
  $row.aPages = $diffs.Pages.Count
  $row.aAllChangesIncluded = @($diffs.Pages | ForEach-Object { Get-Field $_ 'allChangesIncluded' })
  $row.aFiles = $aChanges.Count
  $row.baseEqualsIterationCommonRef = ($base -eq $commonRef)
  if (-not $base -or -not $head) { $row.outcome = 'no base or head from the API'; $results.Add($row); continue }

  # The reference: git diff -U0 -M in the clone, between the same base and head.
  $haveCommits = (Confirm-Commit $base $pullId) -and (Confirm-Commit $head $pullId) -and (-not $target -or (Confirm-Commit $target $pullId))
  if (-not $haveCommits) { $row.outcome = 'reference unavailable: commits not in the clone'; $results.Add($row); continue }
  $mergeBase = (Invoke-Git @('merge-base', $target, $head) -Capture).Output
  $row.gitMergeBaseEqualsApiBase = ($mergeBase -eq $base)
  $referenceFile = Join-Path $work "pr-$pullId.diff"
  $referenceText = Get-GitReference $base $head $referenceFile
  $referenceNoIndent = Get-GitReference $base $head (Join-Path $work "pr-$pullId.noindent.diff") -NoIndentHeuristic
  if ($null -eq $referenceText) { $row.outcome = 'git diff failed'; $results.Add($row); continue }
  $reference = ConvertFrom-GitDiff $referenceText
  $referenceAlt = ConvertFrom-GitDiff $referenceNoIndent
  $row.gitFiles = $reference.Count
  $row.gitRenames = @($reference.Values | Where-Object { $_.Status -eq 'renamed' }).Count
  $row.gitBinaries = @($reference.Values | Where-Object { $_.Binary }).Count
  $row.moreThan100Files = ($reference.Count -gt 100)

  # Files of path A, keyed like the reference (new path, old path when deleted).
  $files = [ordered]@{}
  foreach ($change in $aChanges) {
    $type = "$(Get-Field $change 'changeType')"
    $path = ConvertTo-RepoPath (Get-Field $change 'item.path')
    $oldPath = ConvertTo-RepoPath ((Get-Field $change 'originalPath') ?? (Get-Field $change 'sourceServerItem'))
    if (-not $oldPath -or $type -notmatch 'rename') { $oldPath = $path }
    $files[$path] = [pscustomobject]@{ Path = $path; OldPath = $oldPath; Type = $type; ObjectId = (Get-Field $change 'item.objectId'); OriginalObjectId = (Get-Field $change 'item.originalObjectId') }
  }
  $row.aRenames = @($files.Values | Where-Object { $_.Type -match 'rename' }).Count
  $row.aFilesMissing = @($reference.Keys | Where-Object { -not $files.Contains($_) }).Count
  $row.aFilesExtra = @($files.Keys | Where-Object { -not $reference.Contains($_) }).Count

  # Path B: FileDiffs, in batches.
  $bLines = @{}
  $bErrors = [System.Collections.Generic.List[int]]::new()
  $bRaw = [System.Collections.Generic.List[object]]::new()
  $entries = @($files.Values)
  for ($i = 0; $i -lt $entries.Count; $i += $FileDiffBatchSize) {
    $batch = $entries[$i..([Math]::Min($i + $FileDiffBatchSize, $entries.Count) - 1)]
    $body = @{ baseVersionCommit = $base; targetVersionCommit = $head; fileDiffParams = @($batch | ForEach-Object { @{ path = "/$($_.Path)"; originalPath = "/$($_.OldPath)" } }) }
    $answer = Invoke-Ado -Method 'POST' -Uri "$($fileDiffs.Url)?api-version=7.1-preview.1" -Body $body -Category 'B-filediffs'
    if ($answer.Status -ne 200) { $bErrors.Add($answer.Status); continue }
    $bRaw.Add($answer.Json)
    $items = if ($answer.Json -is [array]) { $answer.Json } else { @(Get-Field $answer.Json 'value') }
    foreach ($fileDiff in $items) {
      if ($null -eq $fileDiff -or $null -eq (Get-Field $fileDiff 'path')) { continue }
      $blocks = Get-Field $fileDiff 'lineDiffBlocks'
      $bLines[(ConvertTo-RepoPath (Get-Field $fileDiff 'path'))] = if ($null -eq $blocks) { 'no-blocks' } else { ConvertFrom-LineDiffBlock @($blocks) }
    }
  }
  $row.bErrors = @($bErrors)

  # Path C: both blobs of each file, diffed locally.
  $cLines = @{}; $cAlt = @{}; $blobs = @{ base = [System.Collections.Generic.List[object]]::new(); head = [System.Collections.Generic.List[object]]::new() }
  foreach ($file in $entries) {
    $isAdd = $file.Type -match 'add'
    $isDelete = $file.Type -match 'delete'
    $oldId = $file.OriginalObjectId ?? $(if ($isDelete) { $file.ObjectId })
    $oldBytes = if ($isAdd) { , [byte[]] @() } elseif ($oldId) { Get-Blob $oldId } else { Get-ItemBytes $file.OldPath $base }
    $newBytes = if ($isDelete) { , [byte[]] @() } elseif ($file.ObjectId) { Get-Blob $file.ObjectId } else { Get-ItemBytes $file.Path $head }
    if ($null -eq $oldBytes -or $null -eq $newBytes) { $cLines[$file.Path] = 'unreadable'; continue }
    $tooLarge = $oldBytes.Length -gt $MaxFileBytes -or $newBytes.Length -gt $MaxFileBytes
    $binary = (Test-BinaryContent $oldBytes) -or (Test-BinaryContent $newBytes)
    if ($FixturePulls.Count -gt 0 -and $pullId -eq $FixturePulls[0]) {
      $blobs.base.Add([ordered]@{ path = $file.OldPath; objectId = $oldId; size = $oldBytes.Length; binary = $binary; contentBase64 = $(if ($binary -or $tooLarge) { $null } else { [Convert]::ToBase64String($oldBytes) }) })
      $blobs.head.Add([ordered]@{ path = $file.Path; objectId = $file.ObjectId; size = $newBytes.Length; binary = $binary; contentBase64 = $(if ($binary -or $tooLarge) { $null } else { [Convert]::ToBase64String($newBytes) }) })
    }
    if ($tooLarge) { $cLines[$file.Path] = 'too-large'; continue }
    if ($binary) { $cLines[$file.Path] = 'binary'; continue }
    $cLines[$file.Path] = Get-BlobLineDiff $oldBytes $newBytes
    $cAlt[$file.Path] = Get-BlobLineDiff $oldBytes $newBytes -NoIndentHeuristic
  }

  # Comparison with the reference, file by file.
  $verdicts = @{ B = [ordered]@{}; C = [ordered]@{}; CNoIndent = [ordered]@{} }
  $details = [System.Collections.Generic.List[object]]::new()
  $keys = @(@($reference.Keys) + @($files.Keys) | Select-Object -Unique)
  foreach ($key in $keys) {
    $ref = if ($reference.Contains($key)) { $reference[$key] } else { $null }
    $refAlt = if ($referenceAlt.Contains($key)) { $referenceAlt[$key] } else { $null }
    $detail = [ordered]@{ file = (Get-PathHash $key); gitStatus = $(if ($ref) { $ref.Status } else { 'absent' }); azureType = $(if ($files.Contains($key)) { $files[$key].Type } else { 'absent' }) }
    foreach ($pathName in @('B', 'C', 'CNoIndent')) {
      $candidate = switch ($pathName) { 'B' { $bLines[$key] } 'C' { $cLines[$key] } 'CNoIndent' { $cAlt[$key] } }
      $expected = if ($pathName -eq 'CNoIndent') { $refAlt } else { $ref }
      $verdict = if ($null -eq $expected) { 'extra' }
      elseif ($null -eq $candidate) { if ($expected.Binary) { 'unknown:binary' } else { 'missing' } }
      elseif ($candidate -is [string]) { if ($expected.Binary -and $candidate -in @('binary', 'no-blocks')) { 'unknown:binary' } else { "unknown:$candidate" } }
      elseif ($expected.Binary) { 'binary-computed' }
      else { Compare-LineSet $expected.Lines $candidate }
      $detail[$pathName] = $verdict
      $verdicts[$pathName][$verdict] = 1 + ($verdicts[$pathName][$verdict] ?? 0)
    }
    $details.Add($detail)
  }
  foreach ($pathName in @('B', 'C', 'CNoIndent')) {
    $bad = @($verdicts[$pathName].Keys | Where-Object { $_ -notin @('match', 'unknown:binary', 'unknown:too-large') })
    $row["${pathName}Verdict"] = if ($bad.Count -eq 0) { 'match' } else { 'differ' }
    $row["${pathName}Files"] = $verdicts[$pathName]
  }
  $row.files = $details
  $row.outcome = 'compared'

  # Question 1: comment ids of the existing threads (read only).
  $threads = Invoke-Ado -Uri "$repoUrl/pullRequests/$pullId/threads?api-version=7.1" -Category 'side'
  foreach ($thread in @(Get-Field $threads.Json 'value')) {
    $comments = @(Get-Field $thread 'comments') | Where-Object { $_ }
    if ($comments.Count -eq 0) { continue }
    foreach ($comment in $comments) { Add-Identity (Get-Field $comment 'author') }
    $threadEvidence.Add([ordered]@{ pull = $pullId; thread = [long] (Get-Field $thread 'id'); comments = $comments.Count; firstCommentId = [int] (Get-Field ($comments | Sort-Object { [int] (Get-Field $_ 'id') } | Select-Object -First 1) 'id'); commentIds = @($comments | ForEach-Object { [int] (Get-Field $_ 'id') }) })
  }

  # Question 3: the active pull request whose head is this commit.
  if ($row.status -eq 'active') {
    $query = Invoke-Ado -Method 'POST' -Uri "$repoUrl/pullrequestquery?api-version=7.1" -Category 'side' -Body @{ queries = @(@{ type = 'commit'; items = @($head) }, @{ type = 'lastMergeCommit'; items = @((Get-Field $pull.Json 'lastMergeCommit.commitId')) }) }
    $queryFound = @{ 0 = $null; 1 = $null }
    $index = 0
    foreach ($result in @(Get-Field $query.Json 'results')) {
      if ($null -eq $result) { $index++; continue }
      $ids = foreach ($property in @($result.PSObject.Properties)) { @($property.Value) | ForEach-Object { [int] (Get-Field $_ 'pullRequestId') } }
      $queryFound[$index] = @($ids) -contains $pullId
      $index++
    }
    $source = [Uri]::EscapeDataString("$(Get-Field $pull.Json 'sourceRefName')")
    $active = Invoke-Ado -Uri "$repoUrl/pullrequests?searchCriteria.status=active&searchCriteria.sourceRefName=$source&api-version=7.1" -Category 'side'
    $byList = @(Get-Field $active.Json 'value' | Where-Object { (Get-Field $_ 'lastMergeSourceCommit.commitId') -eq $head } | ForEach-Object { [int] (Get-Field $_ 'pullRequestId') })
    $queryEvidence.Add([ordered]@{ pull = $pullId; queryStatus = $query.Status; commitQueryFindsPull = $queryFound[0]; lastMergeCommitQueryFindsPull = $queryFound[1]; listBySourceRefFindsPull = ($byList -contains $pullId); listMatches = $byList.Count })
  }

  # Fixtures of this pull request.
  if ($FixturePath -and $FixturePulls -contains $pullId) {
    $row.fixtures = @()
    $patchDir = Join-Path $FixturePath 'patches'
    New-Item -ItemType Directory -Force -Path $patchDir | Out-Null
    $found = Find-ForbiddenText $referenceText @{ organization = $forbidden.organization; token = $forbidden.token; email = @($forbidden.identity | Where-Object { $_ -match '@' }) }
    if ($found.Count -eq 0) { [System.IO.File]::WriteAllText((Join-Path $patchDir "pr-$pullId.diff"), $referenceText.Replace("`r`n", "`n"), [System.Text.UTF8Encoding]::new($false)); $row.fixtures += "patches/pr-$pullId.diff" }
    else { $row.fixtures += "patches/pr-$pullId.diff refused: $($found -join ', ')" }

    if ($pullId -eq $FixturePulls[0]) {
      $diffDir = Join-Path $FixturePath 'diffs'
      New-Item -ItemType Directory -Force -Path $diffDir | Out-Null
      $names = @{ $Organization = 'fabrikam'; $Project = 'MyProject' }
      foreach ($name in $forbidden.repository) { $names[$name] = 'MyRepo' }
      $guids = @{}
      $metadata = [ordered]@{
        'commits.json'           = ($diffs.Pages | Select-Object -First 1)
        'filediffs.json'         = @($bRaw)
        'iteration-changes.json' = @($iterationChanges.Pages)
      }
      for ($p = 1; $p -lt $diffs.Pages.Count; $p++) { $metadata["commits-page-$($p + 1).json"] = $diffs.Pages[$p] }
      foreach ($name in $metadata.Keys) {
        $text = Protect-Metadata ($metadata[$name] | ConvertTo-Json -Depth 64) $names $guids
        $found = Find-ForbiddenText $text $forbidden
        if ($found.Count -eq 0) { [System.IO.File]::WriteAllText((Join-Path $diffDir $name), $text.Replace("`r`n", "`n") + "`n", [System.Text.UTF8Encoding]::new($false)); $row.fixtures += "diffs/$name" }
        else { $row.fixtures += "diffs/$name refused: $($found -join ', ')" }
      }
      foreach ($sideName in @('base', 'head')) {
        $text = (@($blobs[$sideName]) | ConvertTo-Json -Depth 8).Replace("`r`n", "`n")
        $decoded = (@($blobs[$sideName]) | Where-Object { $_.contentBase64 } | ForEach-Object { [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($_.contentBase64)) }) -join "`n"
        $found = Find-ForbiddenText ($text + $decoded) @{ organization = $forbidden.organization; token = $forbidden.token; email = @($forbidden.identity | Where-Object { $_ -match '@' }) }
        if ($found.Count -eq 0) { [System.IO.File]::WriteAllText((Join-Path $diffDir "blob-$sideName.json"), $text + "`n", [System.Text.UTF8Encoding]::new($false)); $row.fixtures += "diffs/blob-$sideName.json" }
        else { $row.fixtures += "diffs/blob-$sideName.json refused: $($found -join ', ')" }
      }
    }
  }

  $results.Add($row)
}

# ------------------------------------------------------------------------------------------------
# Question 4: Project Collection Administrators, read with the token of the run.
# ------------------------------------------------------------------------------------------------
$membership = [ordered]@{ groupsStatus = $null; groupFound = $false; descriptorStatus = $null; membershipStatus = $null; membersListStatus = $null; directMembers = $null }
$groups = [System.Collections.Generic.List[object]]::new()
$continuation = $null
do {
  $uri = "$graphUrl/_apis/graph/groups?api-version=7.1-preview.1" + $(if ($continuation) { "&continuationToken=$([Uri]::EscapeDataString($continuation))" })
  $answer = Invoke-Ado -Uri $uri -Category 'side'
  $membership.groupsStatus = $answer.Status
  if ($answer.Status -ne 200) { break }
  @(Get-Field $answer.Json 'value') | Where-Object { $_ } | ForEach-Object { $groups.Add($_) }
  $continuation = $answer.Headers['x-ms-continuationtoken']
} while ($continuation)
$pca = $groups | Where-Object { "$(Get-Field $_ 'principalName')" -like '*\Project Collection Administrators' } | Select-Object -First 1
if ($pca) {
  $membership.groupFound = $true
  $descriptor = Invoke-Ado -Uri "$graphUrl/_apis/graph/descriptors/$userId`?api-version=7.1-preview.1" -Category 'side'
  $membership.descriptorStatus = $descriptor.Status
  if ($descriptor.Status -eq 200) {
    $check = Invoke-Ado -Method 'HEAD' -Uri "$graphUrl/_apis/graph/memberships/$(Get-Field $descriptor.Json 'value')/$(Get-Field $pca 'descriptor')?api-version=7.1-preview.1" -Category 'side'
    $membership.membershipStatus = $check.Status
  }
  $members = Invoke-Ado -Uri "$graphUrl/_apis/graph/Memberships/$(Get-Field $pca 'descriptor')?direction=down&api-version=7.1-preview.1" -Category 'side'
  $membership.membersListStatus = $members.Status
  if ($members.Status -eq 200) { $membership.directMembers = @(Get-Field $members.Json 'value').Count }
}
$side.collectionAdministrators = $membership

# ------------------------------------------------------------------------------------------------
# Question 1, optional write: two probe threads on a test pull request, then deleted.
# ------------------------------------------------------------------------------------------------
$probe = $null
if ($ProbeThreadPull -gt 0) {
  $probe = [System.Collections.Generic.List[object]]::new()
  foreach ($n in 1..2) {
    $created = Invoke-Ado -Method 'POST' -Uri "$repoUrl/pullRequests/$ProbeThreadPull/threads?api-version=7.1" -Category 'side' -Body @{ comments = @(@{ parentCommentId = 0; content = "<!-- code-coverage:spike-m7-01 -->`nProbe $n of the spike M7.01, deleted by the script."; commentType = 1 }); status = 1 }
    $threadId = Get-Field $created.Json 'id'
    $entry = [ordered]@{ createStatus = $created.Status; threadIdType = $(if ($threadId) { $threadId.GetType().Name }); firstCommentId = (Get-Field (@(Get-Field $created.Json 'comments') | Select-Object -First 1) 'id') }
    if ($threadId) {
      $updated = Invoke-Ado -Method 'PATCH' -Uri "$repoUrl/pullRequests/$ProbeThreadPull/threads/$threadId/comments/1?api-version=7.1" -Category 'side' -Body @{ content = "<!-- code-coverage:spike-m7-01 -->`nProbe $n, updated in place." }
      $entry.updateCommentOneStatus = $updated.Status
      $deleted = Invoke-Ado -Method 'DELETE' -Uri "$repoUrl/pullRequests/$ProbeThreadPull/threads/$threadId/comments/1?api-version=7.1" -Category 'side'
      $entry.deleteStatus = $deleted.Status
      $after = Invoke-Ado -Uri "$repoUrl/pullRequests/$ProbeThreadPull/threads/$threadId`?api-version=7.1" -Category 'side'
      $entry.threadDeletedAfter = Get-Field $after.Json 'isDeleted'
    }
    $probe.Add($entry)
  }
}

$side.commentIds = [ordered]@{
  threadsRead = $threadEvidence.Count
  threadsWithOneComment = @($threadEvidence | Where-Object { $_.comments -eq 1 }).Count
  threadsWhoseFirstCommentIsOne = @($threadEvidence | Where-Object { $_.firstCommentId -eq 1 }).Count
  pullsWithSeveralThreadsAllStartingAtOne = @($threadEvidence | Group-Object { $_.pull } | Where-Object { $_.Count -ge 2 -and @($_.Group | Where-Object { $_.firstCommentId -ne 1 }).Count -eq 0 }).Count
  probe = $probe
}
$side.activePullOfHead = @($queryEvidence)

# ------------------------------------------------------------------------------------------------
# Report: anonymized JSON and a text summary, checked before they are written.
# ------------------------------------------------------------------------------------------------
$compared = @($results | Where-Object { $_.outcome -eq 'compared' })
$summary = [ordered]@{
  generated = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
  gitVersion = (& git --version)
  fileDiffsRoute = [ordered]@{ source = $fileDiffs.Source; releasedVersion = $fileDiffs.ReleasedVersion; maxVersion = $fileDiffs.MaxVersion }
  objectIdFallbacks = $objectIdFallbacks
  options = [ordered]@{ pageSize = $PageSize; fileDiffBatchSize = $FileDiffBatchSize; maxFileBytes = $MaxFileBytes; tokenKind = $TokenKind; status = $Status }
  pullsRequested = $Pulls.Count
  pullsCompared = $compared.Count
  matches = [ordered]@{
    B = @($compared | Where-Object { $_.BVerdict -eq 'match' }).Count
    C = @($compared | Where-Object { $_.CVerdict -eq 'match' }).Count
    CNoIndent = @($compared | Where-Object { $_.CNoIndentVerdict -eq 'match' }).Count
    AFileList = @($compared | Where-Object { $_.aFilesMissing -eq 0 -and $_.aFilesExtra -eq 0 }).Count
  }
  cost = $stats
  sideQuestions = $side
  pulls = $results
}

$json = $summary | ConvertTo-Json -Depth 32
$leak = Find-ForbiddenText $json $forbidden
if ($leak.Count -gt 0) { throw "The report still contains: $($leak -join ', '). Nothing was written; report this as a bug of the script." }
$json.Replace("`r`n", "`n") | Set-Content -LiteralPath (Join-Path $OutputPath 'report.json') -Encoding utf8NoBOM

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add("M7.01 measurement, $($summary.generated), $($summary.gitVersion)")
$lines.Add("FileDiffs route: $($fileDiffs.Source), releasedVersion=$($fileDiffs.ReleasedVersion), maxVersion=$($fileDiffs.MaxVersion)")
$lines.Add("Pull requests compared: $($compared.Count) of $($Pulls.Count)")
$lines.Add("Same lines as git diff -U0: B (FileDiffs) $($summary.matches.B), C (blobs + xdiff) $($summary.matches.C), C without indent heuristic vs git --no-indent-heuristic $($summary.matches.CNoIndent); same file list for A: $($summary.matches.AFileList)")
$lines.Add('')
$lines.Add('| PR | git files | renames | binaries | >100 | A pages | A missing/extra | base = iteration | B | C | C (no indent) |')
$lines.Add('|---|---|---|---|---|---|---|---|---|---|---|')
foreach ($r in $results) {
  if ($r.outcome -ne 'compared') { $lines.Add("| $($r.pull) | $($r.outcome) |||||||||"); continue }
  $lines.Add("| $($r.pull) | $($r.gitFiles) | $($r.gitRenames) | $($r.gitBinaries) | $($r.moreThan100Files) | $($r.aPages) | $($r.aFilesMissing)/$($r.aFilesExtra) | $($r.baseEqualsIterationCommonRef) | $($r.BVerdict) | $($r.CVerdict) | $($r.CNoIndentVerdict) |")
}
$lines.Add('')
$lines.Add('| Cost | requests | bytes | ms | X-RateLimit-Cost sum (responses carrying it) | min remaining | throttled | failures |')
$lines.Add('|---|---|---|---|---|---|---|---|')
foreach ($key in $stats.Keys) {
  $s = $stats[$key]
  $lines.Add("| $key | $($s.requests) | $($s.bytes) | $($s.milliseconds) | $($s.costSum) ($($s.costHeaders)) | $($s.minRemaining) | $($s.throttled) | $($s.failures) |")
}
$lines.Add('')
$lines.Add("Comment ids: $($side.commentIds | ConvertTo-Json -Depth 6 -Compress)")
$lines.Add("Organization id: $($side.organizationId | ConvertTo-Json -Compress)")
$lines.Add("Active pull request of a head: $($side.activePullOfHead | ConvertTo-Json -Depth 4 -Compress)")
$lines.Add("Collection administrators: $($side.collectionAdministrators | ConvertTo-Json -Compress)")
$text = $lines -join "`n"
if ((Find-ForbiddenText $text $forbidden).Count -gt 0) { throw 'The summary still contains a forbidden value. Nothing more was written.' }
$text | Set-Content -LiteralPath (Join-Path $OutputPath 'report.md') -Encoding utf8NoBOM
Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
Write-Output $text
Write-Output ''
Write-Output "Report: $(Join-Path $OutputPath 'report.md') and report.json (anonymized)."
