// ------------------------------------------------------------------------------------------------
// <copyright file="ApiTokenReach.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Tokens;

namespace CodeCoverage.Application.Features.Access;

/// <summary>Tells which personal tokens of its user a principal may see and revoke.</summary>
/// <remarks>
///   A signed-in user on the Web reaches every token it owns. A request authenticated by a token stays within it: a token
///   of an organization reaches the tokens of that organization, a token of the instance the tokens of the instance.
/// </remarks>
internal static class ApiTokenReach
{
  #region Methods

  /// <summary>Tells whether a principal reaches a token of its user.</summary>
  /// <param name="principal">The principal.</param>
  /// <param name="token">The token, owned by the user of the principal.</param>
  /// <returns><see langword="true" /> when the principal may see and revoke the token.</returns>
  public static bool Reaches(this ICurrentUser principal, ApiToken token) =>
    principal.IsInstanceToken ? token.IsInstanceToken : principal.TokenOrganizationId is not { } organizationId || token.OrganizationId == organizationId;

  #endregion
}
