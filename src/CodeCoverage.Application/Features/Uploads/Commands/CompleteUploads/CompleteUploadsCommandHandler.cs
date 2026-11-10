// ------------------------------------------------------------------------------------------------
// <copyright file="CompleteUploadsCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Commits;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Features.Uploads.Commands.CompleteUploads;

/// <summary>Handler for the <see cref="CompleteUploadsCommand" /> command.</summary>
/// <param name="authenticator">What checks the credential of an upload.</param>
/// <param name="commits">The repository of the commits.</param>
/// <param name="uploads">The repository of the uploads.</param>
/// <param name="jobQueue">The job queue.</param>
/// <remarks>
///   <para>
///     The credential is checked exactly as the one of an upload: the same refusals, in the same order.
///     Nothing calls the forge: the notification itself is a job.
///   </para>
///   <para>
///     The counts are those of the commit the address names. After the uploads of a synthetic merge commit were
///     moved to the head of the pull request, they are zero, and the notification still reaches that head.
///   </para>
/// </remarks>
internal sealed class CompleteUploadsCommandHandler(
  UploadAuthenticator authenticator,
  ICommitRepository commits,
  IUploadRepository uploads,
  IJobQueue jobQueue) : ICommandHandler<CompleteUploadsCommand, UploadCompletion>
{
  #region Methods

  /// <summary>Builds the refusal of a commit that does not exist or cannot be read.</summary>
  /// <returns>The exception.</returns>
  private static NotFoundException NotFound() => new(CommitErrors.NotFound, "The commit does not exist.");

  #endregion

  #region Implementation of ICommandHandler<CompleteUploadsCommand,UploadCompletion>

  /// <inheritdoc />
  /// <exception cref="UnauthorizedException">The credential is not one of the organization.</exception>
  /// <exception cref="ForbiddenException">The credential is the one of another repository.</exception>
  /// <exception cref="NotFoundException">The repository or the commit does not exist.</exception>
  public async ValueTask<UploadCompletion> Handle(CompleteUploadsCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    if (!RepositorySlug.TryFromAddress(command.Owner, command.Name, out var slug) || !GitSha.TryParse(command.Sha, out var sha))
    {
      throw NotFound();
    }

    var authorization = await authenticator.AuthenticateAsync(command.Credential, command.Forge, slug, cancellationToken).ConfigureAwait(false);

    // A repository that received no upload is not tracked yet: it has no commit either.
    if (authorization.RepositoryId is not { } repositoryId
        || await commits.FindByShaAsync(repositoryId, sha, cancellationToken).ConfigureAwait(false) is not { } commit)
    {
      throw NotFound();
    }

    var all = await uploads.ListByCommitAsync(commit.Id, cancellationToken).ConfigureAwait(false);
    var completion = new UploadCompletion(
      all.Count,
      all.Count(static u => u.State is UploadState.Processed),
      all.Count(static u => u.State is UploadState.Received or UploadState.Processing),
      all.Count(static u => u.State is UploadState.Failed)
    );

    // Queued even while uploads are being processed: the job waits for them. Without any upload there is nothing to
    // notify, unless the commit is the one of a pull request: the uploads sent with the SHA of its synthetic merge
    // commit were moved to its head (ARCHITECTURE §1.4-2), which only the job can ask the forge for.
    if (completion.Total > 0 || commit.PullNumber is not null)
    {
      await jobQueue.EnqueueAsync(NotifyCommitJobs.CreateForced(commit.Id), cancellationToken).ConfigureAwait(false);
    }

    return completion;
  }

  #endregion
}