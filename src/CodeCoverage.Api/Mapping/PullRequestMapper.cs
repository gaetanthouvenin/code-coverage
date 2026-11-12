// ------------------------------------------------------------------------------------------------
// <copyright file="PullRequestMapper.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Api.Contracts.PullRequests;
using CodeCoverage.Application.Features.PullRequests.Queries.ListPullRequests;
using CodeCoverage.Domain.PullRequests;

namespace CodeCoverage.Api.Mapping;

/// <summary>Converts the pull requests between the Domain and the API.</summary>
internal static class PullRequestMapper
{
  #region Methods

  /// <summary>Lists one page of the pull requests of a repository and converts it for the API.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="state">The state, or <see langword="null" /> for the open pull requests.</param>
  /// <param name="page">The number of the page, or <see langword="null" /> for the first.</param>
  /// <param name="pageSize">The size of a page, or <see langword="null" /> for the default one.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The page.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<PullRequestPageResponse> GetPullRequestPageResponseAsync(
    this ISender sender,
    string forge,
    string owner,
    string repo,
    string? state,
    int? page,
    int? pageSize,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    var query = new ListPullRequestsQuery(
      AddressSegments.ToRepositoryForge(forge),
      owner,
      repo,
      state ?? ListPullRequestsQuery.OpenState,
      page ?? 1,
      pageSize ?? ListPullRequestsQuery.DefaultPageSize
    );
    var pulls = await sender.Send(query, cancellationToken).ConfigureAwait(false);

    return new PullRequestPageResponse([.. pulls.Items.Select(ToResponse)], pulls.Page, pulls.PageSize, pulls.TotalCount);
  }

  private static PullRequestResponse ToResponse(PullRequestSummary summary)
  {
    var pull = summary.PullRequest;
    var comparison = summary.Comparison;

    return new PullRequestResponse(
      pull.Number,
      pull.Title,
      pull.AuthorLogin,
      pull.State.ToString(),
      pull.HeadSha.Value,
      pull.HeadBranch,
      pull.BaseBranch,
      pull.UpdatedAt,
      comparison?.Head.Coverage,
      comparison?.Patch.Coverage,
      comparison?.Delta
    );
  }

  #endregion
}
