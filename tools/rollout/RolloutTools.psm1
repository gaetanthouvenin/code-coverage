# ------------------------------------------------------------------------------------------------
# RolloutTools - the functions shared by the rollout scripts of tools/rollout (M4.07, M4.08).
# Workflow files are edited as text, line by line: the YAML is never reformatted, so a pull request
# changes only the lines of the step it adds or removes. gh is called through Invoke-Gh, which the
# offline self-test points to a shim (ROLLOUT_GH).
# ------------------------------------------------------------------------------------------------
Set-StrictMode -Version Latest

function Invoke-Gh {
  <#
  .SYNOPSIS
    Runs gh (or the shim named by ROLLOUT_GH) and returns its standard output; throws on a failure.
  #>
  param([Parameter(ValueFromRemainingArguments)] [string[]] $Arguments)

  $gh = if ($env:ROLLOUT_GH) { $env:ROLLOUT_GH } else { 'gh' }
  $output = if ($gh.EndsWith('.ps1')) { & pwsh -NoProfile -File $gh @Arguments 2>&1 } else { & $gh @Arguments 2>&1 }

  if ($LASTEXITCODE -ne 0) {
    throw "gh $($Arguments -join ' ') failed: $($output -join ' ')"
  }

  return ($output | Out-String).TrimEnd()
}

function Get-StepRange {
  <#
  .SYNOPSIS
    Finds the step of a workflow that uses an action, and gives its first and last line (0-based).
  .OUTPUTS
    @{ Start; End; Indent } or $null when no step uses the action.
  #>
  param([string[]] $Lines, [string] $Action)

  for ($i = 0; $i -lt $Lines.Length; $i++) {
    if ($Lines[$i] -notmatch "^\s*(-\s+)?uses:\s*['""]?$([regex]::Escape($Action))@") { continue }

    # The step starts at the nearest line above (or this one) that opens a list item.
    $start = $i
    while ($start -ge 0 -and $Lines[$start] -notmatch '^\s*-\s') { $start-- }
    if ($start -lt 0) { continue }

    $indent = ($Lines[$start] -replace '^(\s*)-.*$', '$1').Length
    $end = $i
    for ($j = $start + 1; $j -lt $Lines.Length; $j++) {
      $line = $Lines[$j]
      if ($line.Trim().Length -eq 0) { continue }
      $lineIndent = ($line -replace '^(\s*).*$', '$1').Length
      if ($lineIndent -lt $indent -or ($lineIndent -eq $indent -and $line.TrimStart().StartsWith('-'))) { break }
      $end = $j
    }

    return @{ Start = $start; End = $end; Indent = $indent }
  }

  return $null
}

function Get-Indent([string] $Line) { return ($Line -replace '^(\s*).*$', '$1').Length }

function Remove-YamlComment([string] $Line) {
  # A comment starts at a # that begins the line or follows a space; quoted # are rare in these keys.
  if ($Line -match '^\s*#') { return '' }
  return ($Line -replace '\s+#.*$', '')
}

function Get-YamlBlock {
  <#
  .SYNOPSIS
    The lines nested under the key of line $Index (more indented, blank and comment lines skipped).
  #>
  param([string[]] $Lines, [int] $Index)

  $indent = Get-Indent $Lines[$Index]
  $block = [System.Collections.Generic.List[string]]::new()
  for ($j = $Index + 1; $j -lt $Lines.Length; $j++) {
    $line = Remove-YamlComment $Lines[$j]
    if ($line.Trim().Length -eq 0) { continue }
    if ((Get-Indent $line) -le $indent) { break }
    $block.Add($line)
  }
  return $block.ToArray()
}

function Get-JobOfLine {
  <#
  .SYNOPSIS
    The lines of the job (under jobs:) that holds line $Index, comments removed, and their indent.
  .OUTPUTS
    @{ Start; End; KeyIndent } or $null.
  #>
  param([string[]] $Lines, [int] $Index)

  $jobs = -1
  for ($i = 0; $i -lt $Index; $i++) { if ($Lines[$i] -match '^jobs:\s*$') { $jobs = $i } }
  if ($jobs -lt 0) { return $null }

  $keyIndent = -1
  $start = -1
  for ($i = $jobs + 1; $i -le $Index; $i++) {
    $line = Remove-YamlComment $Lines[$i]
    if ($line.Trim().Length -eq 0) { continue }
    if ($keyIndent -lt 0) { $keyIndent = Get-Indent $line }
    if ((Get-Indent $line) -eq $keyIndent) { $start = $i }
  }
  if ($start -lt 0) { return $null }

  $end = $Lines.Length - 1
  for ($i = $Index + 1; $i -lt $Lines.Length; $i++) {
    $line = Remove-YamlComment $Lines[$i]
    if ($line.Trim().Length -eq 0) { continue }
    if ((Get-Indent $line) -le $keyIndent) { $end = $i - 1; break }
  }

  return @{ Start = $start; End = $end; KeyIndent = $keyIndent }
}

function Find-JobKey {
  <#
  .SYNOPSIS
    The index of a direct key of the job (runs-on, permissions, container, strategy), or -1.
  #>
  param([string[]] $Lines, $Job, [string] $Key)

  $childIndent = -1
  for ($i = $Job.Start + 1; $i -le $Job.End; $i++) {
    $line = Remove-YamlComment $Lines[$i]
    if ($line.Trim().Length -eq 0) { continue }
    if ($childIndent -lt 0) { $childIndent = Get-Indent $line }
    if ((Get-Indent $line) -eq $childIndent -and $line -match "^\s*$([regex]::Escape($Key)):") { return $i }
  }
  return -1
}

function Test-GrantsIdToken {
  <#
  .SYNOPSIS
    Whether a permissions key (line $Index) grants id-token: write: write-all, an inline map or a block.
  #>
  param([string[]] $Lines, [int] $Index)

  $inline = (Remove-YamlComment $Lines[$Index]) -replace '^\s*permissions:\s*', ''
  if ($inline -match '^write-all\s*$') { return $true }
  if ($inline -match 'id-token\s*:\s*write') { return $true }
  if ($inline.Trim().Length -gt 0) { return $false }
  return [bool] (Get-YamlBlock $Lines $Index | Where-Object { $_ -match '^\s*id-token\s*:\s*write\s*$' })
}

function Test-PlainLinuxX64Job {
  <#
  .SYNOPSIS
    Whether the job runs on a GitHub-hosted linux-x64 runner without a container: the only case where
    the pinned sum (the sum of coverage-upload-linux-x64) applies.
  #>
  param([string[]] $Lines, $Job)

  if ((Find-JobKey $Lines $Job 'container') -ge 0) { return $false }
  $runsOn = Find-JobKey $Lines $Job 'runs-on'
  if ($runsOn -lt 0) { return $false }
  $value = ((Remove-YamlComment $Lines[$runsOn]) -replace '^\s*runs-on:\s*', '').Trim().Trim('"', "'")
  return $value -match '^ubuntu-(latest|\d{2}\.\d{2})$'
}

function Add-CoverageUploadStep {
  <#
  .SYNOPSIS
    Adds the CodeCoverage upload step after the codecov/codecov-action step of a workflow.
  .DESCRIPTION
    Returns the new text, or $null when the workflow has no Codecov step or already uploads to
    CodeCoverage. The credential: the token: line of the Codecov step when it has one (an imported
    CODECOV_TOKEN works as an upload token); otherwise OIDC when the job grants id-token: write (or the
    workflow does and the job sets no permissions), comments ignored; otherwise, when unsure, the
    CODECOV_TOKEN secret. The pinned sum is the one of coverage-upload-linux-x64: it is written only
    when the job plainly runs on an ubuntu runner without a container; elsewhere the action still
    checks the .sha256 of the release.
  #>
  param(
    [Parameter(Mandatory)] [string] $Text,
    [Parameter(Mandatory)] [string] $ActionRepository,
    [Parameter(Mandatory)] [string] $Version,
    [Parameter(Mandatory)] [string] $Url,
    [string] $Sha256
  )

  if ($Text -match "uses:\s*['""]?$([regex]::Escape($ActionRepository))/action@") { return $null }

  $newline = $Text.Contains("`r`n") ? "`r`n" : "`n"
  $lines = $Text -split "\r?\n"
  $range = Get-StepRange $lines 'codecov/codecov-action'
  if ($null -eq $range) { return $null }

  $pad = ' ' * $range.Indent
  $job = Get-JobOfLine $lines $range.Start
  $with = @(
    "$pad  with:",
    "$pad    version: $Version"
  )
  if ($Sha256 -and $null -ne $job -and (Test-PlainLinuxX64Job $lines $job)) { $with += "$pad    sha256: $Sha256" }
  $with += "$pad    url: $Url"

  # The flags and files of the Codecov step are kept, with their names in the action.
  $codecov = $lines[$range.Start..$range.End]
  foreach ($pair in @(@('flags', 'flags'), @('files', 'files'), @('file', 'files'), @('name', 'name'))) {
    $found = $codecov | Where-Object { $_ -match "^\s+$($pair[0]):\s*(.+)$" } | Select-Object -First 1
    if ($found -and $found -match "^\s+$($pair[0]):\s*(.+)$") { $with += "$pad    $($pair[1]): $($Matches[1].Trim())" }
  }

  $tokenLine = $codecov | ForEach-Object { Remove-YamlComment $_ } | Where-Object { $_ -match '^\s+token:\s*(.+)$' } | Select-Object -First 1
  if ($tokenLine -and $tokenLine -match '^\s+token:\s*(.+)$') {
    $with += "$pad    token: $($Matches[1].Trim())"
  }
  else {
    $oidc = $false
    $jobPermissions = if ($null -ne $job) { Find-JobKey $lines $job 'permissions' } else { -1 }
    if ($jobPermissions -ge 0) {
      $oidc = Test-GrantsIdToken $lines $jobPermissions
    }
    else {
      for ($i = 0; $i -lt $lines.Length; $i++) {
        if ($lines[$i] -match '^permissions:') { $oidc = Test-GrantsIdToken $lines $i; break }
      }
    }
    if (-not $oidc) { $with += "$pad    token: `${{ secrets.CODECOV_TOKEN }}" }
  }

  $step = @("$pad- name: Upload coverage to CodeCoverage", "$pad  if: `${{ !cancelled() }}", "$pad  uses: $ActionRepository/action@v1") + $with
  $result = [System.Collections.Generic.List[string]]::new()
  $result.AddRange([string[]] $lines[0..$range.End])
  $result.Add('')
  $result.AddRange([string[]] $step)
  if ($range.End + 1 -lt $lines.Length) { $result.AddRange([string[]] $lines[($range.End + 1)..($lines.Length - 1)]) }

  return $result -join $newline
}

function Remove-CodecovUploadStep {
  <#
  .SYNOPSIS
    Removes every codecov/codecov-action step of a workflow; returns $null when there is none.
  #>
  param([Parameter(Mandatory)] [string] $Text)

  $newline = $Text.Contains("`r`n") ? "`r`n" : "`n"
  $lines = [System.Collections.Generic.List[string]]::new([string[]] ($Text -split "\r?\n"))
  $changed = $false

  while ($true) {
    $range = Get-StepRange $lines.ToArray() 'codecov/codecov-action'
    if ($null -eq $range) { break }

    $count = $range.End - $range.Start + 1
    # One blank line before the step goes with it, so that no double blank line is left.
    if ($range.Start -gt 0 -and $lines[$range.Start - 1].Trim().Length -eq 0) {
      $lines.RemoveRange($range.Start - 1, $count + 1)
    }
    else {
      $lines.RemoveRange($range.Start, $count)
    }
    $changed = $true
  }

  return $changed ? ($lines -join $newline) : $null
}

function Get-RepositoryList {
  <#
  .SYNOPSIS
    Reads owner/name repositories from parameters and an optional file (# comments and blank lines skipped).
  #>
  param([string[]] $Repository, [string] $RepositoriesFile)

  $names = [System.Collections.Generic.List[string]]::new()
  $Repository | Where-Object { $_ } | ForEach-Object { $names.Add($_.Trim()) }
  if ($RepositoriesFile) {
    Get-Content -LiteralPath $RepositoriesFile | ForEach-Object { $_.Trim() } | Where-Object { $_ -and -not $_.StartsWith('#') } | ForEach-Object { $names.Add($_) }
  }
  foreach ($name in $names) {
    if ($name -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$') { throw "Not an owner/name: '$name'." }
  }
  if ($names.Count -eq 0) { throw 'Give at least one repository, with -Repository or -RepositoriesFile.' }

  return $names.ToArray()
}

function Invoke-ForEachRepository {
  <#
  .SYNOPSIS
    Runs an action per repository; a failure is reported and the batch goes on. Returns the number of
    failed repositories.
  #>
  param([Parameter(Mandatory)] [string[]] $Names, [Parameter(Mandatory)] [scriptblock] $Action)

  $failed = 0
  foreach ($name in $Names) {
    try {
      # The lines of the action are printed, not returned: the function returns the count only.
      & $Action $name | ForEach-Object { Write-Host $_ }
    }
    catch {
      $failed++
      Write-Host "failed: $name ($($_.Exception.Message))"
    }
  }
  return $failed
}

function Update-WorkflowsByPullRequest {
  <#
  .SYNOPSIS
    Applies an edit to the workflows of a repository and opens one pull request with the changed files.
  .DESCRIPTION
    Reads .github/workflows through the contents API, applies the edit to each file, and, unless
    -Execute is absent (a dry run, which only prints what would change), creates the branch from the
    default branch, writes each changed file on it, and opens the pull request. Returns a summary line.
  #>
  param(
    [Parameter(Mandatory)] [string] $Name,
    [Parameter(Mandatory)] [scriptblock] $Edit,
    [Parameter(Mandatory)] [string] $Branch,
    [Parameter(Mandatory)] [string] $Title,
    [Parameter(Mandatory)] [string] $Body,
    [switch] $Execute
  )

  try {
    $listing = Invoke-Gh api "repos/$Name/contents/.github/workflows" | ConvertFrom-Json
  }
  catch {
    if ($_.Exception.Message -match 'Not Found|\(HTTP 404\)') { return "unchanged: $Name (no .github/workflows)" }
    throw
  }
  $changes = [System.Collections.Generic.List[object]]::new()

  foreach ($entry in @($listing) | Where-Object { $_.type -eq 'file' -and $_.name -match '\.ya?ml$' }) {
    $file = Invoke-Gh api "repos/$Name/contents/$($entry.path)" | ConvertFrom-Json
    $text = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String(($file.content -replace '\s', '')))
    $edited = & $Edit $text
    if ($null -ne $edited -and $edited -cne $text) {
      $changes.Add([pscustomobject]@{ Path = $entry.path; Sha = $file.sha; Text = $edited })
    }
  }

  if ($changes.Count -eq 0) { return "unchanged: $Name (nothing to edit)" }

  if (-not $Execute) {
    foreach ($change in $changes) { Write-Output "dry run: $Name would change $($change.Path)" }
    return "dry run: $Name, $($changes.Count) file(s), pull request '$Title' not opened"
  }

  # A rerun: the pull request of an earlier run is still open, or its branch is left over.
  $open = @(Invoke-Gh pr list --repo $Name --head $Branch --state open --json url | ConvertFrom-Json)
  if ($open.Count -gt 0) { return "already open: $Name $($open[0].url)" }
  $leftover = $true
  try { Invoke-Gh api "repos/$Name/git/ref/heads/$Branch" | Out-Null }
  catch {
    if ($_.Exception.Message -notmatch 'Not Found|\(HTTP 404\)') { throw }
    $leftover = $false
  }
  if ($leftover) { throw "the branch $Branch exists without an open pull request: delete it, or give another -Branch." }

  $repository = Invoke-Gh api "repos/$Name" | ConvertFrom-Json
  $default = $repository.default_branch
  $head = Invoke-Gh api "repos/$Name/git/ref/heads/$default" | ConvertFrom-Json
  Invoke-Gh api -X POST "repos/$Name/git/refs" -f "ref=refs/heads/$Branch" -f "sha=$($head.object.sha)" | Out-Null

  foreach ($change in $changes) {
    $content = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($change.Text))
    Invoke-Gh api -X PUT "repos/$Name/contents/$($change.Path)" -f "message=$Title" -f "content=$content" -f "sha=$($change.Sha)" -f "branch=$Branch" | Out-Null
  }

  $url = Invoke-Gh pr create --repo $Name --base $default --head $Branch --title $Title --body $Body
  return "opened: $Name $url"
}

Export-ModuleMember -Function Invoke-Gh, Get-StepRange, Add-CoverageUploadStep, Remove-CodecovUploadStep, Get-RepositoryList, Invoke-ForEachRepository, Update-WorkflowsByPullRequest
