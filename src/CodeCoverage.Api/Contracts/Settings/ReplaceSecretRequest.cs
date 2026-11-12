// ------------------------------------------------------------------------------------------------
// <copyright file="ReplaceSecretRequest.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Contracts.Settings;

/// <summary>The new value of a secret of the instance.</summary>
/// <param name="Value">The secret, stored protected and never read back.</param>
public sealed record ReplaceSecretRequest(string Value)
{
  #region Methods

  /// <summary>Describes the request without its secret, for the logs.</summary>
  /// <returns>The name of the request.</returns>
  public override string ToString() => $"{nameof(ReplaceSecretRequest)} {{ }}";

  #endregion
}
