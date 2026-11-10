// ------------------------------------------------------------------------------------------------
// <copyright file="ProcessUploadJobs.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.Json;

namespace CodeCoverage.Domain.Contracts.Jobs;

/// <summary>Builds the processing of an upload, queued by its reception and each time it is processed again.</summary>
public static class ProcessUploadJobs
{
  #region Methods

  /// <summary>Builds the job, without lock nor merge: the uploads of a commit are processed in parallel.</summary>
  /// <param name="uploadId">The public identifier of the upload.</param>
  /// <returns>The job.</returns>
  public static JobRequest Create(Guid uploadId) => JobRequest.Create(JobTypes.ProcessUpload, JsonSerializer.Serialize(new { UploadId = uploadId }));

  #endregion
}
