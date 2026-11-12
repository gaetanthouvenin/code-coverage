// ------------------------------------------------------------------------------------------------
// <copyright file="CommitMapper.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Mediator;

using CodeCoverage.Api.Contracts.Commits;
using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Commits;
using CodeCoverage.Application.Features.Commits.Queries.GetCommit;
using CodeCoverage.Application.Features.Commits.Queries.GetCommitFiles;
using CodeCoverage.Application.Features.Commits.Queries.GetFileCoverage;
using CodeCoverage.Application.Features.Commits.Queries.ListCommits;
using CodeCoverage.Application.Features.Repositories;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Notifications;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Api.Mapping;

/// <summary>Converts the commits between the Domain and the API.</summary>
internal static class CommitMapper
{
  #region Methods

  /// <summary>Lists one page of the commits of a repository and converts it for the API.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="branch">The branch, or <see langword="null" /> for every commit.</param>
  /// <param name="page">The number of the page, or <see langword="null" /> for the first.</param>
  /// <param name="pageSize">The size of a page, or <see langword="null" /> for the default one.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The page.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<CommitPageResponse> GetCommitPageResponseAsync(
    this ISender sender,
    string forge,
    string owner,
    string repo,
    string? branch,
    int? page,
    int? pageSize,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    var query = new ListCommitsQuery(AddressSegments.ToRepositoryForge(forge), owner, repo, branch, page ?? 1, pageSize ?? ListCommitsQuery.DefaultPageSize);
    var commits = await sender.Send(query, cancellationToken).ConfigureAwait(false);

    return new CommitPageResponse([.. commits.Items.Select(static c => ToResponse(c, null))], commits.Page, commits.PageSize, commits.TotalCount);
  }

  /// <summary>Reads a commit and converts it for the API.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="sha">The full SHA of the commit.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The commit, with its sessions, uploads, flags and statuses.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<CommitResponse> GetCommitResponseAsync(
    this ISender sender,
    string forge,
    string owner,
    string repo,
    string sha,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    var detail = await sender.Send(new GetCommitQuery(AddressSegments.ToRepositoryForge(forge), owner, repo, AddressSegments.ToCommitSha(sha)), cancellationToken).ConfigureAwait(false);

    return ToResponse(detail.Commit, detail);
  }

  /// <summary>Lists the files of a commit and converts them for the API.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="sha">The full SHA of the commit.</param>
  /// <param name="flag">The flag, or <see langword="null" /> for the merged report.</param>
  /// <param name="path">A prefix of the paths, or <see langword="null" />.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The files, by path.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<IReadOnlyList<FileTotalsResponse>> GetCommitFilesResponseAsync(
    this ISender sender,
    string forge,
    string owner,
    string repo,
    string sha,
    string? flag,
    string? path,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    var files = await sender.Send(new GetCommitFilesQuery(AddressSegments.ToRepositoryForge(forge), owner, repo, AddressSegments.ToCommitSha(sha), flag, path), cancellationToken).ConfigureAwait(false);

    return [.. files.Select(static f => new FileTotalsResponse(f.Path, ToModel(f.Totals)))];
  }

  /// <summary>Reads the coverage of a file of a commit and converts it for the API.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="sha">The full SHA of the commit.</param>
  /// <param name="path">The path of the file.</param>
  /// <param name="flag">The flag, or <see langword="null" /> for the merged report.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The coverage of the file, line by line.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<FileCoverageResponse> GetFileCoverageResponseAsync(
    this ISender sender,
    string forge,
    string owner,
    string repo,
    string sha,
    string path,
    string? flag,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    var view = await sender.Send(new GetFileCoverageQuery(AddressSegments.ToRepositoryForge(forge), owner, repo, AddressSegments.ToCommitSha(sha), path, flag), cancellationToken).ConfigureAwait(false);
    var file = view.File;

    return new FileCoverageResponse(file.Path, view.Flag, ToModel(file.Totals), [.. file.Lines.Select(ToModel)]);
  }

  private static CommitResponse ToResponse(Commit commit, CommitDetail? detail) =>
    new(
      commit.Sha.Value,
      commit.Branch,
      commit.PullNumber,
      commit.State.ToString(),
      commit.CompletedAt is null ? null : new CoverageTotalsModel(commit.Lines, commit.Hits, commit.Misses, commit.Partials, commit.Coverage),
      commit.CompletedAt,
      detail is null ? null : [.. detail.Sessions.Select(ToModel)],
      detail is null ? null : [.. detail.Uploads.Select(UploadMapper.ToResponse)],
      detail is null ? null : [.. detail.Flags.Select(ToModel)],
      detail is null ? null : [.. detail.Statuses.Select(ToModel)]
    );

  private static SessionModel ToModel(Session session) =>
    new(session.Number, session.Kind.ToString(), session.Flags, session.Name, session.CiService, session.CarriedForwardFromSha?.Value);

  private static FlagTotalsModel ToModel(CommitFlagTotals totals) =>
    new(
      totals.Flag,
      new CoverageTotalsModel(totals.Lines, totals.Hits, totals.Misses, totals.Partials, totals.Coverage),
      totals.IsCarriedForward,
      totals.CarriedForwardFromSha?.Value
    );

  private static PostedStatusModel ToModel(PostedStatus status) =>
    new(status.Status.Context, status.Status.State.ToString(), status.Status.Description, status.TargetUrl, status.PostedAt);

  private static CoverageTotalsModel ToModel(CoverageTotals totals) =>
    new(totals.Lines, totals.Hits, totals.Misses, totals.Partials, totals.Coverage);

  private static FileLineModel ToModel(LineCoverage line) =>
    new(
      line.Line,
      CoverageMath.Classify(line).ToString(),
      line.Hits,
      line.IsBranch ? string.Create(CultureInfo.InvariantCulture, $"{line.Covered}/{line.Total}") : null
    );

  #endregion
}
