// ------------------------------------------------------------------------------------------------
// <copyright file="AccessErrors.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Application.Features.Access;

/// <summary>The error codes of the access: sign-in, permissions and personal tokens.</summary>
/// <remarks>
///   The codes every message can raise, <c>Access.Forbidden</c> and <c>Access.Unauthenticated</c>, stay in
///   <see cref="Security.SecurityErrors" />: the two classes share the prefix, never a code.
/// </remarks>
public static class AccessErrors
{
  #region Fields

  /// <summary>
  ///   The account reaches no repository of an active organization, and does not administer the instance. Maps to HTTP
  ///   403 Forbidden.
  /// </summary>
  public const string NoRepository = "Access.NoRepository";

  /// <summary>The user owns no such personal token of the API. Maps to HTTP 404 Not Found.</summary>
  public const string ApiTokenNotFound = "Access.ApiTokenNotFound";

  /// <summary>The name of a personal token is empty or too long. Maps to HTTP 400 Bad Request.</summary>
  public const string InvalidApiTokenName = "Access.InvalidApiTokenName";

  /// <summary>A personal token names a forge without an organization, or the reverse. Maps to HTTP 400 Bad Request.</summary>
  public const string InvalidApiTokenOrganization = "Access.InvalidApiTokenOrganization";

  /// <summary>The expiry of a personal token is not in the future. Maps to HTTP 400 Bad Request.</summary>
  public const string InvalidApiTokenExpiry = "Access.InvalidApiTokenExpiry";

  #endregion
}
