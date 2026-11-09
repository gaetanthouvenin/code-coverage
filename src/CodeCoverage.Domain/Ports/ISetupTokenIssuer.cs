// ------------------------------------------------------------------------------------------------
// <copyright file="ISetupTokenIssuer.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Ports;

/// <summary>The token that proves, during the first start, that the caller can read the logs of the host.</summary>
public interface ISetupTokenIssuer
{
  #region Methods

  /// <summary>Issues a token and writes it to the logs, unless one is already valid.</summary>
  void EnsureIssued();

  /// <summary>Checks a token, in a time that does not depend on its content.</summary>
  /// <param name="token">The token, or <see langword="null" />.</param>
  /// <returns><see langword="true" /> when the token is the one issued and not revoked.</returns>
  bool IsValid(string? token);

  /// <summary>Forgets the token: it no longer proves anything.</summary>
  void Revoke();

  #endregion
}
