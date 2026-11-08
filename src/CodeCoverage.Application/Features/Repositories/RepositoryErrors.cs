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

  #endregion
}
