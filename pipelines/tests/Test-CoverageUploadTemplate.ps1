#Requires -Version 7.2
<#
.SYNOPSIS
  Runs the script of pipelines/templates/coverage-upload.yml offline, against a local release.

.DESCRIPTION
  Extracts the pwsh script of the template, checks that it holds neither a template expression nor a
  macro (it reads only $env: values), publishes the uploader of this machine from the sources as a
  fake release v0.0.0-test served by a local HttpListener, then runs the script as Azure Pipelines
  would, with the env: mapping of the template:
    - a good release with its pinned sum installs the uploader and runs it with --dry-run;
    - a wrong pinned sum, a release whose binary does not match its .sha256, and an unreachable
      server each write a warning and exit 0, and exit 1 with failOnError;
    - an undefined secret (the literal text of its macro) counts as absent.
  No network beyond localhost, no real token. Run by the build-and-test job of ci-cd.yml.

.PARAMETER UploaderPath
  A published uploader of this machine, to skip the dotnet publish.

.EXAMPLE
  pwsh pipelines/tests/Test-CoverageUploadTemplate.ps1
#>
[CmdletBinding()]
param([string] $UploaderPath)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$template = Join-Path $root 'pipelines/templates/coverage-upload.yml'
$failures = [System.Collections.Generic.List[string]]::new()

function Assert-True {
  param([string] $Name, [bool] $Condition, [string] $Detail = '')

  if ($Condition) { Write-Host "  ok: $Name" } else { $failures.Add("$Name $Detail") }
}

# The block of "- pwsh: |", up to the next key of the step.
$lines = Get-Content -LiteralPath $template
$start = [Array]::FindIndex($lines, [Predicate[string]] { param($l) $l -match '^\s*- pwsh: \|\s*$' })
if ($start -lt 0) { throw 'The template has no pwsh step.' }
$indent = ($lines[$start + 1] -replace '^(\s*).*$', '$1').Length
$script = [System.Collections.Generic.List[string]]::new()
for ($i = $start + 1; $i -lt $lines.Length; $i++) {
  $line = $lines[$i]
  if ($line.Trim().Length -gt 0 -and ($line -replace '^(\s*).*$', '$1').Length -lt $indent) { break }
  $script.Add(($line.Length -ge $indent) ? $line.Substring($indent) : '')
}
$text = $script -join "`n"

Write-Host 'Template'
Assert-True 'the script holds no template expression' (-not $text.Contains('${{'))
Assert-True 'the script holds no macro' (-not $text.Contains('$('))
Assert-True 'the token is mapped explicitly' ((Get-Content -LiteralPath $template -Raw) -match 'COVERAGE_TOKEN: \$\(COVERAGE_TOKEN\)')

$work = Join-Path ([System.IO.Path]::GetTempPath()) "template-test-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $work | Out-Null
$scriptPath = Join-Path $work 'coverage-upload.ps1'
Set-Content -LiteralPath $scriptPath -Value $text -Encoding utf8NoBOM

$rid = [System.Runtime.InteropServices.RuntimeInformation]::RuntimeIdentifier
$fileName = if ($rid -eq 'win-x64') { 'coverage-upload-win-x64.exe' } else { "coverage-upload-$rid" }

if (-not $UploaderPath) {
  $publish = Join-Path $work 'publish'
  & dotnet publish (Join-Path $root 'src/CodeCoverage.Uploader/CodeCoverage.Uploader.csproj') -c Release -r $rid --self-contained true `
    -p:PublishSingleFile=true -p:InvariantGlobalization=true -o $publish --nologo -v q | Out-Host
  if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }
  $UploaderPath = Join-Path $publish ($IsWindows ? 'CodeCoverage.Uploader.exe' : 'CodeCoverage.Uploader')
}

$release = Join-Path $work 'release/v0.0.0-test'
New-Item -ItemType Directory -Path $release | Out-Null
Copy-Item -LiteralPath $UploaderPath -Destination (Join-Path $release $fileName)
$sum = (Get-FileHash -LiteralPath (Join-Path $release $fileName) -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath (Join-Path $release "$fileName.sha256") -Value $sum -NoNewline
$tampered = Join-Path $work 'release/v0.0.1-test'
New-Item -ItemType Directory -Path $tampered | Out-Null
Set-Content -LiteralPath (Join-Path $tampered $fileName) -Value 'not the uploader' -NoNewline
Set-Content -LiteralPath (Join-Path $tampered "$fileName.sha256") -Value $sum -NoNewline

# A local release: the listener is started here, synchronously, and served by a thread job.
$listenerSocket = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
$listenerSocket.Start()
$port = $listenerSocket.LocalEndpoint.Port
$listenerSocket.Stop()
$listener = [System.Net.HttpListener]::new()
$listener.Prefixes.Add("http://127.0.0.1:$port/")
$listener.Start()
$serving = Start-ThreadJob -ArgumentList $listener, (Join-Path $work 'release') -ScriptBlock {
  param($Listener, $Root)

  while ($Listener.IsListening) {
    try { $context = $Listener.GetContext() } catch { return }
    $path = Join-Path $Root ($context.Request.Url.AbsolutePath.TrimStart('/'))
    if (Test-Path -LiteralPath $path -PathType Leaf) {
      $bytes = [System.IO.File]::ReadAllBytes($path)
      $context.Response.StatusCode = 200
      $context.Response.OutputStream.Write($bytes, 0, $bytes.Length)
    }
    else {
      $context.Response.StatusCode = 404
    }
    $context.Response.Close()
  }
}

$report = Join-Path $root 'tests/fixtures/reports/cobertura/coverlet-async.xml'

function Invoke-Template {
  param([hashtable] $Overrides)

  # The env: mapping of the template, as Azure Pipelines resolves it; undefined macros keep their text.
  $environment = [ordered]@{
    COVERAGE_UPLOADER_VERSION      = '0.0.0-test'
    COVERAGE_DOWNLOAD_URL          = "http://127.0.0.1:$port"
    COVERAGE_URL                   = 'http://127.0.0.1:9'
    COVERAGE_FILE                  = $report
    COVERAGE_FLAGS                 = 'unit'
    COVERAGE_NAME                  = 'template-test'
    COVERAGE_DRY_RUN               = 'true'
    COVERAGE_FAIL_ON_ERROR         = 'false'
    COVERAGE_TOKEN                 = 'not-a-real-token'
    CODECOV_TOKEN                  = '$(CODECOV_TOKEN)'
    UPLOADER_SHA256_LINUX_X64      = '$(UPLOADER_SHA256_LINUX_X64)'
    UPLOADER_SHA256_LINUX_MUSL_X64 = '$(UPLOADER_SHA256_LINUX_MUSL_X64)'
    UPLOADER_SHA256_WIN_X64        = '$(UPLOADER_SHA256_WIN_X64)'
    UPLOADER_SHA256_OSX_ARM64      = '$(UPLOADER_SHA256_OSX_ARM64)'
    AGENT_TEMPDIRECTORY            = $work
    TF_BUILD                       = $null
  }
  foreach ($key in $Overrides.Keys) { $environment[$key] = $Overrides[$key] }

  $saved = @{}
  foreach ($key in $environment.Keys) {
    $saved[$key] = [Environment]::GetEnvironmentVariable($key)
    [Environment]::SetEnvironmentVariable($key, $environment[$key])
  }

  try {
    $output = & pwsh -NoProfile -NonInteractive -File $scriptPath 2>&1 | Out-String
    return [pscustomobject]@{ Output = $output; ExitCode = $LASTEXITCODE }
  }
  finally {
    foreach ($key in $saved.Keys) { [Environment]::SetEnvironmentVariable($key, $saved[$key]) }
  }
}

$pinnedName = 'UPLOADER_SHA256_' + $rid.ToUpperInvariant().Replace('-', '_')

try {
  Write-Host 'Script'

  $good = Invoke-Template @{ $pinnedName = $sum.ToUpperInvariant() }
  Assert-True 'a good release with its pinned sum exits 0' ($good.ExitCode -eq 0) $good.Output
  Assert-True 'a good release installs the uploader' ($good.Output -match 'CodeCoverage uploader 0\.0\.0-test .* installed') $good.Output
  Assert-True 'the uploader runs (dry run)' ($good.Output -notmatch 'task\.logissue') $good.Output

  $noPin = Invoke-Template @{}
  Assert-True 'an undefined pinned sum only checks the release' ($noPin.ExitCode -eq 0 -and $noPin.Output -match "No $pinnedName in the variables") $noPin.Output

  $wrongPin = Invoke-Template @{ $pinnedName = ('0' * 64) }
  Assert-True 'a wrong pinned sum warns and exits 0' ($wrongPin.ExitCode -eq 0 -and $wrongPin.Output -match 'task\.logissue type=warning.*pinned') $wrongPin.Output

  $badRelease = Invoke-Template @{ COVERAGE_UPLOADER_VERSION = '0.0.1-test' }
  Assert-True 'a binary that does not match its .sha256 warns and exits 0' ($badRelease.ExitCode -eq 0 -and $badRelease.Output -match 'task\.logissue type=warning.*one of the release') $badRelease.Output

  $unreachable = Invoke-Template @{ COVERAGE_DOWNLOAD_URL = 'http://127.0.0.1:1' }
  Assert-True 'an unreachable release warns and exits 0' ($unreachable.ExitCode -eq 0 -and $unreachable.Output -match 'task\.logissue type=warning.*download') $unreachable.Output

  $failing = Invoke-Template @{ COVERAGE_DOWNLOAD_URL = 'http://127.0.0.1:1'; COVERAGE_FAIL_ON_ERROR = 'true' }
  Assert-True 'failOnError fails an unreachable release' ($failing.ExitCode -eq 1 -and $failing.Output -match 'task\.logissue type=error') $failing.Output

  $badVersion = Invoke-Template @{ COVERAGE_UPLOADER_VERSION = '$(UploaderVersion)' }
  Assert-True 'an undefined version macro warns and exits 0' ($badVersion.ExitCode -eq 0 -and $badVersion.Output -match 'not a release version') $badVersion.Output

  Assert-True 'no token value is printed' ((@($good, $noPin, $wrongPin) | ForEach-Object Output) -join "`n" -notmatch 'not-a-real-token')
}
finally {
  $listener.Stop()
  $listener.Close()
  Remove-Job $serving -Force -ErrorAction SilentlyContinue
  Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}

if ($failures.Count -gt 0) {
  Write-Host ''
  $failures | ForEach-Object { Write-Host "FAILED: $_" }
  exit 1
}

Write-Host 'All template checks passed.'
exit 0
