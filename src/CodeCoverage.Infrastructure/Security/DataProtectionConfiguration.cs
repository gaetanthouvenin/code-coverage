// ------------------------------------------------------------------------------------------------
// <copyright file="DataProtectionConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Security.Cryptography.X509Certificates;

using Azure.Identity;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Persistence;

namespace CodeCoverage.Infrastructure.Security;

/// <summary>Configures the Data Protection key ring shared by the Api and the Web.</summary>
public static class DataProtectionConfiguration
{
  #region Fields

  /// <summary>
  ///   The application name of the key ring, identical in both hosts so that each one unprotects
  ///   what the other protected (by default, Data Protection isolates the hosts by content root).
  /// </summary>
  public const string ApplicationName = "CodeCoverage";

  #endregion

  #region Methods

  /// <summary>
  ///   Registers Data Protection with its keys in the database, protected by a certificate or an Azure
  ///   Key Vault key, and the <see cref="ISecretProtector" /> built on it.
  /// </summary>
  /// <param name="services">The service collection.</param>
  /// <param name="configuration">The configuration of the host, which holds the <c>DataProtection</c> section.</param>
  /// <param name="environment">The environment of the host.</param>
  /// <returns>The same collection, for chaining.</returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="services" />, <paramref name="configuration" /> or <paramref name="environment" /> is
  ///   <see langword="null" />.
  /// </exception>
  /// <exception cref="InvalidOperationException">
  ///   Both a certificate and a Key Vault key are configured, the Key Vault key identifier is not an
  ///   absolute URI, or the host runs in Production without protecting its keys while
  ///   <c>DataProtection:AllowUnprotectedKeys</c> is not <see langword="true" />.
  /// </exception>
  /// <remarks>
  ///   <para>
  ///     Called by both hosts before the host is built, so that a refusal stops the start-up. The keys
  ///     are read from and written to the <c>DataProtectionKeys</c> table through
  ///     <see cref="CodeCoverageDbContext" />, on first use.
  ///   </para>
  ///   <para>
  ///     <c>DataProtection:CertificatePath</c> (with <c>DataProtection:CertificatePassword</c>) selects
  ///     <c>ProtectKeysWithCertificate</c>; <c>DataProtection:KeyVaultKeyId</c> selects
  ///     <c>ProtectKeysWithAzureKeyVault</c> with the identity of the host
  ///     (<see cref="DefaultAzureCredential" />).
  ///   </para>
  /// </remarks>
  public static IServiceCollection AddCodeCoverageDataProtection(
    this IServiceCollection services,
    IConfiguration configuration,
    IHostEnvironment environment)
  {
    ArgumentNullException.ThrowIfNull(services);
    ArgumentNullException.ThrowIfNull(configuration);
    ArgumentNullException.ThrowIfNull(environment);

    var options = configuration.GetSection(DataProtectionKeyRingOptions.SectionName).Get<DataProtectionKeyRingOptions>()
                  ?? new DataProtectionKeyRingOptions();

    var builder = services.AddDataProtection()
                          .SetApplicationName(ApplicationName)
                          .PersistKeysToDbContext<CodeCoverageDbContext>();

    ProtectKeys(builder, options, environment);

    services.TryAddSingleton<ISecretProtector, DataProtectionSecretProtector>();

    return services;
  }

  /// <summary>Encrypts the keys at rest with the configured certificate or Key Vault key.</summary>
  /// <param name="builder">The Data Protection builder.</param>
  /// <param name="options">The <c>DataProtection</c> section.</param>
  /// <param name="environment">The environment of the host.</param>
  /// <exception cref="InvalidOperationException">The configuration is ambiguous, invalid or unsafe.</exception>
  private static void ProtectKeys(
    IDataProtectionBuilder builder,
    DataProtectionKeyRingOptions options,
    IHostEnvironment environment)
  {
    var hasCertificate = !string.IsNullOrWhiteSpace(options.CertificatePath);
    var hasKeyVault = !string.IsNullOrWhiteSpace(options.KeyVaultKeyId);

    if (hasCertificate && hasKeyVault)
    {
      throw new InvalidOperationException(
        "Both DataProtection:CertificatePath and DataProtection:KeyVaultKeyId are set: configure only one key protection."
      );
    }

    if (hasCertificate)
    {
      // Kept for the lifetime of the host: Data Protection encrypts and decrypts the keys with it.
      builder.ProtectKeysWithCertificate(
        X509CertificateLoader.LoadPkcs12FromFile(options.CertificatePath!, options.CertificatePassword)
      );

      return;
    }

    if (hasKeyVault)
    {
      if (!Uri.TryCreate(options.KeyVaultKeyId, UriKind.Absolute, out var keyIdentifier))
      {
        throw new InvalidOperationException(
          $"DataProtection:KeyVaultKeyId '{options.KeyVaultKeyId}' is not an absolute key URI."
        );
      }

      builder.ProtectKeysWithAzureKeyVault(keyIdentifier, new DefaultAzureCredential());

      return;
    }

    if (environment.IsProduction() && !options.AllowUnprotectedKeys)
    {
      throw new InvalidOperationException(
        "The Data Protection keys would be stored unencrypted in Production. Set DataProtection__CertificatePath "
        + "(and DataProtection__CertificatePassword) or DataProtection__KeyVaultKeyId, or accept the risk explicitly "
        + "with DataProtection__AllowUnprotectedKeys=true."
      );
    }
  }

  #endregion
}
