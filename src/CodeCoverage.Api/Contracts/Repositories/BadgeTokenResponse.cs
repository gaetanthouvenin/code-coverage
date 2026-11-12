// ------------------------------------------------------------------------------------------------
// <copyright file="BadgeTokenResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Contracts.Repositories;

/// <summary>A new badge token, given once.</summary>
/// <param name="Value">The token, to add as <c>?token=</c> to the badge URLs of a private repository.</param>
public sealed record BadgeTokenResponse(string Value)
{
  #region Methods

  /// <summary>Describes the response without its token, for the logs.</summary>
  /// <returns>The name of the response.</returns>
  public override string ToString() => $"{nameof(BadgeTokenResponse)} {{ }}";

  #endregion
}
