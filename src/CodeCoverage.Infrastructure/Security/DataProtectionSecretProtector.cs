// ------------------------------------------------------------------------------------------------
// <copyright file="DataProtectionSecretProtector.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.DataProtection;

using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Infrastructure.Security;

/// <summary>Protects the secrets with ASP.NET Core Data Protection, under one dedicated purpose.</summary>
/// <remarks>
///   The key ring lives in the <c>DataProtectionKeys</c> table and the application name is shared
///   (<see cref="DataProtectionConfiguration" />): the Api and the Web unprotect the values of each other.
/// </remarks>
internal sealed class DataProtectionSecretProtector : ISecretProtector
{
  #region Fields

  /// <summary>The purpose of the protector: a value protected under another purpose is rejected.</summary>
  public const string Purpose = "CodeCoverage.Secrets";

  private readonly IDataProtector _protector;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="DataProtectionSecretProtector" /> class.</summary>
  /// <param name="provider">The Data Protection provider of the host.</param>
  /// <exception cref="ArgumentNullException"><paramref name="provider" /> is <see langword="null" />.</exception>
  public DataProtectionSecretProtector(IDataProtectionProvider provider)
  {
    ArgumentNullException.ThrowIfNull(provider);

    _protector = provider.CreateProtector(Purpose);
  }

  #endregion

  #region Implementation of ISecretProtector

  /// <inheritdoc />
  public string Protect(string plaintext)
  {
    ArgumentNullException.ThrowIfNull(plaintext);

    return _protector.Protect(plaintext);
  }

  /// <inheritdoc />
  public string Unprotect(string protectedValue)
  {
    ArgumentNullException.ThrowIfNull(protectedValue);

    return _protector.Unprotect(protectedValue);
  }

  #endregion
}
