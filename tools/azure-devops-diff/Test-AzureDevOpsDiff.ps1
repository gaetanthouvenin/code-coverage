#Requires -Version 7.2
<#
.SYNOPSIS
  Offline self-test of AzureDevOpsDiff.psm1 (M7.01): no network, no Azure DevOps.

.DESCRIPTION
  Checks the reading of git diff -U0, the lineDiffBlocks conversion, the binary heuristic, the
  scrubbing, and compares the local blob diff (the port of xdiff) with git diff --no-index -U0 on
  synthetic file pairs, including the files of this repository edited at random. The last check
  is the local evidence for path C; it needs git on the PATH.

.PARAMETER Pairs
  How many random edits of repository files are compared with git (200).

.EXAMPLE
  pwsh tools/azure-devops-diff/Test-AzureDevOpsDiff.ps1
#>
[CmdletBinding()]
param([ValidateRange(0, 5000)] [int] $Pairs = 200)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'AzureDevOpsDiff.psm1') -Force
$failures = [System.Collections.Generic.List[string]]::new()
$work = Join-Path ([System.IO.Path]::GetTempPath()) "azdo-diff-test-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $work | Out-Null

function Assert-True {
  param([string] $Name, [bool] $Condition, [string] $Detail = '')

  if ($Condition) { Write-Host "  ok: $Name" } else { $failures.Add("$Name $Detail") }
}

function Get-GitLineSet {
  # The reference: git diff --no-index -U0 between two files, read by ConvertFrom-GitDiff.
  param([string] $OldPath, [string] $NewPath, [switch] $NoIndentHeuristic)

  $options = @('-c', 'core.autocrlf=false', '-c', 'diff.algorithm=default', 'diff', '--no-index', '-U0', '--no-color', '--no-ext-diff')
  if ($NoIndentHeuristic) { $options += '--no-indent-heuristic' } else { $options += '--indent-heuristic' }
  $text = (& git @options -- $OldPath $NewPath 2>$null) -join "`n"
  $files = ConvertFrom-GitDiff $text
  if ($files.Count -eq 0) { return New-LineSet }
  return @($files.Values)[0].Lines
}

function Write-Bytes([string] $Path, [string] $Text) { [System.IO.File]::WriteAllBytes($Path, [System.Text.Encoding]::UTF8.GetBytes($Text)) }

try {
  Write-Host 'git diff -U0 reading'
  $sample = @'
diff --git a/src/A.cs b/src/A.cs
index 1111111..2222222 100644
--- a/src/A.cs
+++ b/src/A.cs
@@ -3 +3,2 @@ class A
-x
+y
+z
@@ -10,0 +12 @@ class A
+w
diff --git a/old name.txt b/new name.txt
similarity index 90%
rename from old name.txt
rename to new name.txt
index 3333333..4444444 100644
--- a/old name.txt
+++ b/new name.txt
@@ -1 +1 @@
-a
+b
diff --git a/img.png b/img.png
new file mode 100644
index 0000000..5555555
Binary files /dev/null and b/img.png differ
diff --git a/gone.cs b/gone.cs
deleted file mode 100644
index 6666666..0000000
--- a/gone.cs
+++ /dev/null
@@ -1,2 +0,0 @@
-1
-2
'@
  $parsed = ConvertFrom-GitDiff $sample
  Assert-True 'four files are read' ($parsed.Count -eq 4) "($($parsed.Count))"
  Assert-True 'added lines of a modified file' ((@($parsed['src/A.cs'].Lines.Added) -join ',') -eq '3,4,12')
  Assert-True 'removed lines of a modified file' ((@($parsed['src/A.cs'].Lines.Removed) -join ',') -eq '3')
  Assert-True 'a rename keeps both paths' ($parsed['new name.txt'].OldPath -eq 'old name.txt' -and $parsed['new name.txt'].Status -eq 'renamed')
  Assert-True 'a binary is flagged' ($parsed['img.png'].Binary -and $parsed['img.png'].Status -eq 'added')
  Assert-True 'a deleted file is keyed by its old path' ($parsed['gone.cs'].Status -eq 'deleted' -and (@($parsed['gone.cs'].Lines.Removed) -join ',') -eq '1,2')
  $quotedPath = '"a/dir\\x\303\251.txt"'
  $quoted = ConvertFrom-GitDiff ("diff --git $quotedPath $($quotedPath.Replace('a/', 'b/'))`nindex 1..2 100644`n--- $quotedPath`n+++ $($quotedPath.Replace('a/', 'b/'))`n@@ -1 +1 @@`n-a`n+b")
  Assert-True 'a quoted path is unquoted (backslash, octal UTF-8)' ($quoted.Contains("dir\x$([char] 0xE9).txt")) "($(@($quoted.Keys) -join ', '))"

  Write-Host 'lineDiffBlocks'
  $blocks = @(
    [pscustomobject]@{ changeType = 'none'; originalLineNumberStart = 1; originalLinesCount = 2; modifiedLineNumberStart = 1; modifiedLinesCount = 2 },
    [pscustomobject]@{ changeType = 3; originalLineNumberStart = 3; originalLinesCount = 1; modifiedLineNumberStart = 3; modifiedLinesCount = 2 },
    [pscustomobject]@{ changeType = 'add'; originalLineNumberStart = 10; originalLinesCount = 0; modifiedLineNumberStart = 12; modifiedLinesCount = 1 })
  $fromBlocks = ConvertFrom-LineDiffBlock $blocks
  Assert-True 'blocks give the same lines as git' ((Compare-LineSet $parsed['src/A.cs'].Lines $fromBlocks) -eq 'match')

  Write-Host 'binary heuristic and line split'
  Assert-True 'a NUL byte is binary' (Test-BinaryContent ([byte[]] @(65, 0, 66)))
  Assert-True 'text is not binary' (-not (Test-BinaryContent ([System.Text.Encoding]::UTF8.GetBytes("a`r`nb"))))
  Assert-True 'a missing final newline is its own line' ((Split-GitLine ([System.Text.Encoding]::UTF8.GetBytes("a`nb"))).Length -eq 2)
  Assert-True 'a carriage return stays in its line' ((Split-GitLine ([System.Text.Encoding]::UTF8.GetBytes("a`r`nb`r`n")))[0] -eq "a`r`n")

  Write-Host 'scrubbing'
  $guids = @{}
  $json = '{"url":"https://dev.azure.com/Contoso-Org/Proj%20One/_apis/git/repositories/7d9c3e5b-4a2f-4c1e-9b8d-1f2e3d4c5b6a","author":{"displayName":"Jane Roe","uniqueName":"jane@contoso.com"},"x":"7D9C3E5B-4A2F-4C1E-9B8D-1F2E3D4C5B6A","note":"mail ops@contoso.com"}'
  $scrubbed = Protect-Metadata $json @{ 'Contoso-Org' = 'fabrikam'; 'Proj One' = 'MyProject' } $guids
  $left = Find-ForbiddenText $scrubbed @{ organization = 'Contoso-Org'; project = 'Proj One'; user = @('Jane Roe', 'jane@contoso.com'); email = 'ops@contoso.com' }
  Assert-True 'nothing forbidden is left' ($left.Count -eq 0) "($($left -join ', ')) $scrubbed"
  Assert-True 'the same GUID gets the same placeholder' (([regex]::Matches($scrubbed, '00000000-0000-0000-0000-000000000001')).Count -eq 2) $scrubbed
  Assert-True 'a leftover is reported by its label' ((Find-ForbiddenText 'by Jane Roe' @{ user = 'jane roe' }) -contains 'user')

  Write-Host 'local blob diff against git diff --no-index -U0'
  $cases = [ordered]@{
    'edit in the middle'      = @("a`nb`nc`nd`n", "a`nb`nX`nd`n")
    'added file'              = @('', "1`n2`n")
    'removed file'            = @("1`n2`n", '')
    'no final newline added'  = @("a`nb", "a`nb`n")
    'duplicated braces'       = @("f() {`n  x;`n}`n`ng() {`n  y;`n}`n", "f() {`n  x;`n}`n`nh() {`n  z;`n}`n`ng() {`n  y;`n}`n")
    'CRLF to LF'              = @("a`r`nb`r`n", "a`nb`n")
    'repeated blank lines'    = @("a`n`n`nb`n", "a`n`n`n`n`nb`n")
  }
  $old = Join-Path $work 'old.txt'
  $new = Join-Path $work 'new.txt'
  foreach ($name in $cases.Keys) {
    Write-Bytes $old $cases[$name][0]
    Write-Bytes $new $cases[$name][1]
    $mine = Get-BlobLineDiff ([System.IO.File]::ReadAllBytes($old)) ([System.IO.File]::ReadAllBytes($new))
    $git = Get-GitLineSet $old $new
    Assert-True "path C matches git: $name" ((Compare-LineSet $git $mine) -eq 'match') "(git +$(@($git.Added) -join ',') -$(@($git.Removed) -join ',') / mine +$(@($mine.Added) -join ',') -$(@($mine.Removed) -join ','))"
  }

  # Random edits of the files of this repository: the closest offline stand-in for real pull requests.
  $root = (& git -C $PSScriptRoot rev-parse --show-toplevel).Trim()
  $sources = @(& git -C $root ls-files -- '*.cs' '*.razor' '*.md' '*.ps1' | Where-Object { $_ })
  $random = [System.Random]::new(277)
  $same = 0; $sameNoIndent = 0; $compared = 0; $gaps = [System.Collections.Generic.List[string]]::new()
  for ($p = 0; $p -lt $Pairs -and $sources.Count -gt 0; $p++) {
    $file = $sources[$random.Next($sources.Count)]
    $lines = [System.Collections.Generic.List[string]]::new([string[]] (Get-Content -LiteralPath (Join-Path $root $file)))
    $original = ($lines -join "`n") + "`n"
    for ($e = 0; $e -lt 1 + $random.Next(6); $e++) {
      $at = $random.Next([Math]::Max(1, $lines.Count))
      switch ($random.Next(4)) {
        0 { $lines.Insert([Math]::Min($at, $lines.Count), "    // inserted $e") }
        1 { if ($lines.Count -gt 1) { $lines.RemoveAt([Math]::Min($at, $lines.Count - 1)) } }
        2 { if ($lines.Count -gt 0) { $lines[[Math]::Min($at, $lines.Count - 1)] += ' // edited' } }
        3 { $lines.InsertRange([Math]::Min($at, $lines.Count), [string[]] @('', '  {', '  }', '')) }
      }
    }
    Write-Bytes $old $original
    Write-Bytes $new (($lines -join "`n") + "`n")
    $oldBytes = [System.IO.File]::ReadAllBytes($old)
    $newBytes = [System.IO.File]::ReadAllBytes($new)
    $mine = Get-BlobLineDiff $oldBytes $newBytes
    $mineNoIndent = Get-BlobLineDiff $oldBytes $newBytes -NoIndentHeuristic
    $git = Get-GitLineSet $old $new
    $gitNoIndent = Get-GitLineSet $old $new -NoIndentHeuristic
    $compared++
    if ((Compare-LineSet $git $mine) -eq 'match') { $same++ } else { $gaps.Add($file) }
    if ((Compare-LineSet $gitNoIndent $mineNoIndent) -eq 'match') { $sameNoIndent++ }
  }
  Write-Host "  random edits: $same/$compared match git (indent heuristic on), $sameNoIndent/$compared match git --no-indent-heuristic"
  if ($gaps.Count -gt 0) { Write-Host "  differ: $($gaps -join ', ')" }
  Assert-True 'every random edit matches git' ($same -eq $compared) "($same/$compared)"
  Assert-True 'every random edit matches git --no-indent-heuristic' ($sameNoIndent -eq $compared) "($sameNoIndent/$compared)"

  Write-Host 'measurement script, dry run and a run against the local stand-in of Azure DevOps'
  $measure = Join-Path $PSScriptRoot 'Measure-AzureDevOpsDiff.ps1'
  $dry = (& pwsh -NoProfile -NonInteractive -File $measure -Organization contoso-spike -Project Mercury -Repository Atlas 2>&1 | Out-String)
  Assert-True 'the dry run exits with 0 and reads nothing' ($LASTEXITCODE -eq 0 -and $dry -match 'nothing read') $dry

  # A repository with a base, a target that moved on, and a head with an edit, a rename, an added,
  # a deleted and a binary file.
  $repo = Join-Path $work 'repo'
  New-Item -ItemType Directory -Path $repo | Out-Null
  $git = { param([string[]] $GitArgs) & git -C $repo -c user.name=spike -c user.email=spike@example.invalid -c core.autocrlf=false @GitArgs 2>&1 | Out-Null }
  & $git @('init', '--quiet', '--initial-branch=main')
  New-Item -ItemType Directory -Path (Join-Path $repo 'src'), (Join-Path $repo 'docs') | Out-Null
  $class = "namespace Spike;`n`npublic sealed class A`n{`n  public int One() => 1;`n`n  public int Two()`n  {`n    return 2;`n  }`n}`n"
  Write-Bytes (Join-Path $repo 'src/A.cs') $class
  Write-Bytes (Join-Path $repo 'old.txt') ((1..12 | ForEach-Object { "line $_" }) -join "`n")
  Write-Bytes (Join-Path $repo 'gone.txt') "gone`n"
  Write-Bytes (Join-Path $repo 'docs/readme.md') "# Spike`n"
  [System.IO.File]::WriteAllBytes((Join-Path $repo 'img.bin'), [byte[]] @(137, 80, 0, 1, 2, 3))
  & $git @('add', '-A'); & $git @('commit', '--quiet', '-m', 'base')
  $baseSha = (& git -C $repo rev-parse HEAD).Trim()
  & $git @('checkout', '--quiet', '-b', 'feature')
  Write-Bytes (Join-Path $repo 'src/A.cs') ($class.Replace("  public int Two()", "  public int Three()`n  {`n    return 3;`n  }`n`n  public int Two()"))
  & $git @('mv', 'old.txt', 'new.txt')
  Write-Bytes (Join-Path $repo 'new.txt') (((1..12 | ForEach-Object { "line $_" }) -join "`n").Replace('line 6', 'line six'))
  Write-Bytes (Join-Path $repo 'added.cs') "class Added`n{`n}`n"
  Remove-Item (Join-Path $repo 'gone.txt')
  [System.IO.File]::WriteAllBytes((Join-Path $repo 'img.bin'), [byte[]] @(137, 80, 0, 9, 9))
  & $git @('add', '-A'); & $git @('commit', '--quiet', '-m', 'head')
  $headSha = (& git -C $repo rev-parse HEAD).Trim()
  & $git @('checkout', '--quiet', 'main')
  Write-Bytes (Join-Path $repo 'docs/readme.md') "# Spike`n`nTarget moved on.`n"
  & $git @('commit', '--quiet', '-am', 'target')
  $targetSha = (& git -C $repo rev-parse HEAD).Trim()

  $probe = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
  $probe.Start(); $port = $probe.LocalEndpoint.Port; $probe.Stop()
  $stop = Join-Path $work 'stop-mock'
  $info = @{ organization = 'contoso-spike'; project = 'Mercury'; repository = 'Atlas'; pullId = 1; base = $baseSha; target = $targetSha; head = $headSha; user = 'Jane Roe'; email = 'jane.roe@contoso-spike.com' } | ConvertTo-Json -Compress
  $infoFile = Join-Path $work 'mock-info.json'
  Set-Content -LiteralPath $infoFile -Value $info -Encoding utf8NoBOM
  $mock = Start-Process -FilePath 'pwsh' -PassThru -NoNewWindow -ArgumentList @('-NoProfile', '-NonInteractive', '-File', (Join-Path $PSScriptRoot 'MockAzureDevOps.ps1'), '-Prefix', "http://localhost:$port/", '-RepoPath', $repo, '-InfoFile', $infoFile, '-StopFile', $stop)
  try {
    $client = [System.Net.Http.HttpClient]::new()
    $ready = $false
    for ($t = 0; $t -lt 50 -and -not $ready; $t++) {
      try { $ready = $client.GetAsync("http://localhost:$port/dev/x/_apis/connectionData").GetAwaiter().GetResult().IsSuccessStatusCode } catch { Start-Sleep -Milliseconds 200 }
    }
    Assert-True 'the stand-in of Azure DevOps listens' $ready

    $out = Join-Path $work 'out'
    $fixtures = Join-Path $work 'fixtures'
    $savedToken = $env:AZURE_DEVOPS_TOKEN
    $env:AZURE_DEVOPS_TOKEN = 'spike-test-token-not-real'
    try {
      $run = & pwsh -NoProfile -NonInteractive -File $measure -Organization contoso-spike -Project Mercury -Repository Atlas -ClonePath $repo `
        -ServiceRoot "http://localhost:$port" -PageSize 2 -FixturePath $fixtures -FixturePulls 1 -OutputPath $out -Execute 2>&1 | Out-String
      $exit = $LASTEXITCODE
    }
    finally { $env:AZURE_DEVOPS_TOKEN = $savedToken }
    Assert-True 'the measurement exits with 0' ($exit -eq 0) $run
    $report = Get-Content -LiteralPath (Join-Path $out 'report.json') -Raw -ErrorAction SilentlyContinue | ConvertFrom-Json
    Assert-True 'one pull request is compared' ($null -ne $report -and $report.pullsCompared -eq 1) $run
    if ($null -ne $report) {
      $pr = $report.pulls[0]
      Assert-True 'path A is read in several pages' ($pr.aPages -ge 3) "($($pr.aPages))"
      Assert-True 'path A gives the file list of git' ($pr.aFilesMissing -eq 0 -and $pr.aFilesExtra -eq 0) ($pr | ConvertTo-Json -Depth 6)
      Assert-True 'path B (stand-in blocks) matches git' ($pr.BVerdict -eq 'match') ($pr.BFiles | ConvertTo-Json)
      Assert-True 'path C (blobs + xdiff) matches git' ($pr.CVerdict -eq 'match') ($pr.CFiles | ConvertTo-Json)
      Assert-True 'the rename and the binary are seen' ($pr.gitRenames -eq 1 -and $pr.gitBinaries -eq 1)
      Assert-True 'the comment ids are read' ($report.sideQuestions.commentIds.threadsWhoseFirstCommentIsOne -eq 2)
      Assert-True 'the pull request of the head is found' ($report.sideQuestions.activePullOfHead[0].commitQueryFindsPull -eq $true)
      Assert-True 'the membership is read' ($report.sideQuestions.collectionAdministrators.membershipStatus -eq 200)
    }
    $written = @(Get-ChildItem -LiteralPath $fixtures, $out -Recurse -File -ErrorAction SilentlyContinue)
    Assert-True 'the fixtures of M7.46 are written' ((Test-Path (Join-Path $fixtures 'diffs/commits.json')) -and (Test-Path (Join-Path $fixtures 'diffs/blob-base.json')) -and (Test-Path (Join-Path $fixtures 'diffs/blob-head.json')) -and (Test-Path (Join-Path $fixtures 'patches/pr-1.diff'))) $run
    $leaks = foreach ($f in $written) {
      $content = Get-Content -LiteralPath $f.FullName -Raw
      foreach ($needle in @('contoso-spike', 'Mercury', 'Atlas', 'Jane Roe', 'jane.roe', 'spike-test-token-not-real', $baseSha, $headSha)) {
        if ($f.Name -like '*.diff' -or $f.Name -like 'blob-*.json' -or $f.Name -like 'filediffs.json' -or $f.Name -like 'iteration-changes.json' -or $f.Name -like 'commits*.json') {
          if ($needle -in @($baseSha, $headSha)) { continue }
        }
        if ($content.IndexOf($needle, [StringComparison]::OrdinalIgnoreCase) -ge 0) { "$($f.Name): $needle" }
      }
    }
    Assert-True 'no written file names the organization, a person, the token or a SHA of the report' (@($leaks).Count -eq 0) ($leaks -join '; ')
    $expected = (& git -C $repo diff -U0 --no-color -M --indent-heuristic $baseSha $headSha) -join "`n"
    $actual = (Get-Content -LiteralPath (Join-Path $fixtures 'patches/pr-1.diff') -Raw -ErrorAction SilentlyContinue)
    Assert-True 'the patch fixture is the git diff of the pull request' ($null -ne $actual -and $actual.TrimEnd() -eq $expected.TrimEnd())
  }
  finally {
    New-Item -ItemType File -Path $stop -Force | Out-Null
    if (-not $mock.WaitForExit(10000)) { $mock.Kill() }
  }
}
finally {
  Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}

if ($failures.Count -gt 0) {
  Write-Host "FAILED ($($failures.Count)):"
  $failures | ForEach-Object { Write-Host "  - $_" }
  exit 1
}
Write-Host 'All checks passed.'
