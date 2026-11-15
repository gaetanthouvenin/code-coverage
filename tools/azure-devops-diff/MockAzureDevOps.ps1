#Requires -Version 7.2
<#
.SYNOPSIS
  A local stand-in for the Azure DevOps endpoints the spike reads, answered from a git repository.
  For the offline self-test only (Test-AzureDevOpsDiff.ps1): the answers follow the shapes of the
  REST reference 7.1, not the real service, whose behaviour the real run measures.

.PARAMETER Prefix
  The address to listen on, http://localhost:<port>/.

.PARAMETER RepoPath
  The git repository whose commits are served.

.PARAMETER InfoFile
  A JSON file: organization, project, repository, pullId, base, target, head, user, email.

.PARAMETER StopFile
  The listener stops when this file exists.
#>
param([string] $Prefix, [string] $RepoPath, [string] $InfoFile, [string] $StopFile)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$i = Get-Content -LiteralPath $InfoFile -Raw | ConvertFrom-Json

function Invoke-RepoGit([string[]] $Arguments) { return (& git -C $RepoPath @Arguments 2>$null) }

function Get-Changes {
  # The changed files between base and head, as Diffs - Get would list them (blobs only).
  $lines = @(Invoke-RepoGit @('diff', '--name-status', '-M', $i.base, $i.head))
  foreach ($line in $lines) {
    $parts = $line -split "`t"
    $code = $parts[0].Substring(0, 1)
    switch ($code) {
      'A' { [ordered]@{ changeType = 'add'; item = [ordered]@{ objectId = (Invoke-RepoGit @('rev-parse', "$($i.head):$($parts[1])")); gitObjectType = 'blob'; path = "/$($parts[1])" } } }
      'D' { [ordered]@{ changeType = 'delete'; item = [ordered]@{ originalObjectId = (Invoke-RepoGit @('rev-parse', "$($i.base):$($parts[1])")); gitObjectType = 'blob'; path = "/$($parts[1])" } } }
      'R' { [ordered]@{ changeType = 'edit, rename'; originalPath = "/$($parts[1])"; item = [ordered]@{ objectId = (Invoke-RepoGit @('rev-parse', "$($i.head):$($parts[2])")); originalObjectId = (Invoke-RepoGit @('rev-parse', "$($i.base):$($parts[1])")); gitObjectType = 'blob'; path = "/$($parts[2])" } } }
      default { [ordered]@{ changeType = 'edit'; item = [ordered]@{ objectId = (Invoke-RepoGit @('rev-parse', "$($i.head):$($parts[1])")); originalObjectId = (Invoke-RepoGit @('rev-parse', "$($i.base):$($parts[1])")); gitObjectType = 'blob'; path = "/$($parts[1])"; url = "https://dev.azure.com/$($i.organization)/$($i.project)/_apis/git/repositories/$($i.repository)/items/$($parts[1])" } } }
    }
  }
}

function Get-Blocks([string] $OldPath, [string] $NewPath) {
  # lineDiffBlocks from git's own hunks, for the stand-in of FileDiffs.
  $hasOld = $null -ne (Invoke-RepoGit @('rev-parse', '--verify', '--quiet', "$($i.base):$OldPath"))
  $hasNew = $null -ne (Invoke-RepoGit @('rev-parse', '--verify', '--quiet', "$($i.head):$NewPath"))
  if (-not $hasOld -or -not $hasNew) {
    $lines = @(Invoke-RepoGit @('show', $(if ($hasNew) { "$($i.head):$NewPath" } else { "$($i.base):$OldPath" }))).Count
    if ($hasNew) { return @([ordered]@{ changeType = 1; originalLineNumberStart = 0; originalLinesCount = 0; modifiedLineNumberStart = 1; modifiedLinesCount = $lines }) }
    return @([ordered]@{ changeType = 2; originalLineNumberStart = 1; originalLinesCount = $lines; modifiedLineNumberStart = 0; modifiedLinesCount = 0 })
  }
  $text = (Invoke-RepoGit @('diff', '-U0', '--no-color', "$($i.base):$OldPath", "$($i.head):$NewPath")) -join "`n"
  if ($text -match 'Binary files') { return $null }
  $blocks = foreach ($m in [regex]::Matches($text, '(?m)^@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@')) {
    $oc = if ($m.Groups[2].Success) { [int] $m.Groups[2].Value } else { 1 }
    $nc = if ($m.Groups[4].Success) { [int] $m.Groups[4].Value } else { 1 }
    $type = if ($oc -eq 0) { 1 } elseif ($nc -eq 0) { 2 } else { 3 }
    [ordered]@{ changeType = $type; originalLineNumberStart = [int] $m.Groups[1].Value; originalLinesCount = $oc; modifiedLineNumberStart = [int] $m.Groups[3].Value; modifiedLinesCount = $nc }
  }
  return @([ordered]@{ changeType = 0; originalLineNumberStart = 1; originalLinesCount = 1; modifiedLineNumberStart = 1; modifiedLinesCount = 1 }) + @($blocks)
}

function Get-Page($Items, $Query) {
  $top = if ($Query['$top']) { [int] $Query['$top'] } else { 100 }
  $skip = if ($Query['$skip']) { [int] $Query['$skip'] } else { 0 }
  return @($Items | Select-Object -Skip $skip -First $top)
}

$listener = [System.Net.HttpListener]::new()
$listener.Prefixes.Add($Prefix)
$listener.Start()
$identity = [ordered]@{ id = '11111111-2222-3333-4444-555555555555'; displayName = $i.user; uniqueName = $i.email; providerDisplayName = $i.user }
try {
  while (-not (Test-Path -LiteralPath $StopFile)) {
    $pending = $listener.GetContextAsync()
    while (-not $pending.Wait(200)) { if (Test-Path -LiteralPath $StopFile) { return } }
    $context = $pending.Result
    $request = $context.Request
    $path = $request.Url.AbsolutePath
    $query = [System.Web.HttpUtility]::ParseQueryString($request.Url.Query)
    $body = if ($request.HasEntityBody) { [System.IO.StreamReader]::new($request.InputStream).ReadToEnd() | ConvertFrom-Json } else { $null }
    $status = 200
    $bytes = $null
    $answer = switch -Regex ($path) {
      '/dev/[^/]+/_apis/connectionData$' { [ordered]@{ authenticatedUser = $identity; instanceId = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee'; deploymentType = 'hosted' } }
      '/dev/[^/]+/_apis/git$' { [ordered]@{ count = 1; value = @([ordered]@{ id = 'c4c5a7e6-e9f3-4730-a92b-84baacff694b'; area = 'git'; resourceName = 'fileDiffs'; routeTemplate = '{project}/_apis/{area}/repositories/{repositoryId}/{resource}'; releasedVersion = '0.0'; maxVersion = '7.2' }) } }
      '/app/' { $status = 401; [ordered]@{ message = 'TF400813' } }
      '/pullRequests/\d+/iterations/\d+/changes$' {
        $all = @(Get-Changes | ForEach-Object { $_.item.Remove('url'); $_ })
        $page = Get-Page $all $query
        $skip = if ($query['$skip']) { [int] $query['$skip'] } else { 0 }
        $next = if ($skip + $page.Count -lt $all.Count) { $skip + $page.Count } else { 0 }
        [ordered]@{ changeEntries = $page; nextSkip = $next; nextTop = $(if ($next) { $page.Count } else { 0 }) }
      }
      '/pullRequests/\d+/iterations$' { [ordered]@{ value = @([ordered]@{ id = 1; sourceRefCommit = @{ commitId = $i.head }; targetRefCommit = @{ commitId = $i.target }; commonRefCommit = @{ commitId = $i.base } }) } }
      '/pullRequests/\d+/threads$' { [ordered]@{ value = @([ordered]@{ id = 7; comments = @([ordered]@{ id = 1; author = $identity }) }, [ordered]@{ id = 8; comments = @([ordered]@{ id = 1; author = $identity }, [ordered]@{ id = 2; parentCommentId = 1; author = $identity }) }) } }
      '/pullRequests/\d+$' { [ordered]@{ pullRequestId = $i.pullId; status = 'active'; sourceRefName = 'refs/heads/feature'; createdBy = $identity; lastMergeSourceCommit = @{ commitId = $i.head }; lastMergeTargetCommit = @{ commitId = $i.target }; lastMergeCommit = @{ commitId = $i.target } } }
      '/pullrequests$' { [ordered]@{ value = @([ordered]@{ pullRequestId = $i.pullId; lastMergeSourceCommit = @{ commitId = $i.head } }) } }
      '/pullrequestquery$' { [ordered]@{ queries = $body.queries; results = @([ordered]@{ $i.head = @(@{ pullRequestId = $i.pullId }) }, [ordered]@{}) } }
      '/diffs/commits$' {
        $all = @(Get-Changes)
        $page = Get-Page $all $query
        $result = [ordered]@{ changes = $page; commonCommit = $i.base; aheadCount = 1; behindCount = 1 }
        if (-not $query['$skip']) { $result.allChangesIncluded = $true }
        $result
      }
      '/fileDiffs$' { , @($body.fileDiffParams | ForEach-Object { [ordered]@{ path = $_.path; originalPath = $_.originalPath; lineDiffBlocks = (Get-Blocks $_.originalPath.TrimStart('/') $_.path.TrimStart('/')) } }) }
      '/blobs/([0-9a-f]{40})$' {
        $sha = $Matches[1]
        $file = Join-Path ([System.IO.Path]::GetTempPath()) "mock-blob-$sha"
        Start-Process -FilePath 'git' -ArgumentList @('-C', $RepoPath, 'cat-file', 'blob', $sha) -RedirectStandardOutput $file -NoNewWindow -Wait
        $bytes = [System.IO.File]::ReadAllBytes($file)
        Remove-Item -LiteralPath $file
        $null
      }
      '/git/repositories/[^/]+$' { [ordered]@{ id = '99999999-8888-7777-6666-555555555555'; name = $i.repository } }
      '/graph/groups$' { [ordered]@{ value = @([ordered]@{ principalName = "[$($i.organization)]\Project Collection Administrators"; descriptor = 'vssgp.Pca' }) } }
      '/graph/descriptors/' { [ordered]@{ value = 'aad.User' } }
      '/graph/memberships/[^/]+/[^/]+$' { $null }
      '/graph/Memberships/[^/]+$' { [ordered]@{ value = @([ordered]@{ memberDescriptor = 'aad.User' }) } }
      default { $status = 404; [ordered]@{ message = 'not mocked' } }
    }
    $response = $context.Response
    $response.StatusCode = $status
    if ($null -eq $bytes) {
      $json = if ($null -eq $answer) { '' } else { ConvertTo-Json -InputObject $answer -Depth 20 }
      $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
      $response.ContentType = 'application/json; charset=utf-8'
    }
    else { $response.ContentType = 'application/octet-stream' }
    if ($request.HttpMethod -ne 'HEAD') { $response.OutputStream.Write($bytes, 0, $bytes.Length) }
    $response.Close()
  }
}
finally {
  $listener.Stop()
}
