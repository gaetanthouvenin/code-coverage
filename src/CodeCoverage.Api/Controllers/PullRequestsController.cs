// ------------------------------------------------------------------------------------------------
// <copyright file="PullRequestsController.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using CodeCoverage.Api.Contracts.Comparisons;
using CodeCoverage.Api.Contracts.PullRequests;
using CodeCoverage.Api.Mapping;

namespace CodeCoverage.Api.Controllers;

/// <summary>The pull requests of a repository and their comparisons.</summary>
/// <param name="sender">The request sender.</param>
[ApiController]
[Route(CodeCoverageApi.Route)]
[Tags(CodeCoverageApi.Tag)]
public sealed class PullRequestsController(ISender sender) : ControllerBase
{
  #region Methods

  /// <summary>List the pull requests of a repository</summary>
  /// <remarks>
  ///   One page of the pull requests, the most recently updated first, each with the coverage of its head, of its patch
  ///   and the change of the project coverage when a notification compared its current head. <c>state</c> is
  ///   <c>open</c> (the default), <c>closed</c>, <c>merged</c> or <c>all</c>. A page holds 25 pull requests unless
  ///   <c>pageSize</c> asks for 1 to 100. A private repository the token cannot read answers 404, as a missing one.
  /// </remarks>
  /// <param name="forge">The forge of the repository.</param>
  /// <param name="owner">The owner of the repository.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="state">The state of the pull requests: open, closed, merged or all.</param>
  /// <param name="page">The number of the page, from 1.</param>
  /// <param name="pageSize">The number of pull requests of a page, 1 to 100.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The page.</returns>
  [HttpGet("repos/{forge}/{owner}/{repo}/pulls", Name = "ListPullRequests")]
  [Authorize]
  [ProducesResponseType<PullRequestPageResponse>(StatusCodes.Status200OK)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<PullRequestPageResponse>> ListPullRequestsAsync(
    string forge,
    string owner,
    string repo,
    [FromQuery] string? state,
    [FromQuery] int? page,
    [FromQuery] int? pageSize,
    CancellationToken cancellationToken) =>
    await sender.GetPullRequestPageResponseAsync(forge, owner, repo, state, page, pageSize, cancellationToken).ConfigureAwait(false);

  /// <summary>Read the comparison of a pull request</summary>
  /// <remarks>
  ///   The comparison the last notification stored for the current head of the pull request: project, patch per file,
  ///   impacted files, flags, indirect changes and the commits compared. Nothing is computed in the request: while no
  ///   notification compared the head, it answers 404 <c>Comparison.NotComputed</c>. A comparison whose reports changed
  ///   since is served with <c>isStale</c>. <c>diffIncomplete</c> says that the forge truncated or withheld a diff.
  /// </remarks>
  /// <param name="forge">The forge of the repository.</param>
  /// <param name="owner">The owner of the repository.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="number">The number of the pull request.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The comparison.</returns>
  [HttpGet("repos/{forge}/{owner}/{repo}/pulls/{number:int}", Name = "GetPullRequestComparison")]
  [Authorize]
  [ProducesResponseType<ComparisonResponse>(StatusCodes.Status200OK)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<ComparisonResponse>> GetPullRequestComparisonAsync(
    string forge,
    string owner,
    string repo,
    int number,
    CancellationToken cancellationToken) =>
    await sender.GetPullRequestComparisonResponseAsync(forge, owner, repo, number, cancellationToken).ConfigureAwait(false);

  #endregion
}
