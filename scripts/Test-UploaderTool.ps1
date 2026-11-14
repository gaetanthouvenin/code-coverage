#Requires -Version 7.2
<#
.SYNOPSIS
  Packs the dotnet tool coverage-upload, installs it from a local folder into an empty tool path, and
  runs it (M5.03).

.DESCRIPTION
  The package must be CodeCoverage.Uploader, licensed Apache-2.0, with the given version: the NuGet
  version of GitVersion, read from the .nuspec of the package (the --version of the tool prints the
  informational version, which carries +sha). The tool is installed from the output folder alone (a
  temporary nuget.config that clears every other source). Nothing is published. Exits with 1 when a
  check fails.

.PARAMETER Version
  The NuGet version of the package (GitVersion SemVer).

.PARAMETER AssemblySemVer
  The assembly version, as GitVersion gives it.

.PARAMETER MajorMinorPatch
  The file version prefix, as GitVersion gives it.

.PARAMETER InformationalVersion
  The informational version, as GitVersion gives it.

.PARAMETER Output
  The folder the .nupkg is written to.

.EXAMPLE
  pwsh scripts/Test-UploaderTool.ps1 -Version 0.1.0-test -Output nupkg
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory)]
  [string] $Version,
  [string] $AssemblySemVer = '1.0.0.0',
  [string] $MajorMinorPatch = '1.0.0',
  [string] $InformationalVersion = '',
  [Parameter(Mandatory)]
  [string] $Output
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root 'src/CodeCoverage.Uploader/CodeCoverage.Uploader.csproj'
if (-not $InformationalVersion) { $InformationalVersion = $Version }

if (Test-Path -LiteralPath $Output) { Remove-Item -LiteralPath $Output -Recurse -Force }

& dotnet pack $project -c Release -o $Output --nologo -v q `
  "-p:NuGetVersion=$Version" "-p:AssemblySemVer=$AssemblySemVer" "-p:MajorMinorPatch=$MajorMinorPatch" "-p:InformationalVersion=$InformationalVersion"
if ($LASTEXITCODE -ne 0) { throw 'dotnet pack failed.' }

$package = Get-ChildItem -LiteralPath $Output -Filter '*.nupkg' | Select-Object -First 1
if (-not $package) { throw 'No .nupkg was written.' }

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($package.FullName)
try {
  $entry = $archive.Entries | Where-Object { $_.FullName -like '*.nuspec' } | Select-Object -First 1
  $reader = [System.IO.StreamReader]::new($entry.Open())
  [xml] $nuspec = $reader.ReadToEnd()
  $reader.Dispose()
  $hasReadme = [bool] ($archive.Entries | Where-Object { $_.FullName -eq 'README.md' })
}
finally {
  $archive.Dispose()
}

$metadata = $nuspec.package.metadata
$failures = [System.Collections.Generic.List[string]]::new()
if ($metadata.id -cne 'CodeCoverage.Uploader') { $failures.Add("The package is $($metadata.id), not CodeCoverage.Uploader.") }
if ($metadata.version -cne $Version) { $failures.Add("The package version is $($metadata.version), not $Version.") }
if ($metadata.license.'#text' -cne 'Apache-2.0') { $failures.Add('The package is not licensed Apache-2.0.') }
if (-not $hasReadme) { $failures.Add('The package has no README.md.') }

$toolPath = Join-Path ([System.IO.Path]::GetTempPath()) "coverage-upload-tool-$([guid]::NewGuid().ToString('N'))"
# The local folder is the only source: no nuget.org, so that a package of the same id and version
# published there by someone else can never be installed (the publish job holds id-token: write).
$config = Join-Path ([System.IO.Path]::GetTempPath()) "coverage-upload-nuget-$([guid]::NewGuid().ToString('N')).config"
$source = [System.Security.SecurityElement]::Escape((Resolve-Path -LiteralPath $Output).Path)
Set-Content -LiteralPath $config -Value @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$source" />
  </packageSources>
</configuration>
"@
try {
  & dotnet tool install CodeCoverage.Uploader --tool-path $toolPath --configfile $config --version $Version
  if ($LASTEXITCODE -ne 0) { throw 'dotnet tool install failed.' }

  $tool = Join-Path $toolPath ($IsWindows ? 'coverage-upload.exe' : 'coverage-upload')
  $printed = (& $tool --version | Out-String).Trim()
  if ($LASTEXITCODE -ne 0) { $failures.Add('coverage-upload --version failed.') }
  if (-not $printed.StartsWith($Version, [StringComparison]::Ordinal)) { $failures.Add("coverage-upload --version printed '$printed'.") }

  # The local folder is the only source: a package it does not hold is not found anywhere else.
  $other = & dotnet tool install CodeCoverage.Uploader.NotPacked --tool-path (Join-Path $toolPath 'other') --configfile $config 2>&1 | Out-String
  if ($LASTEXITCODE -eq 0 -or $other -match 'nuget\.org') { $failures.Add("A source other than $Output was consulted: $other") }
}
finally {
  Remove-Item -LiteralPath $toolPath -Recurse -Force -ErrorAction SilentlyContinue
  Remove-Item -LiteralPath $config -Force -ErrorAction SilentlyContinue
}

if ($failures.Count -gt 0) {
  $failures | ForEach-Object { Write-Output "FAILED: $_" }
  exit 1
}

Write-Output "CodeCoverage.Uploader $Version packed, installed from $Output and run."
# The self-check above leaves $LASTEXITCODE at 1 on purpose; the pwsh shell of GitHub Actions exits with it.
exit 0
