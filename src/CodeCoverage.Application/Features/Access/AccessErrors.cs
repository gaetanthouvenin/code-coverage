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

  #endregion
}
