// ------------------------------------------------------------------------------------------------
// <copyright file="ImportBadgeTokenRequest.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Contracts.Repositories;

/// <summary>A badge token to keep, e.g. the graphing token of Codecov.</summary>
/// <param name="Value">The token: 1 to 64 letters, digits, underscores or hyphens.</param>
public sealed record ImportBadgeTokenRequest(string Value)
{
  #region Methods

  /// <summary>Describes the request without its token, for the logs.</summary>
  /// <returns>The name of the request.</returns>
  public override string ToString() => $"{nameof(ImportBadgeTokenRequest)} {{ }}";

  #endregion
}
