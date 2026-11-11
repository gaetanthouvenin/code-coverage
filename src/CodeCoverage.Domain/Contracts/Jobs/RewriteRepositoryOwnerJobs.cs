// ------------------------------------------------------------------------------------------------
// <copyright file="RewriteRepositoryOwnerJobs.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Contracts.Jobs;

/// <summary>Builds the job that rewrites the owner of the repositories of an organization after its account was renamed.</summary>
public static class RewriteRepositoryOwnerJobs
{
  #region Fields

  /// <summary>The key that serializes and merges the rewrites of one organization.</summary>
  public const string Key = "owner-rewrite";

  #endregion

  #region Methods

  /// <summary>Builds the rewrite, in the organization of the scope.</summary>
  /// <returns>The job, merged with the one already waiting for the same organization.</returns>
  /// <remarks>
  ///   The payload is empty: the job reads the login of its organization when it runs, so that two renames in a
  ///   row never leave the older login behind. Merging loses nothing for the same reason.
  /// </remarks>
  public static JobRequest Create() => JobRequest.Create(JobTypes.RewriteRepositoryOwner, "{}", Key, Key, coalesce: true);

  #endregion
}
