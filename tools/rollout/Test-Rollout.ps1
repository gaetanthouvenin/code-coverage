#Requires -Version 7.2
<#
.SYNOPSIS
  Runs the rollout scripts of tools/rollout offline, against a local HTTP listener.

.DESCRIPTION
  No network beyond localhost: the instance is a System.Net.HttpListener on a free local port that
  records each request and answers from a script. Every value is synthetic. Exits with 1 when a check
  fails.

.EXAMPLE
  pwsh tools/rollout/Test-Rollout.ps1
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$failures = [System.Collections.Generic.List[string]]::new()

function Assert-True {
  param([string] $Name, [bool] $Condition, [string] $Detail = '')

  if ($Condition) {
    Write-Host "  ok: $Name"
  }
  else {
    $failures.Add("$Name $Detail")
  }
}

function Get-FreePort {
  $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
  $listener.Start()
  $port = $listener.LocalEndpoint.Port
  $listener.Stop()

  return $port
}

function Start-Instance {
  # A local instance: the listener is started here, synchronously, and served by a thread job. Each request is
  # written to a JSON line file, and answered with the status of the first rule whose path pattern matches.
  param([hashtable[]] $Rules)

  $port = Get-FreePort
  $log = Join-Path ([System.IO.Path]::GetTempPath()) "rollout-$([guid]::NewGuid().ToString('N')).jsonl"
  $listener = [System.Net.HttpListener]::new()
  $listener.Prefixes.Add("http://127.0.0.1:$port/")
  $listener.Start()

  $job = Start-ThreadJob -ArgumentList $listener, $log, $Rules -ScriptBlock {
    param($Listener, $Log, $Rules)

    while ($Listener.IsListening) {
      try {
        $context = $Listener.GetContext()
      }
      catch {
        return
      }

      $request = $context.Request
      $reader = [System.IO.StreamReader]::new($request.InputStream)
      $entry = [ordered]@{
        method        = $request.HttpMethod
        path          = $request.Url.AbsolutePath
        authorization = $request.Headers['Authorization']
        body          = $reader.ReadToEnd()
      }
      Add-Content -LiteralPath $Log -Value ($entry | ConvertTo-Json -Compress)

      $rule = $Rules | Where-Object { $request.Url.AbsolutePath -match $_.Path } | Select-Object -First 1
      $status = if ($rule) { $rule.Status } else { 404 }
      $answer = if ($rule -and $rule.ContainsKey('Body')) { $rule.Body } else { '' }
      $bytes = [System.Text.Encoding]::UTF8.GetBytes($answer)

      $context.Response.StatusCode = $status
      $context.Response.ContentType = 'application/json'
      $context.Response.OutputStream.Write($bytes, 0, $bytes.Length)
      $context.Response.Close()
    }
  }

  return [pscustomobject]@{ Url = "http://127.0.0.1:$port"; Log = $log; Job = $job; Listener = $listener }
}

function Stop-Instance {
  param($Instance)

  $Instance.Listener.Stop()
  $Instance.Listener.Close()
  Remove-Job $Instance.Job -Force -ErrorAction SilentlyContinue
}

function Read-Requests {
  param($Instance)

  if (-not (Test-Path -LiteralPath $Instance.Log)) {
    return @()
  }

  return @(Get-Content -LiteralPath $Instance.Log | ForEach-Object { $_ | ConvertFrom-Json } | Where-Object { $_ })
}

function Invoke-Script {
  # In a child process, with only the given environment variables set: the exit code is part of what is checked.
  param([string] $Name, [string[]] $Arguments, [hashtable] $Environment = @{})

  $saved = @{}
  foreach ($key in 'COVERAGE_API_TOKEN', 'CODECOV_API_TOKEN') {
    $saved[$key] = [Environment]::GetEnvironmentVariable($key)
    [Environment]::SetEnvironmentVariable($key, $Environment[$key])
  }

  try {
    $output = & pwsh -NoProfile -NonInteractive -File (Join-Path $PSScriptRoot $Name) @Arguments 2>&1 | Out-String
    return [pscustomobject]@{ Output = $output; ExitCode = $LASTEXITCODE }
  }
  finally {
    foreach ($key in $saved.Keys) {
      [Environment]::SetEnvironmentVariable($key, $saved[$key])
    }
  }
}

$secrets = @{ COVERAGE_API_TOKEN = 'cvp_synthetic_api_token'; CODECOV_API_TOKEN = 'synthetic-codecov-token' }

Write-Host 'Start-CodecovImport.ps1'

$instance = Start-Instance -Rules @(
  @{ Path = '/repos/github/octo-org/app/codecov-import$'; Status = 202 },
  @{ Path = '/repos/github/octo-org/gone/codecov-import$'; Status = 404; Body = '{"errorCode":"Repository.NotFound"}' }
)

try {
  $dry = Invoke-Script 'Start-CodecovImport.ps1' @('-ApiUrl', $instance.Url, '-Repository', 'octo-org/app') $secrets
  Assert-True 'dry run exits with 0' ($dry.ExitCode -eq 0) $dry.Output
  Assert-True 'dry run sends nothing' (@(Read-Requests $instance).Count -eq 0)
  Assert-True 'dry run names the call' ($dry.Output -match 'dry run: POST .*/api/v1/repos/github/octo-org/app/codecov-import')

  $run = Invoke-Script 'Start-CodecovImport.ps1' @('-ApiUrl', $instance.Url, '-Repository', 'octo-org/app', '-Execute') $secrets
  $requests = @(Read-Requests $instance)
  Assert-True 'execute exits with 0' ($run.ExitCode -eq 0) $run.Output
  Assert-True 'execute sends one POST' ($requests.Count -eq 1 -and $requests[0].method -eq 'POST')
  Assert-True 'execute sends the API token as a bearer' ($requests[0].authorization -eq 'Bearer cvp_synthetic_api_token')
  Assert-True 'execute sends the Codecov token in the body' (($requests[0].body | ConvertFrom-Json).token -eq 'synthetic-codecov-token')
  Assert-True 'execute never prints a token' ($run.Output -notmatch 'synthetic')
  Assert-True 'execute reports the start' ($run.Output -match 'started: octo-org/app')

  $refused = Invoke-Script 'Start-CodecovImport.ps1' @('-ApiUrl', $instance.Url, '-Repository', 'octo-org/gone', '-Execute') $secrets
  Assert-True 'a refusal exits with 1' ($refused.ExitCode -eq 1) $refused.Output
  Assert-True 'a refusal names its error code' ($refused.Output -match 'refused: octo-org/gone \(HTTP 404, Repository.NotFound\)')

  $missing = Invoke-Script 'Start-CodecovImport.ps1' @('-ApiUrl', $instance.Url, '-Repository', 'octo-org/app', '-Execute') @{}
  Assert-True 'execute without tokens fails' ($missing.ExitCode -ne 0)
}
finally {
  Stop-Instance $instance
}

Write-Host 'RolloutTools.psm1'

Import-Module (Join-Path $PSScriptRoot 'RolloutTools.psm1') -Force
$samples = Join-Path $PSScriptRoot 'samples'
$workflows = Join-Path $samples 'workflows'

function Read-Sample([string] $Name) { return [System.IO.File]::ReadAllText((Join-Path $workflows $Name)) }

foreach ($name in 'ci-oidc', 'ci-token') {
  $added = Add-CoverageUploadStep -Text (Read-Sample "$name.yml") -ActionRepository octo-org/code-coverage -Version 1.2.3 -Url https://coverage-api.example.com -Sha256 ('ab' * 32)
  Assert-True "$name gets the upload step after the Codecov one" ($added -ceq (Read-Sample "$name.expected-add.yml"))
  Assert-True "$name is left alone the second time" ($null -eq (Add-CoverageUploadStep -Text $added -ActionRepository octo-org/code-coverage -Version 1.2.3 -Url https://coverage-api.example.com))
}
Assert-True 'a workflow without Codecov is left alone' ($null -eq (Add-CoverageUploadStep -Text (Read-Sample 'lint.yml') -ActionRepository octo-org/code-coverage -Version 1.2.3 -Url https://coverage-api.example.com))
Assert-True 'OIDC when the workflow grants id-token: write' ((Read-Sample 'ci-oidc.expected-add.yml') -notmatch '(?m)^\s+token:')
Assert-True 'the token of the Codecov step is reused' ((Read-Sample 'ci-token.expected-add.yml').TrimEnd().EndsWith('token: ${{ secrets.CODECOV_TOKEN }}'))

function New-Workflow([string] $Head, [string] $Codecov = '') {
  $text = @"
name: Matrix
on: [pull_request]
$Head
    steps:
      - uses: actions/checkout@v6
      - uses: codecov/codecov-action@v5
        with:
          flags: unit
$Codecov
"@
  return $text.TrimEnd()
}

function Get-Added([string] $Text) {
  return Add-CoverageUploadStep -Text $Text -ActionRepository octo-org/code-coverage -Version 1.2.3 -Url https://coverage-api.example.com -Sha256 ('ab' * 32)
}

$commentOnly = Get-Added (New-Workflow "# permissions: id-token: write is not granted here`njobs:`n  test:`n    runs-on: ubuntu-latest")
Assert-True 'a commented id-token: write is no grant' ($commentOnly -match 'token: \$\{\{ secrets\.CODECOV_TOKEN \}\}')
$jobGrant = Get-Added (New-Workflow "permissions:`n  contents: read`njobs:`n  test:`n    runs-on: ubuntu-22.04`n    permissions:`n      id-token: write # for the upload")
Assert-True 'a job granting id-token: write uses OIDC' ($jobGrant -notmatch '(?m)^\s+token:')
$jobDenies = Get-Added (New-Workflow "permissions:`n  id-token: write`njobs:`n  test:`n    runs-on: ubuntu-latest`n    permissions:`n      contents: read")
Assert-True 'the permissions of the job win over the workflow' ($jobDenies -match 'token: \$\{\{ secrets\.CODECOV_TOKEN \}\}')
$ownToken = Get-Added (New-Workflow "jobs:`n  test:`n    runs-on: ubuntu-latest" '          token: ${{ secrets.FRONT_CODECOV }}')
Assert-True 'the token line of the Codecov step is copied' ($ownToken -match '(?m)^\s+token: \$\{\{ secrets\.FRONT_CODECOV \}\}\s*$' -and $ownToken -notmatch 'secrets\.CODECOV_TOKEN')

Assert-True 'the sum is pinned on a plain ubuntu job' ($ownToken -match 'sha256: abab')
foreach ($case in @(
    @{ Name = 'a Windows job'; Head = "jobs:`n  test:`n    runs-on: windows-latest" },
    @{ Name = 'an OS matrix'; Head = "jobs:`n  test:`n    strategy:`n      matrix:`n        os: [ubuntu-latest, macos-latest]`n    runs-on: `${{ matrix.os }}" },
    @{ Name = 'an Alpine container'; Head = "jobs:`n  test:`n    runs-on: ubuntu-latest`n    container: alpine:3.20" },
    @{ Name = 'an ARM runner'; Head = "jobs:`n  test:`n    runs-on: ubuntu-24.04-arm" })) {
  $added = Get-Added (New-Workflow $case.Head)
  Assert-True "no pinned sum on $($case.Name)" ($added -match 'uses: octo-org/code-coverage/action@v1' -and $added -notmatch 'sha256:')
}

# A gh that answers from files: the contents of octo-org/app and the refs of its default branch.
function New-ShimDirectory {
  $directory = Join-Path ([System.IO.Path]::GetTempPath()) "rollout-shim-$([guid]::NewGuid().ToString('N'))"
  New-Item -ItemType Directory -Path $directory | Out-Null

  function Save([string] $Path, $Value) {
    Set-Content -LiteralPath (Join-Path $directory (($Path -replace '/', '__') + '.json')) -Value ($Value | ConvertTo-Json -Depth 8 -Compress)
  }

  $listing = foreach ($name in 'ci-oidc.yml', 'lint.yml') { @{ type = 'file'; name = $name; path = ".github/workflows/$name" } }
  Save 'repos/octo-org/app/contents/.github/workflows' @($listing)
  foreach ($name in 'ci-oidc.yml', 'lint.yml') {
    Save "repos/octo-org/app/contents/.github/workflows/$name" @{ sha = "sha-$name"; content = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes((Read-Sample $name))) }
  }
  Save 'repos/octo-org/app' @{ default_branch = 'main' }
  Save 'repos/octo-org/app/git/ref/heads/main' @{ object = @{ sha = '0123456789abcdef0123456789abcdef01234567' } }

  return $directory
}

function Invoke-WithShim {
  param([string] $Script, [string[]] $Arguments, [scriptblock] $Prepare)

  $directory = New-ShimDirectory
  if ($Prepare) { & $Prepare $directory }
  $log = Join-Path $directory 'calls.jsonl'
  $saved = @{ ROLLOUT_GH = $env:ROLLOUT_GH; ROLLOUT_SHIM_DIR = $env:ROLLOUT_SHIM_DIR; ROLLOUT_SHIM_LOG = $env:ROLLOUT_SHIM_LOG }
  $env:ROLLOUT_GH = Join-Path $samples 'gh-shim.ps1'
  $env:ROLLOUT_SHIM_DIR = $directory
  $env:ROLLOUT_SHIM_LOG = $log
  New-Item -ItemType File -Path $log | Out-Null

  try {
    $output = & pwsh -NoProfile -NonInteractive -File (Join-Path $PSScriptRoot $Script) @Arguments 2>&1 | Out-String
    $calls = @(Get-Content -LiteralPath $log | ForEach-Object { , @($_ | ConvertFrom-Json) })
    return [pscustomobject]@{ Output = $output; ExitCode = $LASTEXITCODE; Calls = $calls }
  }
  finally {
    foreach ($key in $saved.Keys) { [Environment]::SetEnvironmentVariable($key, $saved[$key]) }
    Remove-Item -LiteralPath $directory -Recurse -Force -ErrorAction SilentlyContinue
  }
}

function Test-Write($Calls) { return @($Calls | Where-Object { ($_ -contains '-X') -or (($_[0] -eq 'pr' -and $_[1] -eq 'create')) }).Count }

Write-Host 'Add-CoverageUpload.ps1'

$addArguments = @('-Repository', 'octo-org/app', '-ActionRepository', 'octo-org/code-coverage', '-Version', '1.2.3', '-Url', 'https://coverage-api.example.com', '-Sha256', ('ab' * 32))
$dry = Invoke-WithShim 'Add-CoverageUpload.ps1' $addArguments
Assert-True 'dry run exits with 0' ($dry.ExitCode -eq 0) $dry.Output
Assert-True 'dry run names the changed workflow only' ($dry.Output -match 'would change \.github/workflows/ci-oidc\.yml' -and $dry.Output -notmatch 'lint\.yml') $dry.Output
Assert-True 'dry run writes nothing' ((Test-Write $dry.Calls) -eq 0)

$run = Invoke-WithShim 'Add-CoverageUpload.ps1' ($addArguments + '-Execute')
Assert-True 'execute exits with 0' ($run.ExitCode -eq 0) $run.Output
$branch = @($run.Calls | Where-Object { $_ -contains 'repos/octo-org/app/git/refs' })
Assert-True 'execute creates the branch from the head of main' ($branch.Count -eq 1 -and ($branch[0] -contains 'sha=0123456789abcdef0123456789abcdef01234567'))
$put = @($run.Calls | Where-Object { $_ -contains 'repos/octo-org/app/contents/.github/workflows/ci-oidc.yml' -and $_ -contains 'PUT' })
$content = if ($put.Count -eq 1) { ($put[0] | Where-Object { $_.StartsWith('content=') }) -replace '^content=', '' } else { '' }
Assert-True 'execute writes the edited workflow' ($content -and [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($content)) -ceq (Read-Sample 'ci-oidc.expected-add.yml'))
Assert-True 'execute opens one pull request' (@($run.Calls | Where-Object { ($_[0] -eq 'pr' -and $_[1] -eq 'create') }).Count -eq 1 -and $run.Output -match 'opened: octo-org/app https://github.com/octo-org/app/pull/42') $run.Output

# A batch goes on after a failed repository, and a rerun skips what is done.
$batchFile = Join-Path ([System.IO.Path]::GetTempPath()) "repos-$([guid]::NewGuid().ToString('N')).txt"
@('octo-org/empty', 'octo-org/leftover', 'octo-org/reopened', 'octo-org/app') | Set-Content -LiteralPath $batchFile
$batchShim = {
  param([string] $Directory)
  foreach ($repo in 'leftover', 'reopened') {
    $listing = @(@{ type = 'file'; name = 'ci-oidc.yml'; path = '.github/workflows/ci-oidc.yml' })
    Set-Content -LiteralPath (Join-Path $Directory "repos__octo-org__$($repo)__contents__.github__workflows.json") -Value (ConvertTo-Json -InputObject $listing -Compress)
    $content = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes((Read-Sample 'ci-oidc.yml')))
    Set-Content -LiteralPath (Join-Path $Directory "repos__octo-org__$($repo)__contents__.github__workflows__ci-oidc.yml.json") -Value (@{ sha = 's'; content = $content } | ConvertTo-Json -Compress)
  }
  Set-Content -LiteralPath (Join-Path $Directory 'repos__octo-org__leftover__git__ref__heads__coverage__add-codecoverage-upload.json') -Value '{"object":{"sha":"1"}}'
  Set-Content -LiteralPath (Join-Path $Directory 'pr-list__octo-org__reopened.json') -Value '[{"url":"https://github.com/octo-org/reopened/pull/7"}]'
}
try {
  $batch = Invoke-WithShim 'Add-CoverageUpload.ps1' @('-RepositoriesFile', $batchFile, '-ActionRepository', 'octo-org/code-coverage', '-Version', '1.2.3', '-Url', 'https://coverage-api.example.com', '-Execute') $batchShim
}
finally {
  Remove-Item -LiteralPath $batchFile -Force -ErrorAction SilentlyContinue
}
Assert-True 'a repository without workflows is unchanged' ($batch.Output -match 'unchanged: octo-org/empty \(no \.github/workflows\)') $batch.Output
Assert-True 'a leftover branch fails its repository only' ($batch.Output -match 'failed: octo-org/leftover \(the branch coverage/add-codecoverage-upload exists') $batch.Output
Assert-True 'an open pull request of an earlier run is kept' ($batch.Output -match 'already open: octo-org/reopened https://github.com/octo-org/reopened/pull/7') $batch.Output
Assert-True 'the batch goes on after a failure' ($batch.Output -match 'opened: octo-org/app') $batch.Output
Assert-True 'a failed repository makes the batch exit with 1' ($batch.ExitCode -eq 1) $batch.Output
Assert-True 'nothing is written for the skipped repositories' (@($batch.Calls | Where-Object { ($_ -contains '-X') -and (($_ -join ' ') -match 'octo-org/(leftover|reopened|empty)') }).Count -eq 0)

Write-Host 'Import-CodecovTokens.ps1'

$csv = Join-Path ([System.IO.Path]::GetTempPath()) "tokens-$([guid]::NewGuid().ToString('N')).csv"
@(
  'scope,owner,repo,label,token',
  'organization,octo-org,,codecov-org,synthetic-organization-value',
  'repository,octo-org,app,codecov-app,synthetic-repository-value',
  'repository,octo-org,again,codecov-again,synthetic-again-value'
) | Set-Content -LiteralPath $csv

$tokens = Start-Instance -Rules @(
  @{ Path = '/orgs/github/octo-org/upload-tokens$'; Status = 201; Body = '{"id":1}' },
  @{ Path = '/repos/github/octo-org/app/upload-tokens$'; Status = 201; Body = '{"id":2}' },
  @{ Path = '/repos/github/octo-org/again/upload-tokens$'; Status = 409; Body = '{"errorCode":"UploadToken.AlreadyImported"}' }
)

try {
  $dryTokens = Invoke-Script 'Import-CodecovTokens.ps1' @('-ApiUrl', $tokens.Url, '-CsvPath', $csv) $secrets
  Assert-True 'dry run lists the three imports' ($dryTokens.ExitCode -eq 0 -and ([regex]::Matches($dryTokens.Output, 'dry run: import')).Count -eq 3) $dryTokens.Output
  Assert-True 'dry run sends nothing' (@(Read-Requests $tokens).Count -eq 0)

  $runTokens = Invoke-Script 'Import-CodecovTokens.ps1' @('-ApiUrl', $tokens.Url, '-CsvPath', $csv, '-Execute') $secrets
  $sent = @(Read-Requests $tokens)
  Assert-True 'execute exits with 0, a 409 being done' ($runTokens.ExitCode -eq 0) $runTokens.Output
  Assert-True 'execute sends the imported value and the label' ($sent.Count -eq 3 -and ($sent[0].body | ConvertFrom-Json).importedValue -eq 'synthetic-organization-value' -and ($sent[0].body | ConvertFrom-Json).label -eq 'codecov-org')
  Assert-True 'execute sends the API token as a bearer' (@($sent | Where-Object { $_.authorization -ne 'Bearer cvp_synthetic_api_token' }).Count -eq 0)
  Assert-True 'a 409 is reported as already imported' ($runTokens.Output -match "already imported: 'codecov-again'") $runTokens.Output
  Assert-True 'no token value is printed' ($runTokens.Output -notmatch 'synthetic-' -and $dryTokens.Output -notmatch 'synthetic-')
}
finally {
  Stop-Instance $tokens
  Remove-Item -LiteralPath $csv -Force -ErrorAction SilentlyContinue
}

Write-Host 'Remove-CodecovUpload.ps1'

foreach ($name in 'ci-oidc', 'ci-token') {
  Assert-True "$name loses its Codecov step only" ((Remove-CodecovUploadStep -Text (Read-Sample "$name.expected-add.yml")) -ceq (Read-Sample "$name.expected-remove.yml"))
}
Assert-True 'a workflow without Codecov is left alone by the removal' ($null -eq (Remove-CodecovUploadStep -Text (Read-Sample 'lint.yml')))

# The pilot state: ci-oidc.yml already carries both uploads.
$afterPilot = {
  param([string] $Directory)
  $content = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes((Read-Sample 'ci-oidc.expected-add.yml')))
  Set-Content -LiteralPath (Join-Path $Directory 'repos__octo-org__app__contents__.github__workflows__ci-oidc.yml.json') -Value (@{ sha = 'sha-ci-oidc.yml'; content = $content } | ConvertTo-Json -Compress)
}

$dryRemove = Invoke-WithShim 'Remove-CodecovUpload.ps1' @('-Repository', 'octo-org/app') $afterPilot
Assert-True 'removal dry run exits with 0' ($dryRemove.ExitCode -eq 0) $dryRemove.Output
Assert-True 'removal dry run names the changed workflow only' ($dryRemove.Output -match 'would change \.github/workflows/ci-oidc\.yml' -and $dryRemove.Output -notmatch 'lint\.yml') $dryRemove.Output
Assert-True 'removal dry run writes nothing' ((Test-Write $dryRemove.Calls) -eq 0)

$runRemove = Invoke-WithShim 'Remove-CodecovUpload.ps1' @('-Repository', 'octo-org/app', '-Execute') $afterPilot
Assert-True 'removal execute exits with 0' ($runRemove.ExitCode -eq 0) $runRemove.Output
$putRemove = @($runRemove.Calls | Where-Object { $_ -contains 'repos/octo-org/app/contents/.github/workflows/ci-oidc.yml' -and $_ -contains 'PUT' })
$removed = if ($putRemove.Count -eq 1) { ($putRemove[0] | Where-Object { $_.StartsWith('content=') }) -replace '^content=', '' } else { '' }
Assert-True 'removal execute writes the workflow without Codecov' ($removed -and [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($removed)) -ceq (Read-Sample 'ci-oidc.expected-remove.yml'))
Assert-True 'removal execute opens one pull request' (@($runRemove.Calls | Where-Object { ($_[0] -eq 'pr' -and $_[1] -eq 'create') }).Count -eq 1) $runRemove.Output

$removeFile = Join-Path ([System.IO.Path]::GetTempPath()) "repos-$([guid]::NewGuid().ToString('N')).txt"
@('octo-org/empty', 'octo-org/app') | Set-Content -LiteralPath $removeFile
try {
  $removeBatch = Invoke-WithShim 'Remove-CodecovUpload.ps1' @('-RepositoriesFile', $removeFile, '-Execute') $afterPilot
}
finally {
  Remove-Item -LiteralPath $removeFile -Force -ErrorAction SilentlyContinue
}
Assert-True 'removal skips a repository without workflows and goes on' ($removeBatch.ExitCode -eq 0 -and $removeBatch.Output -match 'unchanged: octo-org/empty' -and $removeBatch.Output -match 'opened: octo-org/app') $removeBatch.Output

Write-Host 'Test-BranchProtection.ps1'

# $null classic protection: GitHub's "Branch not protected"; $null rules: an empty list.
function New-ProtectionShim([object] $Classic, [object] $Rules) {
  return {
    param([string] $Directory)
    $classicFile = Join-Path $Directory 'repos__octo-org__app__branches__main__protection__required_status_checks.json'
    if ($null -ne $Classic) {
      Set-Content -LiteralPath $classicFile -Value ($Classic | ConvertTo-Json -Depth 8 -Compress)
    }
    else {
      Set-Content -LiteralPath ([System.IO.Path]::ChangeExtension($classicFile, '.error')) -Value 'gh: Branch not protected (HTTP 404)'
    }
    Set-Content -LiteralPath (Join-Path $Directory 'repos__octo-org__app__rules__branches__main.json') -Value (ConvertTo-Json -InputObject @($Rules | Where-Object { $null -ne $_ }) -Depth 8 -Compress)
  }.GetNewClosure()
}

$anyApp = New-ProtectionShim @{ checks = @(@{ context = 'codecov/project'; app_id = $null }, @{ context = 'build'; app_id = 15368 }) } @(
  @{ type = 'required_status_checks'; parameters = @{ required_status_checks = @(@{ context = 'codecov/patch' }) } })
$open = Invoke-WithShim 'Test-BranchProtection.ps1' @('-Repository', 'octo-org/app', '-ExpectedAppId', '4242') $anyApp
Assert-True 'checks of any application are satisfied' ($open.ExitCode -eq 0 -and $open.Output -match 'codecov/project\s+ok \(any application\)' -and $open.Output -match 'codecov/patch\s+ok \(any application\)') $open.Output
Assert-True 'checks outside the prefix are not judged' ($open.Output -notmatch '\bbuild\b') $open.Output
Assert-True 'the protection check only reads' ((Test-Write $open.Calls) -eq 0)

$ours = New-ProtectionShim @{ checks = @(@{ context = 'codecov/project'; app_id = 4242 }) } $null
$mine = Invoke-WithShim 'Test-BranchProtection.ps1' @('-Repository', 'octo-org/app', '-ExpectedAppId', '4242') $ours
Assert-True 'a check pinned to the App of CodeCoverage is satisfied' ($mine.ExitCode -eq 0 -and $mine.Output -match 'ok \(CodeCoverage\)') $mine.Output

$codecov = New-ProtectionShim $null @(@{ type = 'required_status_checks'; parameters = @{ required_status_checks = @(@{ context = 'codecov/patch'; integration_id = 254 }) } })
$pinnedRun = Invoke-WithShim 'Test-BranchProtection.ps1' @('-Repository', 'octo-org/app', '-ExpectedAppId', '4242') $codecov
Assert-True 'a ruleset pinned to another application exits with 1' ($pinnedRun.ExitCode -eq 1 -and $pinnedRun.Output -match 'pinned to application 254') $pinnedRun.Output

$bare = Invoke-WithShim 'Test-BranchProtection.ps1' @('-Repository', 'octo-org/app') (New-ProtectionShim $null $null)
Assert-True 'an unprotected branch is reported, not failed' ($bare.ExitCode -eq 0 -and $bare.Output -match 'no required codecov/\* check') $bare.Output

# A token without admin access: GitHub answers 404 Not Found, which is no all-clear.
$hidden = Invoke-WithShim 'Test-BranchProtection.ps1' @('-Repository', 'octo-org/app') { param([string] $Directory) }
Assert-True 'an unreadable protection is unknown and exits with 1' ($hidden.ExitCode -eq 1 -and $hidden.Output -match 'unknown: not readable' -and $hidden.Output -notmatch 'no required') $hidden.Output

if ($failures.Count -gt 0) {
  Write-Host ''
  $failures | ForEach-Object { Write-Host "FAILED: $_" }
  exit 1
}

Write-Host 'All rollout checks passed.'
exit 0
