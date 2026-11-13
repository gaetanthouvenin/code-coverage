// ------------------------------------------------------------------------------------------------
// <copyright file="PullRequestMapper.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Features.PullRequests.Queries.ListPullRequests;
using CodeCoverage.Application.Features.Repositories.Queries.GetEffectiveConfig;
using CodeCoverage.Domain.Config;
using CodeCoverage.Web.ViewModels.Repositories;
using CodeCoverage.Domain.Organizations;
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

  /// <summary>Reads the open pull requests of a repository, for the panel of the overview.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge of the repository.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="name">The name of the repository.</param>
  /// <param name="format">How the repository writes a coverage, already read by the overview.</param>
  /// <param name="count">The number of rows.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The rows, the newest first.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> or <paramref name="format" /> is <see langword="null" />.</exception>
  /// <remarks>The configuration is not read again: the overview passes what it already has.</remarks>
  internal static async Task<IReadOnlyList<PullRequestRowViewModel>> ListOpenPullRequestRowsAsync(
    this ISender sender,
    Forge forge,
    string owner,
    string name,
    CoverageFormatViewModel format,
    int count,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);
    ArgumentNullException.ThrowIfNull(format);

    var query = new ListPullRequestsQuery(forge, owner, name, ListPullRequestsQuery.OpenState, 1, count);
    var pulls = await sender.Send(query, cancellationToken).ConfigureAwait(false);
    var rounding = Enum.Parse<RoundingMode>(format.Rounding);

    return [.. pulls.Items.Select(p => ToRow(p, format.Precision, rounding))];
  }

  private static PullRequestRowViewModel ToRow(PullRequestSummary summary, CoverageConfig config) => ToRow(summary, config.Precision, config.Rounding);

  private static PullRequestRowViewModel ToRow(PullRequestSummary summary, int precision, RoundingMode rounding)
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
      summary.Comparison is { } comparison ? DisplayFormatter.Percent(comparison.Patch.Coverage, precision, rounding) : null,
      DisplayFormatter.Delta(delta, precision, rounding),
      delta is { } value ? Math.Sign(value) : 0
    );
  }

  #endregion
}
