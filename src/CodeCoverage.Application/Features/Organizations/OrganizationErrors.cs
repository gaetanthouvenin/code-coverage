// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationErrors.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Application.Features.Organizations;

/// <summary>The error codes of the organizations.</summary>
public static class OrganizationErrors
{
  #region Fields

  /// <summary>
  ///   The organization does not exist, is not active, or the sender cannot see it: these cases are
  ///   never told apart.
  /// </summary>
  public const string NotFound = "Organization.NotFound";

  #endregion
}
