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

  /// <summary>The organization was already admitted or is suspended: only a pending one can be admitted.</summary>
  public const string NotPending = "Organization.NotPending";

  /// <summary>The login is not one a forge can give.</summary>
  public const string InvalidLogin = "Organization.InvalidLogin";

  /// <summary>The identifier of the account is empty or too long.</summary>
  public const string InvalidAccountId = "Organization.InvalidAccountId";

  #endregion
}
