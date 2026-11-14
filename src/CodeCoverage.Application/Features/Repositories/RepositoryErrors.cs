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

  /// <summary>
  ///   A query of the history is out of its bounds: unknown interval, start after the end, too many or too long flags,
  ///   branch too long. Maps to HTTP 400 Bad Request.
  /// </summary>
  public const string InvalidHistory = "Repository.InvalidHistory";

  /// <summary>
  ///   The API token of Codecov given to import the history is empty, too long or holds spaces. Maps to HTTP 400 Bad
  ///   Request.
  /// </summary>
  public const string InvalidCodecovToken = "Repository.InvalidCodecovToken";

  /// <summary>
  ///   Codecov refused the import of the history for good: the token is not valid or not allowed, or Codecov does not
  ///   have the repository. The job fails; maps to HTTP 422 Unprocessable Entity.
  /// </summary>
  public const string CodecovRefused = "Repository.CodecovRefused";

  /// <summary>
  ///   The forge of the repository has no counterpart on Codecov (Azure DevOps): its history cannot be imported. Maps to
  ///   HTTP 422 Unprocessable Entity.
  /// </summary>
  public const string CodecovUnsupportedForge = "Repository.CodecovUnsupportedForge";

  #endregion
}
