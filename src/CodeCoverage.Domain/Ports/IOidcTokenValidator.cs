// ------------------------------------------------------------------------------------------------
// <copyright file="IOidcTokenValidator.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Identity;

namespace CodeCoverage.Domain.Ports;

/// <summary>Verifies the OIDC tokens that GitHub Actions workflows present in place of an upload secret.</summary>
public interface IOidcTokenValidator
{
  #region Methods

  /// <summary>Verifies a token and reads the identity it proves.</summary>
  /// <param name="token">The compact JSON Web Token.</param>
  /// <param name="cancellationToken">A token to cancel the call.</param>
  /// <returns>
  ///   The identity of the workflow, or <see langword="null" /> when the token is not one this instance accepts.
  /// </returns>
  /// <exception cref="ArgumentNullException"><paramref name="token" /> is <see langword="null" />.</exception>
  /// <exception cref="InvalidOperationException">The expected audience is not configured.</exception>
  /// <remarks>
  ///   A token is never accepted when it cannot be verified. An implementation throws when the keys of the issuer cannot
  ///   be obtained, so that an outage is not reported as a bad credential.
  /// </remarks>
  Task<GitHubOidcIdentity?> ValidateAsync(string token, CancellationToken cancellationToken);

  #endregion
}
