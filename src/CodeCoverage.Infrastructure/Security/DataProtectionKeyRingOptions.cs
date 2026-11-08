// ------------------------------------------------------------------------------------------------
// <copyright file="DataProtectionKeyRingOptions.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.Security;

/// <summary>The <c>DataProtection</c> configuration section: how the key ring is protected at rest.</summary>
/// <remarks>
///   Bootstrap settings, expected as environment variables (<c>DataProtection__CertificatePath</c>,
///   <c>DataProtection__CertificatePassword</c>, <c>DataProtection__KeyVaultKeyId</c>,
///   <c>DataProtection__AllowUnprotectedKeys</c>) and read once, at start-up, from the host
///   configuration, before the database settings store: the key ring must be usable before the
///   settings stored in the database, some of them protected, can be read.
/// </remarks>
public sealed class DataProtectionKeyRingOptions
{
  #region Fields

  /// <summary>The name of the configuration section.</summary>
  public const string SectionName = "DataProtection";

  #endregion

  #region Properties

  /// <summary>Gets or sets the path of the PKCS#12 (<c>.pfx</c>) certificate that encrypts the keys.</summary>
  /// <value>The path of the certificate file; <see langword="null" /> when no certificate is used.</value>
  public string? CertificatePath { get; set; }

  /// <summary>Gets or sets the password of the certificate file.</summary>
  /// <value>The password; <see langword="null" /> when the file has none.</value>
  public string? CertificatePassword { get; set; }

  /// <summary>Gets or sets the identifier of the Azure Key Vault key that wraps the keys.</summary>
  /// <value>
  ///   The absolute key URI (<c>https://{vault}.vault.azure.net/keys/{name}</c>); <see langword="null" />
  ///   when Key Vault is not used. The host authenticates with its own identity (managed identity in Azure).
  /// </value>
  public string? KeyVaultKeyId { get; set; }

  /// <summary>Gets or sets a value indicating whether the keys may be stored unencrypted in Production.</summary>
  /// <value>
  ///   <see langword="false" /> (the default): in Production, the host refuses to start without a
  ///   certificate or a Key Vault key. <see langword="true" /> accepts the risk explicitly.
  /// </value>
  public bool AllowUnprotectedKeys { get; set; }

  #endregion
}
