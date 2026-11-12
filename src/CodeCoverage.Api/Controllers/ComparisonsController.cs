// ------------------------------------------------------------------------------------------------
// <copyright file="ComparisonsController.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using CodeCoverage.Api.Contracts.Comparisons;
using CodeCoverage.Api.Mapping;

namespace CodeCoverage.Api.Controllers;

/// <summary>The comparisons of the commits of a repository.</summary>
/// <param name="sender">The request sender.</param>
[ApiController]
[Route(CodeCoverageApi.Route)]
[Tags(CodeCoverageApi.Tag)]
public sealed class ComparisonsController(ISender sender) : ControllerBase
{
  #region Methods

  /// <summary>Compare two commits</summary>
  /// <remarks>
  ///   The comparison a notification stored for the head commit against the base commit, the one of its push build
  ///   first. Nothing is computed in the request: a pair no notification compared answers 404
  ///   <c>Comparison.NotComputed</c>. Both SHAs are full; a missing, short or unknown one answers 404
  ///   <c>Commit.NotFound</c>.
  /// </remarks>
  /// <param name="forge">The forge of the repository.</param>
  /// <param name="owner">The owner of the repository.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="baseSha">The full SHA of the base commit.</param>
  /// <param name="head">The full SHA of the head commit.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The comparison.</returns>
  [HttpGet("repos/{forge}/{owner}/{repo}/compare", Name = "CompareCommits")]
  [Authorize]
  [ProducesResponseType<ComparisonResponse>(StatusCodes.Status200OK)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<ComparisonResponse>> CompareAsync(
    string forge,
    string owner,
    string repo,
    [FromQuery(Name = "base")] string? baseSha,
    [FromQuery] string? head,
    CancellationToken cancellationToken) =>
    await sender.GetComparisonResponseAsync(forge, owner, repo, baseSha, head, cancellationToken).ConfigureAwait(false);

  #endregion
}
