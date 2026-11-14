#Requires -Version 7.2
<#
.SYNOPSIS
  Copies the standalone uploader binaries out of an Api image and checks each one against its .sha256.

.DESCRIPTION
  The release (M4.01) publishes the very files the Api serves on /api/v1/uploader/{rid}: they are
  extracted from the Api image built for the same version, never built a second time (a second
  build could differ byte for byte, and its sums with it).

  `docker create` makes a stopped container of the image, `docker cp` copies /app/uploader out of it,
  and the container is removed. Each of the 4 runtimes of UploaderFileCatalog must be there with its
  `.sha256`, which must hold the lower-case hexadecimal SHA-256 of the binary. Exits with 1 otherwise.

  Run by the compose-smoke job on the image it builds (a dry run, on every pull request) and by the
  release job on the image pushed for the tag.

.PARAMETER Image
  The Api image, e.g. code-coverage-api:smoke or ghcr.io/<owner>/code-coverage-api:1.2.3.

.PARAMETER Destination
  The directory the files are copied to; created, and emptied first.

.EXAMPLE
  pwsh scripts/Export-Uploader.ps1 -Image code-coverage-api:smoke -Destination artifacts/uploader
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory)]
  [string] $Image,
  [Parameter(Mandatory)]
  [string] $Destination
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# The names of UploaderFileCatalog (src/CodeCoverage.Api/Configuration/Uploader/UploaderFileCatalog.cs).
$expected = 'coverage-upload-linux-x64', 'coverage-upload-linux-musl-x64', 'coverage-upload-win-x64.exe', 'coverage-upload-osx-arm64'

if (Test-Path -LiteralPath $Destination) {
  Remove-Item -LiteralPath $Destination -Recurse -Force
}

New-Item -ItemType Directory -Path $Destination | Out-Null

$container = (& docker create $Image).Trim()

if ($LASTEXITCODE -ne 0 -or -not $container) {
  throw "docker create failed for '$Image'."
}

try {
  $staging = Join-Path ([System.IO.Path]::GetTempPath()) "uploader-$([guid]::NewGuid().ToString('N'))"
  & docker cp "${container}:/app/uploader" $staging

  if ($LASTEXITCODE -ne 0) {
    throw "docker cp of /app/uploader failed for '$Image'."
  }

  Get-ChildItem -LiteralPath $staging -File | Copy-Item -Destination $Destination
  Remove-Item -LiteralPath $staging -Recurse -Force
}
finally {
  & docker rm $container | Out-Null
}

$failures = [System.Collections.Generic.List[string]]::new()

foreach ($name in $expected) {
  $binary = Join-Path $Destination $name
  $sumFile = "$binary.sha256"

  if (-not (Test-Path -LiteralPath $binary) -or -not (Test-Path -LiteralPath $sumFile)) {
    $failures.Add("$name or its .sha256 is missing from /app/uploader.")
    continue
  }

  $published = (Get-Content -LiteralPath $sumFile -Raw).Trim()
  $actual = (Get-FileHash -LiteralPath $binary -Algorithm SHA256).Hash.ToLowerInvariant()

  if ($published -cnotmatch '^[0-9a-f]{64}$') {
    $failures.Add("$name.sha256 is not a lower-case SHA-256: '$published'.")
  }
  elseif ($published -cne $actual) {
    $failures.Add("$name does not match its .sha256 ($published, actual $actual).")
  }
  else {
    Write-Output "ok: $name $actual"
  }
}

$unexpected = Get-ChildItem -LiteralPath $Destination -File |
  Where-Object { $_.Name -notin $expected -and $_.Name -notin ($expected | ForEach-Object { "$_.sha256" }) }

foreach ($file in $unexpected) {
  $failures.Add("Unexpected file in /app/uploader: $($file.Name).")
}

if ($failures.Count -gt 0) {
  $failures | ForEach-Object { Write-Output "FAILED: $_" }
  exit 1
}

Write-Output "The $($expected.Count) uploader binaries of '$Image' match their sums."
