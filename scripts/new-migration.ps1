#Requires -Version 7.2
<#
.SYNOPSIS
  Generates a named EF Core migration for PostgreSQL and for SQL Server, together.

.DESCRIPTION
  Runs dotnet ef (restored from dotnet-tools.json) on src/CodeCoverage.Infrastructure.PostgreSql and
  src/CodeCoverage.Infrastructure.SqlServer, through the DesignTimeDbContextFactory of each project.
  Both migrations get the same identifier, and the generated files and both snapshots get the
  copyright header (docs/CONVENTIONS.md §3.1).

  Both Migrations folders are saved first and restored if anything fails or the run is interrupted:
  one provider is never left migrated and not the other.

  With -Regenerate, the last migration of each provider must be the one named by -Name. It is removed
  on both sides, then generated again from the current model: use it after a rebase on a branch that
  brought another migration (docs/CONVENTIONS.md §8.2).

  Generating a migration reaches no database. -Regenerate uses dotnet ef migrations remove --force:
  no database has to be reachable, but a design database that answers and has this migration applied
  gets it reverted. The database is the one of CODECOVERAGE_DESIGN_CONNECTION, otherwise the default
  of each DesignTimeDbContextFactory.

.PARAMETER Name
  The name of the migration, in PascalCase, e.g. AddRepositories.

.PARAMETER Regenerate
  Removes the last migration of both providers, which must be named -Name, and generates it again.

.EXAMPLE
  pwsh scripts/new-migration.ps1 -Name AddRepositories

.EXAMPLE
  pwsh scripts/new-migration.ps1 -Name AddRepositories -Regenerate
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory)]
  [ValidatePattern('^[A-Z][A-Za-z0-9]*$')]
  [string] $Name,
  [switch] $Regenerate
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent
$snapshotFile = 'CodeCoverageDbContextModelSnapshot.cs'
$utf8 = [System.Text.UTF8Encoding]::new($false)

$providers = 'PostgreSql', 'SqlServer' | ForEach-Object {
  $project = Join-Path $root "src/CodeCoverage.Infrastructure.$_"
  [pscustomobject] @{
    Name       = $_
    Project    = $project
    Migrations = Join-Path $project 'Migrations'
  }
}

# The identifier of the last migration of a provider, e.g. 20261108024917_AddJobs.
function Get-LastMigrationId([pscustomobject] $Provider) {
  $last = Get-ChildItem -LiteralPath $Provider.Migrations -Filter '*.Designer.cs' |
          Sort-Object -Property Name |
          Select-Object -Last 1
  if ($last) { $last.Name -replace '\.Designer\.cs$', '' } else { '' }
}

function Get-MigrationName([string] $Id) {
  ($Id -split '_', 2)[-1]
}

function Invoke-DotnetEf([pscustomobject] $Provider, [string[]] $Arguments) {
  & dotnet ef @Arguments --project $Provider.Project --startup-project $Provider.Project
  if ($LASTEXITCODE -ne 0) {
    throw "dotnet ef $($Arguments[0]) $($Arguments[1]) failed for $($Provider.Name) (exit code $LASTEXITCODE): see its output above."
  }
}

function Remove-LastMigration([pscustomobject] $Provider) {
  # After a rebase on another migration, the target model of this migration no longer matches the
  # snapshot: EF then only rewrites the snapshot from that target model and keeps the migration. The
  # second run removes it. The name is read again each time, never to remove the migration before it.
  foreach ($attempt in 1..2) {
    Invoke-DotnetEf $Provider @('migrations', 'remove', '--force')
    if ((Get-MigrationName (Get-LastMigrationId $Provider)) -ne $Name) {
      return
    }
  }

  throw "dotnet ef migrations remove kept $Name on $($Provider.Name)."
}

# Gives the migration of a provider the identifier of the other one: dotnet ef stamps each with its
# own clock.
function Set-MigrationId([pscustomobject] $Provider, [string] $Id) {
  $current = Get-LastMigrationId $Provider
  if ($current -eq $Id) {
    return
  }

  foreach ($suffix in '.cs', '.Designer.cs') {
    $from = Join-Path $Provider.Migrations "$current$suffix"
    $to = Join-Path $Provider.Migrations "$Id$suffix"
    Move-Item -LiteralPath $from -Destination $to
  }

  $designer = Join-Path $Provider.Migrations "$Id.Designer.cs"
  $text = [System.IO.File]::ReadAllText($designer).Replace("`"$current`"", "`"$Id`"")
  [System.IO.File]::WriteAllText($designer, $text, $utf8)
}

# dotnet ef writes these files without the header that SourceFileConventionTests requires.
function Add-CopyrightHeader([string] $Path) {
  $rule = '// ' + ('-' * 96)
  $text = [System.IO.File]::ReadAllText($Path)
  if ($text.StartsWith($rule)) {
    return
  }

  $fileName = Split-Path -Path $Path -Leaf
  $newLine = [Environment]::NewLine
  $header = @(
    $rule
    "// <copyright file=`"$fileName`" company=`"Gaëtan Thouvenin`">"
    '//   Copyright (c) Gaëtan Thouvenin. All rights reserved.'
    '// </copyright>'
    $rule
  ) -join $newLine
  [System.IO.File]::WriteAllText($Path, "$header$newLine$newLine$text", $utf8)
}

# Refuses what would make the run fail halfway, before anything is touched.
foreach ($provider in $providers) {
  $snapshot = Join-Path $provider.Migrations $snapshotFile
  if (Select-String -LiteralPath $snapshot -Pattern '^(<{7}|={7}|>{7})( |$)' -Quiet) {
    throw "$snapshot has conflict markers: keep either side first (the snapshot is rebuilt from the migrations), then run this script again."
  }
}

$lastIds = @($providers | ForEach-Object { Get-LastMigrationId $_ })
$lastNames = @($lastIds | ForEach-Object { Get-MigrationName $_ })

if ($lastNames[0] -ne $lastNames[1]) {
  throw "The providers disagree on their last migration ($($providers[0].Name): $($lastIds[0]), $($providers[1].Name): $($lastIds[1])): fix that first."
}

if ($Regenerate -and $lastNames[0] -ne $Name) {
  throw "-Regenerate only replaces the last migration, and it is $($lastIds[0]), not $Name. If $Name is older than a migration that came with the rebase, delete its two files on both sides, take both snapshots from develop and run this script without -Regenerate."
}

$backup = Join-Path ([System.IO.Path]::GetTempPath()) "new-migration-$([guid]::NewGuid().ToString('N'))"
$succeeded = $false

# dotnet tool run looks for the manifest from the current directory.
Push-Location -LiteralPath $root

try {
  dotnet tool restore
  if ($LASTEXITCODE -ne 0) {
    throw "dotnet tool restore failed (exit code $LASTEXITCODE): see its output above."
  }

  New-Item -ItemType Directory -Path $backup | Out-Null
  foreach ($provider in $providers) {
    Copy-Item -LiteralPath $provider.Migrations -Destination (Join-Path $backup $provider.Name) -Recurse
  }

  if ($Regenerate) {
    foreach ($provider in $providers) {
      Remove-LastMigration $provider
    }
  }

  foreach ($provider in $providers) {
    Invoke-DotnetEf $provider @('migrations', 'add', $Name)
  }

  $id = Get-LastMigrationId $providers[0]

  foreach ($provider in $providers) {
    Set-MigrationId $provider $id

    foreach ($file in "$id.cs", "$id.Designer.cs", $snapshotFile) {
      Add-CopyrightHeader (Join-Path $provider.Migrations $file)
    }
  }

  $succeeded = $true
  Write-Host "Generated the migration $id for $($providers.Name -join ' and ')."
}
finally {
  if (-not $succeeded -and (Test-Path -LiteralPath $backup)) {
    foreach ($provider in $providers) {
      Remove-Item -LiteralPath $provider.Migrations -Recurse -Force
      Copy-Item -LiteralPath (Join-Path $backup $provider.Name) -Destination $provider.Migrations -Recurse
    }

    Write-Host "No migration generated: the Migrations folders of $($providers.Name -join ' and ') were restored." -ForegroundColor Yellow
  }

  if (Test-Path -LiteralPath $backup) {
    Remove-Item -LiteralPath $backup -Recurse -Force
  }

  Pop-Location
}
