// ------------------------------------------------------------------------------------------------
// <copyright file="ProcessUploadCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Text.Json;

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Uploads.Commands.ProcessUpload;

/// <summary>Handler for the <see cref="ProcessUploadCommand" /> command.</summary>
/// <param name="uploads">The uploads of the organization.</param>
/// <param name="commits">The commits of the organization.</param>
/// <param name="repositories">The repositories of the organization.</param>
/// <param name="directory">The directory of the organizations.</param>
/// <param name="configResolver">The effective configuration of a commit.</param>
/// <param name="trees">The files of a repository at a SHA.</param>
/// <param name="processor">Turns the stored parts into a coverage.</param>
/// <param name="codec">The encoding of the coverage.</param>
/// <param name="reports">The saving of the result with the finalization.</param>
/// <param name="clock">The clock.</param>
/// <remarks>Each upload writes its own lines only, so that the uploads of a commit are processed in parallel.</remarks>
internal sealed class ProcessUploadCommandHandler(
  IUploadRepository uploads,
  ICommitRepository commits,
  IRepositoryRepository repositories,
  IOrganizationDirectory directory,
  ConfigResolver configResolver,
  RepositoryTreeProvider trees,
  UploadProcessor processor,
  ICoverageReportCodec codec,
  ICommitReportRepository reports,
  IClock clock) : ICommandHandler<ProcessUploadCommand>
{
  #region Methods

  /// <summary>Builds the finalization of a commit, merged with the one already waiting.</summary>
  /// <param name="commitId">The identifier of the commit.</param>
  /// <returns>The job.</returns>
  private static JobRequest FinalizeJob(long commitId)
  {
    var id = commitId.ToString(CultureInfo.InvariantCulture);

    return JobRequest.Create(JobTypes.FinalizeCommit, JsonSerializer.Serialize(new { CommitId = commitId }), $"commit:{id}", $"finalize:{id}", coalesce: true);
  }

  /// <summary>Builds the refusal of a row that disappeared.</summary>
  /// <returns>The exception.</returns>
  private static NotFoundException Gone() => new(UploadErrors.NotFound, "The upload or its repository no longer exists.");

  /// <summary>Writes the result on the upload.</summary>
  /// <param name="upload">The upload.</param>
  /// <param name="outcome">What the processing gave.</param>
  /// <returns>The blob of its coverage, or <see langword="null" /> when it failed.</returns>
  private byte[]? Record(Upload upload, UploadProcessingResult outcome)
  {
    if (outcome.Report is not { } report)
    {
      upload.MarkFailed(outcome.ErrorCode!, outcome.Error!, outcome.ReportFiles, outcome.ResolvedFiles, outcome.UnresolvedFiles, outcome.Log, clock.UtcNow);

      return null;
    }

    upload.MarkProcessed(outcome.ReportFiles, outcome.ResolvedFiles, outcome.UnresolvedFiles, report.Totals, outcome.Log, clock.UtcNow);

    return codec.Encode(report);
  }

  #endregion

  #region Implementation of ICommandHandler<ProcessUploadCommand>

  /// <inheritdoc />
  public async ValueTask<Unit> Handle(ProcessUploadCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    if (!await uploads.TryStartProcessingAsync(command.UploadId, command.Worker, cancellationToken).ConfigureAwait(false))
    {
      return Unit.Value;
    }

    var upload = await uploads.FindByPublicIdAsync(command.UploadId, cancellationToken).ConfigureAwait(false) ?? throw Gone();
    var commit = await commits.FindAsync(upload.CommitId, cancellationToken).ConfigureAwait(false) ?? throw Gone();
    var repository = await repositories.FindAsync(commit.RepositoryId, cancellationToken).ConfigureAwait(false) ?? throw Gone();
    var organization = await directory.FindCurrentAsync(cancellationToken).ConfigureAwait(false) ?? throw Gone();
    var address = repository.ToForgeRef(organization.Forge);
    var config = await configResolver.ResolveAsync(repository, address, commit.Sha, cancellationToken).ConfigureAwait(false);

    var outcome = await processor.ProcessAsync(
                    upload,
                    config.Config,
                    token => trees.GetAsync(repository.Id, address, commit.Sha, token),
                    cancellationToken
                  ).ConfigureAwait(false);

    var blob = Record(upload, outcome);

    await reports.SaveProcessedUploadAsync(upload, blob, FinalizeJob(commit.Id), cancellationToken).ConfigureAwait(false);

    return Unit.Value;
  }

  #endregion
}
