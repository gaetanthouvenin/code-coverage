// ------------------------------------------------------------------------------------------------
// <copyright file="CommitsController.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using CodeCoverage.Api.Contracts.Commits;
using CodeCoverage.Api.Contracts.Uploads;
using CodeCoverage.Api.Mapping;
using CodeCoverage.Api.Uploads;
using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Uploads;

namespace CodeCoverage.Api.Controllers;

/// <summary>The commits of a repository.</summary>
/// <param name="sender">The request sender.</param>
[ApiController]
[Route(CodeCoverageApi.Route)]
[Tags(CodeCoverageApi.Tag)]
public sealed class CommitsController(ISender sender) : ControllerBase
{
  #region Methods

  /// <summary>List the commits of a repository</summary>
  /// <remarks>
  ///   One page of the commits, the most recently completed first; the commits not finalized yet come last. A page
  ///   holds 25 commits unless <c>pageSize</c> asks for 1 to 100. A private repository the token cannot read answers
  ///   404, as a missing one.
  /// </remarks>
  /// <param name="forge">The forge of the repository.</param>
  /// <param name="owner">The owner of the repository.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="branch">The branch the commits were uploaded for, or every commit.</param>
  /// <param name="page">The number of the page, from 1.</param>
  /// <param name="pageSize">The number of commits of a page, 1 to 100.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The page.</returns>
  [HttpGet("repos/{forge}/{owner}/{repo}/commits", Name = "ListCommits")]
  [Authorize]
  [ProducesResponseType<CommitPageResponse>(StatusCodes.Status200OK)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<CommitPageResponse>> ListCommitsAsync(
    string forge,
    string owner,
    string repo,
    [FromQuery] string? branch,
    [FromQuery] int? page,
    [FromQuery] int? pageSize,
    CancellationToken cancellationToken) =>
    await sender.GetCommitPageResponseAsync(forge, owner, repo, branch, page, pageSize, cancellationToken).ConfigureAwait(false);

  /// <summary>Read a commit</summary>
  /// <remarks>
  ///   The totals and the state of the commit, its sessions, its uploads, the totals of its flags (carried forward ones
  ///   included) and the statuses posted for it. A compacted commit is read like any other: only its reports are gone.
  /// </remarks>
  /// <param name="forge">The forge of the repository.</param>
  /// <param name="owner">The owner of the repository.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="sha">The full SHA of the commit.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The commit.</returns>
  [HttpGet("repos/{forge}/{owner}/{repo}/commits/{sha}", Name = "GetCommit")]
  [Authorize]
  [ProducesResponseType<CommitResponse>(StatusCodes.Status200OK)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<CommitResponse>> GetCommitAsync(string forge, string owner, string repo, string sha, CancellationToken cancellationToken) =>
    await sender.GetCommitResponseAsync(forge, owner, repo, sha, cancellationToken).ConfigureAwait(false);

  /// <summary>List the files of a commit</summary>
  /// <remarks>
  ///   The files of the report of the commit with their counters, sorted by path. Without <c>flag</c>, the index of the
  ///   files is read without decoding any line; with it, the sessions of the flag are merged, and a compacted commit
  ///   answers 422 <c>Commit.Compacted</c>. <c>path</c> keeps the paths that start with it.
  /// </remarks>
  /// <param name="forge">The forge of the repository.</param>
  /// <param name="owner">The owner of the repository.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="sha">The full SHA of the commit.</param>
  /// <param name="flag">The flag, case-sensitive, or the merged report.</param>
  /// <param name="path">A prefix of the paths.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The files.</returns>
  [HttpGet("repos/{forge}/{owner}/{repo}/commits/{sha}/files", Name = "GetCommitFiles")]
  [Authorize]
  [ProducesResponseType<IReadOnlyList<FileTotalsResponse>>(StatusCodes.Status200OK)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
  public async Task<ActionResult<IReadOnlyList<FileTotalsResponse>>> GetCommitFilesAsync(
    string forge,
    string owner,
    string repo,
    string sha,
    [FromQuery] string? flag,
    [FromQuery] string? path,
    CancellationToken cancellationToken) =>
    Ok(await sender.GetCommitFilesResponseAsync(forge, owner, repo, sha, flag, path, cancellationToken).ConfigureAwait(false));

  /// <summary>Read the coverage of a file of a commit</summary>
  /// <remarks>
  ///   The coverable lines of the file, each with its state (hit, miss, partial), its hits and its branches. With
  ///   <c>flag</c>, only the sessions of the flag count. A compacted commit answers 422 <c>Commit.Compacted</c>; a file
  ///   the report does not hold, 404 <c>Commit.FileNotFound</c>.
  /// </remarks>
  /// <param name="forge">The forge of the repository.</param>
  /// <param name="owner">The owner of the repository.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="sha">The full SHA of the commit.</param>
  /// <param name="path">The path of the file, as the report stores it.</param>
  /// <param name="flag">The flag, case-sensitive, or the merged report.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The coverage of the file.</returns>
  [HttpGet("repos/{forge}/{owner}/{repo}/commits/{sha}/files/{**path}", Name = "GetFileCoverage")]
  [Authorize]
  [ProducesResponseType<FileCoverageResponse>(StatusCodes.Status200OK)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
  public async Task<ActionResult<FileCoverageResponse>> GetFileCoverageAsync(
    string forge,
    string owner,
    string repo,
    string sha,
    string path,
    [FromQuery] string? flag,
    CancellationToken cancellationToken) =>
    await sender.GetFileCoverageResponseAsync(forge, owner, repo, sha, path, flag, cancellationToken).ConfigureAwait(false);

  /// <summary>Notify a commit again</summary>
  /// <remarks>
  ///   Queues the notification of the commit (statuses and comment of its pull request), whatever
  ///   <c>codecov.notify.after_n_builds</c> and <c>manual_trigger</c> say. Requires an administrator of the repository;
  ///   a reader gets 403. The action is audited. It answers at once: the statuses are published in the background.
  /// </remarks>
  /// <param name="forge">The forge of the repository.</param>
  /// <param name="owner">The owner of the repository.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="sha">The full SHA of the commit.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>Accepted.</returns>
  [HttpPost("repos/{forge}/{owner}/{repo}/commits/{sha}/notify", Name = "TriggerNotify")]
  [Authorize]
  [ProducesResponseType(StatusCodes.Status202Accepted)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> NotifyAsync(string forge, string owner, string repo, string sha, CancellationToken cancellationToken)
  {
    await sender.TriggerNotifyAsync(forge, owner, repo, sha, cancellationToken).ConfigureAwait(false);

    return Accepted();
  }

  /// <summary>Process the uploads of a commit again</summary>
  /// <remarks>
  ///   Queues the parsing of every upload of the commit again from its raw files, then its finalization and its
  ///   notification. Requires an administrator of the repository; a reader gets 403. A commit without upload answers 422
  ///   <c>Commit.NoUpload</c>, one whose raw files are no longer kept 422 <c>Commit.RawExpired</c>. The action is
  ///   audited.
  /// </remarks>
  /// <param name="forge">The forge of the repository.</param>
  /// <param name="owner">The owner of the repository.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="sha">The full SHA of the commit.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>Accepted.</returns>
  [HttpPost("repos/{forge}/{owner}/{repo}/commits/{sha}/reprocess", Name = "ReprocessCommit")]
  [Authorize]
  [ProducesResponseType(StatusCodes.Status202Accepted)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
  public async Task<IActionResult> ReprocessAsync(string forge, string owner, string repo, string sha, CancellationToken cancellationToken)
  {
    await sender.ReprocessCommitAsync(forge, owner, repo, sha, cancellationToken).ConfigureAwait(false);

    return Accepted();
  }

  /// <summary>Announce the end of the uploads of a commit</summary>
  /// <remarks>
  ///   Asks for the notification of the commit as soon as every upload it received is processed, whatever
  ///   <c>codecov.notify.after_n_builds</c> and <c>manual_trigger</c> say: the last job of a pipeline calls
  ///   it, as <c>upload-complete</c> of Codecov. It takes the credential that sends the uploads of the
  ///   repository, and answers at once: the statuses are published in the background.
  /// </remarks>
  /// <param name="forge">The forge of the repository.</param>
  /// <param name="owner">The owner of the repository.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="sha">The full SHA of the commit.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>Where the uploads of the commit stand.</returns>
  [HttpPost("repos/{forge}/{owner}/{repo}/commits/{sha}/uploads-complete", Name = "CompleteUploads")]
  [AllowAnonymous]
  [ProducesResponseType<UploadsCompleteResponse>(StatusCodes.Status200OK)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<UploadsCompleteResponse>> CompleteUploadsAsync(string forge, string owner, string repo, string sha, CancellationToken cancellationToken)
  {
    var credential = UploadCredentialReader.Read(Request.Headers)
                     ?? throw new UnauthorizedException(UploadErrors.TokenInvalid, "The upload token is not valid.");

    return await sender.CompleteUploadsResponseAsync(credential, forge, owner, repo, sha, cancellationToken).ConfigureAwait(false);
  }

  #endregion
}