// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryErrors.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Application.Features.Repositories;

/// <summary>The error codes of the repositories.</summary>
public static class RepositoryErrors
{
  #region Fields

  /// <summary>
  ///   The repository does not exist, its owner cannot be entered, or the sender cannot read it: these
  ///   cases are never told apart.
  /// </summary>
  public const string NotFound = "Repository.NotFound";

  /// <summary>The repository is known and its coverage is not tracked.</summary>
  public const string Inactive = "Repository.Inactive";

  /// <summary>An imported badge token is not 1 to 64 characters of <c>[A-Za-z0-9_-]</c>. Maps to HTTP 400 Bad Request.</summary>
  public const string InvalidBadgeToken = "Repository.InvalidBadgeToken";

  #endregion
}
