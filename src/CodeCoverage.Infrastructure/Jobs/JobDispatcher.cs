// ------------------------------------------------------------------------------------------------
// <copyright file="JobDispatcher.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.Json;

using Mediator;

using CodeCoverage.Application.Features.Commits.Commands.FinalizeCommit;
using CodeCoverage.Application.Features.Diagnostics.Commands.RunDiagnosticJob;
using CodeCoverage.Application.Features.Uploads.Commands.ProcessUpload;
using CodeCoverage.Domain.Contracts.Jobs;

namespace CodeCoverage.Infrastructure.Jobs;

/// <summary>
///   <see cref="IJobDispatcher" /> that maps each type of <see cref="JobTypes" /> to the command of the
///   Application it runs, built from the JSON payload of the job.
/// </summary>
/// <param name="sender">The mediator of the scope of the job.</param>
/// <remarks>
///   Each feature that runs work in the background adds its case here, next to its constant in
///   <see cref="JobTypes" /> (job pattern, PLAN §1.4). The commands go through the whole pipeline of the
///   mediator (logging, authorization, validation): the worker sends them as the system principal, which
///   the <c>ISystemOnly</c> commands require.
/// </remarks>
internal sealed class JobDispatcher(ISender sender) : IJobDispatcher
{
  #region Methods

  /// <summary>Reads the commit of a job.</summary>
  /// <param name="job">The job.</param>
  /// <returns>The identifier of the commit.</returns>
  /// <exception cref="InvalidJobException">The payload is not valid.</exception>
  private static long ReadCommitId(JobRecord job)
  {
    try
    {
      using var payload = JsonDocument.Parse(job.PayloadJson);

      return payload.RootElement.GetProperty("CommitId").GetInt64();
    }
    catch (Exception exception) when (exception is JsonException or KeyNotFoundException or FormatException or InvalidOperationException)
    {
      throw new InvalidJobException($"The payload of the {job.Type} job {job.Id} is not valid.");
    }
  }

  /// <summary>Reads the upload of a job.</summary>
  /// <param name="job">The job.</param>
  /// <returns>The public identifier of the upload.</returns>
  /// <exception cref="InvalidJobException">The payload is not valid.</exception>
  private static Guid ReadUploadId(JobRecord job)
  {
    try
    {
      using var payload = JsonDocument.Parse(job.PayloadJson);

      return payload.RootElement.GetProperty("UploadId").GetGuid();
    }
    catch (Exception exception) when (exception is JsonException or KeyNotFoundException or FormatException or InvalidOperationException)
    {
      throw new InvalidJobException($"The payload of the {job.Type} job {job.Id} is not valid.");
    }
  }

  #endregion

  #region Implementation of IJobDispatcher

  /// <inheritdoc />
  public async Task DispatchAsync(JobRecord job, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(job);

    switch (job.Type)
    {
      case JobTypes.Ping:
        await sender.Send(new RunDiagnosticJobCommand(job.Id), cancellationToken).ConfigureAwait(false);
        break;

      case JobTypes.ProcessUpload:
        await sender.Send(new ProcessUploadCommand(ReadUploadId(job), job.LockedBy ?? job.Type), cancellationToken).ConfigureAwait(false);
        break;

      case JobTypes.FinalizeCommit:
        await sender.Send(new FinalizeCommitCommand(ReadCommitId(job)), cancellationToken).ConfigureAwait(false);
        break;

      default:
        throw new InvalidJobException($"The job type '{job.Type}' is unknown: no command runs it.");
    }
  }

  #endregion
}
