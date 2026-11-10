// ------------------------------------------------------------------------------------------------
// <copyright file="FinalizeCommitJobs.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Text.Json;

namespace CodeCoverage.Domain.Contracts.Jobs;

/// <summary>Builds the finalization of a commit, queued by the processing of an upload and by the wake-ups of a finalization.</summary>
public static class FinalizeCommitJobs
{
  #region Methods

  /// <summary>Builds the job, merged with the one already waiting.</summary>
  /// <param name="commitId">The identifier of the commit.</param>
  /// <returns>The job.</returns>
  public static JobRequest Create(long commitId)
  {
    var id = commitId.ToString(CultureInfo.InvariantCulture);

    return JobRequest.Create(JobTypes.FinalizeCommit, JsonSerializer.Serialize(new { CommitId = commitId }), $"commit:{id}", $"finalize:{id}", coalesce: true);
  }

  #endregion
}
