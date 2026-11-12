// ------------------------------------------------------------------------------------------------
// <copyright file="CommitMapper.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Features.Commits.Queries.ListCommits;
using CodeCoverage.Application.Features.Repositories.Queries.GetEffectiveConfig;
using CodeCoverage.Application.Features.Repositories.Queries.GetRepository;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Config;
using CodeCoverage.Web.ViewModels.Commits;

namespace CodeCoverage.Web.Mapping;

/// <summary>Reads the commits for the pages and converts them to view models: no page names a Domain type.</summary>
internal static class CommitMapper
{
  #region Fields

  /// <summary>The number of commits of a page.</summary>
  public const int PageSize = ListCommitsQuery.DefaultPageSize;

  #endregion

  #region Methods

  /// <summary>Reads one page of the commits of a branch, formatted as the configuration of the repository says.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="name">The name of the repository.</param>
  /// <param name="branch">The branch, or <see langword="null" /> for the default one.</param>
  /// <param name="page">The page asked; kept within the pages a list can hold.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The page, or <see langword="null" /> when the forge segment is not one the console serves.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<CommitPageViewModel?> GetCommitPageViewModelAsync(
    this ISender sender,
    string forge,
    string owner,
    string name,
    string? branch,
    int page,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    if (!RepositoryMapper.TryParseForge(forge, out var parsed))
    {
      return null;
    }

    var overview = await sender.Send(new GetRepositoryQuery(parsed, owner, name), cancellationToken).ConfigureAwait(false);
    var listed = string.IsNullOrEmpty(branch) ? overview.Repository.DefaultBranch : branch;
    var config = (await sender.Send(new GetEffectiveConfigQuery(parsed, owner, name), cancellationToken).ConfigureAwait(false)).Config;
    var query = new ListCommitsQuery(parsed, owner, name, listed, Math.Clamp(page, 1, ListCommitsQuery.MaxPage), PageSize);
    var commits = await sender.Send(query, cancellationToken).ConfigureAwait(false);
    IReadOnlyList<string> branches = [.. overview.Branches.Select(static b => b.Name).Append(overview.Repository.DefaultBranch).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    return new CommitPageViewModel(
      listed,
      branches,
      [.. commits.Items.Select(c => ToRow(c, config))],
      commits.Page,
      commits.PageSize,
      commits.TotalCount
    );
  }

  /// <summary>Converts the state of a commit: known and pending are processed, compacted keeps its totals.</summary>
  /// <param name="state">The state.</param>
  /// <returns>The state shown.</returns>
  internal static CommitStatus ToStatus(CommitState state) => state switch
  {
    CommitState.Complete => CommitStatus.Complete,
    CommitState.Compacted => CommitStatus.Compacted,
    CommitState.Error => CommitStatus.Error,
    var _ => CommitStatus.Processing,
  };

  // The first line of the message: the subject of the commit.
  private static string? Subject(string? message) => message?.Split('\n', 2)[0].TrimEnd('\r');

  private static CommitRowViewModel ToRow(CommitSummary summary, CoverageConfig config)
  {
    var commit = summary.Commit;

    return new CommitRowViewModel(
      commit.Sha.Value,
      DisplayFormatter.ShortSha(commit.Sha),
      Subject(summary.Git?.Message),
      summary.Git?.AuthorLogin,
      summary.Git?.CommittedAt ?? commit.CompletedAt ?? commit.CreatedAt,
      DisplayFormatter.Percent(commit.Coverage, config.Precision, config.Rounding),
      DisplayFormatter.Delta(summary.Delta, config.Precision, config.Rounding),
      summary.Delta is { } delta ? Math.Sign(delta) : 0,
      ToStatus(commit.State),
      commit.SessionCount
    );
  }

  #endregion
}
