// ------------------------------------------------------------------------------------------------
// <copyright file="FinalizeCommitCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Commits.Commands.FinalizeCommit;

/// <summary>Handler for the <see cref="FinalizeCommitCommand" /> command.</summary>
/// <param name="commits">The commits of the organization.</param>
/// <param name="uploads">The uploads of the organization.</param>
/// <param name="repositories">The repositories of the organization.</param>
/// <param name="directory">The directory of the organizations.</param>
/// <param name="configResolver">The effective configuration of a commit.</param>
/// <param name="reports">The blobs and the saving of the finalization.</param>
/// <param name="codec">The encoding of the coverage.</param>
/// <param name="merger">Rebuilds the report from the sessions.</param>
/// <param name="clock">The clock.</param>
/// <remarks>The job is serialized by its lock key. The rebuild is complete and deterministic, whatever the order of arrival.</remarks>
internal sealed class FinalizeCommitCommandHandler(
  ICommitRepository commits,
  IUploadRepository uploads,
  IRepositoryRepository repositories,
  IOrganizationDirectory directory,
  ConfigResolver configResolver,
  ICommitReportRepository reports,
  ICoverageReportCodec codec,
  CommitMerger merger,
  IClock clock) : ICommandHandler<FinalizeCommitCommand>
{
  #region Methods

  /// <summary>Builds the refusal of a commit that cannot be read.</summary>
  /// <returns>The exception.</returns>
  private static NotFoundException NotFound() => new(CommitErrors.NotFound, "The commit no longer exists.");

  /// <summary>Reads the sessions of the uploads that were processed.</summary>
  /// <param name="commit">The commit.</param>
  /// <param name="processed">The uploads, by session number.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The sessions and their coverage.</returns>
  private async Task<IReadOnlyList<SessionCoverage>> ReadSessionsAsync(Commit commit, IReadOnlyList<Upload> processed, CancellationToken cancellationToken)
  {
    var sessions = new List<SessionCoverage>(processed.Count);

    foreach (var upload in processed)
    {
      var blob = await reports.GetUploadCoverageAsync(upload.Id, cancellationToken).ConfigureAwait(false)
                 ?? throw new UnprocessableEntityException(CommitErrors.Compacted, "The coverage of an upload of this commit no longer exists.");
      var report = codec.Decode(blob);
      var session = Session.FromUpload(commit.Id, upload.Sequence, upload.Id, upload.Flags, upload.Name, upload.CiService, upload.BuildUrl, report.Totals);

      sessions.Add(new SessionCoverage(session, report));
    }

    return sessions;
  }

  #endregion

  #region Implementation of ICommandHandler<FinalizeCommitCommand>

  /// <inheritdoc />
  /// <exception cref="NotFoundException">The commit no longer exists.</exception>
  /// <exception cref="UnprocessableEntityException">The coverage of a processed upload no longer exists.</exception>
  public async ValueTask<Unit> Handle(FinalizeCommitCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var commit = await commits.FindAsync(command.CommitId, cancellationToken).ConfigureAwait(false) ?? throw NotFound();
    var all = await uploads.ListByCommitAsync(commit.Id, cancellationToken).ConfigureAwait(false);

    // The last upload to end queues the next finalization.
    if (all.Count == 0 || all.Any(static upload => upload.State is UploadState.Received or UploadState.Processing))
    {
      return Unit.Value;
    }

    var repository = await repositories.FindAsync(commit.RepositoryId, cancellationToken).ConfigureAwait(false) ?? throw NotFound();
    var organization = await directory.FindCurrentAsync(cancellationToken).ConfigureAwait(false) ?? throw NotFound();
    var resolved = await configResolver.ResolveAsync(repository, repository.ToForgeRef(organization.Forge), commit.Sha, cancellationToken).ConfigureAwait(false);
    Upload[] processed = [.. all.Where(static upload => upload.State is UploadState.Processed).OrderBy(static upload => upload.Sequence)];
    var sessions = await ReadSessionsAsync(commit, processed, cancellationToken).ConfigureAwait(false);
    var merged = merger.MergeProject(resolved.Config, sessions);

    commit.RecordFinalization(
      processed.Length == 0 ? CommitState.Error : CommitState.Complete,
      merged.Files.Count,
      merged.Totals,
      sessions.Count,
      processed.Length,
      CarryforwardStatus.None,
      resolved.Source.ToString(),
      clock.UtcNow
    );

    await reports.SaveFinalizedAsync(
      commit,
      codec.Encode(merged),
      codec.EncodeFileIndex(merged),
      [.. sessions.Select(static session => session.Session)],
      merger.BuildFlagTotals(commit, resolved.Config, sessions, merged),
      cancellationToken
    ).ConfigureAwait(false);

    return Unit.Value;
  }

  #endregion
}
