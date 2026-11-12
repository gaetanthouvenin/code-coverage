// ------------------------------------------------------------------------------------------------
// <copyright file="PullRequestMapper.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Features.PullRequests.Queries.ListPullRequests;
using CodeCoverage.Application.Features.Repositories.Queries.GetEffectiveConfig;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.PullRequests;
using CodeCoverage.Web.ViewModels.PullRequests;

namespace CodeCoverage.Web.Mapping;

/// <summary>Reads the pull requests for the pages and converts them to view models: no page names a Domain type.</summary>
internal static class PullRequestMapper
{
  #region Fields

  /// <summary>The number of pull requests of a page.</summary>
  public const int PageSize = ListPullRequestsQuery.DefaultPageSize;

  #endregion

  #region Methods

  /// <summary>Reads one page of the pull requests of a repository in one state, formatted as its configuration says.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="name">The name of the repository.</param>
  /// <param name="state">The state asked; an unknown one lists the open pull requests.</param>
  /// <param name="page">The page asked; kept within the pages a list can hold.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The page, or <see langword="null" /> when the forge segment is not one the console serves.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<PullRequestPageViewModel?> GetPullRequestPageViewModelAsync(
    this ISender sender,
    string forge,
    string owner,
    string name,
    string? state,
    int page,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    if (!RepositoryMapper.TryParseForge(forge, out var parsed))
    {
      return null;
    }

    var listed = ListPullRequestsQuery.TryParseState(state, out _) ? state! : ListPullRequestsQuery.OpenState;
    var config = (await sender.Send(new GetEffectiveConfigQuery(parsed, owner, name), cancellationToken).ConfigureAwait(false)).Config;
    var query = new ListPullRequestsQuery(parsed, owner, name, listed, Math.Clamp(page, 1, ListPullRequestsQuery.MaxPage), PageSize);
    var pulls = await sender.Send(query, cancellationToken).ConfigureAwait(false);

    return new PullRequestPageViewModel(listed, [.. pulls.Items.Select(p => ToRow(p, config))], pulls.Page, pulls.PageSize, pulls.TotalCount);
  }

  private static PullRequestRowViewModel ToRow(PullRequestSummary summary, CoverageConfig config)
  {
    var pull = summary.PullRequest;
    var delta = summary.Comparison?.Delta;

    return new PullRequestRowViewModel(
      pull.Number,
      pull.Title,
      pull.AuthorLogin,
      pull.HeadBranch,
      pull.BaseBranch,
      pull.State switch
      {
        PullRequestState.Merged => PullRequestStatus.Merged,
        PullRequestState.Closed => PullRequestStatus.Closed,
        var _ => PullRequestStatus.Open,
      },
      summary.Comparison is { } comparison ? DisplayFormatter.Percent(comparison.Patch.Coverage, config.Precision, config.Rounding) : null,
      DisplayFormatter.Delta(delta, config.Precision, config.Rounding),
      delta is { } value ? Math.Sign(value) : 0
    );
  }

  #endregion
}
