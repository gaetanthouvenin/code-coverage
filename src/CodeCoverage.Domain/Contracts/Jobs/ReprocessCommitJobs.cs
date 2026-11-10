// ------------------------------------------------------------------------------------------------
// <copyright file="ReprocessCommitJobs.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Text.Json;

namespace CodeCoverage.Domain.Contracts.Jobs;

/// <summary>Builds the reprocessing of a commit, queued when an administrator of its repository asks for it.</summary>
public static class ReprocessCommitJobs
{
  #region Methods

  /// <summary>Builds the job, merged with the one already waiting.</summary>
  /// <param name="commitId">The identifier of the commit.</param>
  /// <returns>The job.</returns>
  /// <remarks>
  ///   It shares the lock of the finalization of the commit: the uploads are never put back in the queue
  ///   while a finalization reads them. Two requests that wait give one reprocessing.
  /// </remarks>
  public static JobRequest Create(long commitId)
  {
    var id = commitId.ToString(CultureInfo.InvariantCulture);

    return JobRequest.Create(JobTypes.ReprocessCommit, JsonSerializer.Serialize(new { CommitId = commitId }), $"commit:{id}", $"reprocess:{id}", coalesce: true);
  }

  #endregion
}
