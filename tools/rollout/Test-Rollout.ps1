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

if ($failures.Count -gt 0) {
  Write-Host ''
  $failures | ForEach-Object { Write-Host "FAILED: $_" }
  exit 1
}

Write-Host 'All rollout checks passed.'
exit 0
