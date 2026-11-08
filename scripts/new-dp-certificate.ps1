#Requires -Version 7.2
<#
.SYNOPSIS
  Creates a self-signed Data Protection certificate for a local docker compose run.

.DESCRIPTION
  Writes a PKCS#12 file (RSA 3072, SHA-256) protected by a password, used by both hosts to encrypt
  the Data Protection key ring stored in the database (DataProtection__CertificatePath and
  DataProtection__CertificatePassword). It is a development certificate: in production, use a
  managed certificate or Azure Key Vault, and back it up separately from the database.

  The file is made readable by everyone on Linux and macOS, because docker compose bind-mounts it
  with the host permissions and the containers run as the non-root user 1001.

.PARAMETER Path
  The PKCS#12 file to create. Defaults to secrets/dp.pfx at the repository root, the path read by
  docker-compose.yml.

.PARAMETER Password
  The password of the file: the value of DP_CERT_PASSWORD. Defaults to the DP_CERT_PASSWORD
  environment variable, then to DP_CERT_PASSWORD in the .env file of the repository.

.PARAMETER Force
  Overwrites an existing file. Without it, an existing file is kept: replacing the certificate
  makes the key ring already stored in the database unreadable.

.EXAMPLE
  pwsh scripts/new-dp-certificate.ps1 -Password $env:DP_CERT_PASSWORD
#>
[CmdletBinding()]
param(
  [string] $Path = (Join-Path (Split-Path $PSScriptRoot -Parent) 'secrets/dp.pfx'),
  [string] $Password = $env:DP_CERT_PASSWORD,
  [switch] $Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$envFile = Join-Path (Split-Path $PSScriptRoot -Parent) '.env'

if ([string]::IsNullOrEmpty($Password) -and (Test-Path -LiteralPath $envFile)) {
  # The value docker compose will pass to the hosts.
  $line = Get-Content -LiteralPath $envFile | Where-Object { $_ -match '^\s*DP_CERT_PASSWORD=' } |
          Select-Object -Last 1
  if ($line) {
    $Password = ($line -replace '^\s*DP_CERT_PASSWORD=', '').Trim().Trim('"', "'")
  }
}

if ([string]::IsNullOrEmpty($Password)) {
  throw 'Give -Password, or set DP_CERT_PASSWORD in the environment or in .env.'
}

if ((Test-Path -LiteralPath $Path) -and -not $Force) {
  Write-Host "Kept the existing certificate $Path (use -Force to replace it)."
  return
}

$directory = Split-Path -Path $Path -Parent

if ($directory -and -not (Test-Path -LiteralPath $directory)) {
  New-Item -ItemType Directory -Path $directory -Force | Out-Null
}

$rsa = [System.Security.Cryptography.RSA]::Create(3072)

try {
  $request = [System.Security.Cryptography.X509Certificates.CertificateRequest]::new(
    'CN=CodeCoverage Data Protection (development)',
    $rsa,
    [System.Security.Cryptography.HashAlgorithmName]::SHA256,
    [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)

  $usages = [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::KeyEncipherment -bor
            [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::DataEncipherment
  $request.CertificateExtensions.Add(
    [System.Security.Cryptography.X509Certificates.X509KeyUsageExtension]::new($usages, $true))

  $now = [DateTimeOffset]::UtcNow
  $certificate = $request.CreateSelfSigned($now.AddDays(-1), $now.AddYears(5))

  try {
    $bytes = $certificate.Export(
      [System.Security.Cryptography.X509Certificates.X509ContentType]::Pkcs12,
      $Password)
    # .NET resolves relative paths against the process directory, not the PowerShell location.
    $fullPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)
    [System.IO.File]::WriteAllBytes($fullPath, $bytes)
  }
  finally {
    $certificate.Dispose()
  }
}
finally {
  $rsa.Dispose()
}

if (-not $IsWindows) {
  chmod 644 $Path
}

Write-Host "Created the Data Protection certificate $Path (valid 5 years)."
