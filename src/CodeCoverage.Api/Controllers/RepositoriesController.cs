// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoriesController.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using CodeCoverage.Api.Contracts.Repositories;
using CodeCoverage.Api.Mapping;

namespace CodeCoverage.Api.Controllers;

/// <summary>The tracked repositories, as the personal token of a user may read them.</summary>
/// <param name="sender">The request sender.</param>
[ApiController]
[Authorize]
[Route(CodeCoverageApi.Route)]
[Tags(CodeCoverageApi.Tag)]
public sealed class RepositoriesController(ISender sender) : ControllerBase
{
  #region Methods

  /// <summary>List the repositories of an organization</summary>
  /// <remarks>
  ///   The repositories its installation serves that the user of the token may read on its forge, by namespace and
  ///   name, each with its <c>active</c> state. <c>search</c> keeps the names that contain it, whatever its case;
  ///   <c>active</c> keeps the enabled (<c>true</c>) or the disabled (<c>false</c>) ones, as <c>repos?active=</c> of
  ///   Codecov. A repository the user cannot read is left out as if it did not exist; an organization the user takes no
  ///   part in answers 404.
  /// </remarks>
  /// <param name="forge">The forge of the organization.</param>
  /// <param name="org">The login of the organization.</param>
  /// <param name="search">A text the name must contain.</param>
  /// <param name="active">true for the enabled repositories, false for the disabled ones; both by default.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The repositories.</returns>
  [HttpGet("orgs/{forge}/{org}/repos", Name = "ListRepositories")]
  [ProducesResponseType<IReadOnlyList<RepositoryResponse>>(StatusCodes.Status200OK)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<IReadOnlyList<RepositoryResponse>>> ListRepositoriesAsync(
    string forge,
    string org,
    [FromQuery] string? search,
    [FromQuery] bool? active,
    CancellationToken cancellationToken) =>
    Ok(await sender.ListRepositoriesResponseAsync(forge, org, search, active, cancellationToken).ConfigureAwait(false));

  /// <summary>Read a repository</summary>
  /// <remarks>
  ///   The repository with its branches, the flags its uploads ever carried and the totals of each flag of the covered
  ///   head of its default branch. A private repository the user cannot read answers 404, as a missing one.
  /// </remarks>
  /// <param name="forge">The forge of the repository.</param>
  /// <param name="owner">The owner of the repository.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The repository.</returns>
  [HttpGet("repos/{forge}/{owner}/{repo}", Name = "GetRepository")]
  [ProducesResponseType<RepositoryDetailResponse>(StatusCodes.Status200OK)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<RepositoryDetailResponse>> GetRepositoryAsync(string forge, string owner, string repo, CancellationToken cancellationToken) =>
    await sender.GetRepositoryResponseAsync(forge, owner, repo, cancellationToken).ConfigureAwait(false);

  /// <summary>List the branches of a repository</summary>
  /// <remarks>The branches by name, with their head and when their covered head was last set.</remarks>
  /// <param name="forge">The forge of the repository.</param>
  /// <param name="owner">The owner of the repository.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The branches.</returns>
  [HttpGet("repos/{forge}/{owner}/{repo}/branches", Name = "ListBranches")]
  [ProducesResponseType<IReadOnlyList<BranchResponse>>(StatusCodes.Status200OK)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<IReadOnlyList<BranchResponse>>> ListBranchesAsync(string forge, string owner, string repo, CancellationToken cancellationToken) =>
    Ok(await sender.ListBranchesResponseAsync(forge, owner, repo, cancellationToken).ConfigureAwait(false));

  /// <summary>Read the coverage history of a branch</summary>
  /// <remarks>
  ///   One series per <c>flag</c> (repeat it for several; none gives the whole project), on the default branch unless
  ///   <c>branch</c> names another. The range defaults to the last 30 days. A point is dated by the committer of its
  ///   commit, else by its finalization; <c>interval</c> is <c>commit</c> (the default), <c>day</c> or <c>week</c> (ISO
  ///   weeks from Monday, in UTC), which keep the last value of each period. The 10 000 most recent totals are read at
  ///   most.
  /// </remarks>
  /// <param name="forge">The forge of the repository.</param>
  /// <param name="owner">The owner of the repository.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="branch">The branch, or the default branch.</param>
  /// <param name="flag">A flag, case-sensitive; repeated for several.</param>
  /// <param name="from">The first instant, 30 days before <c>to</c> by default.</param>
  /// <param name="to">The last instant, now by default.</param>
  /// <param name="interval">The grouping: commit, day or week.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The series.</returns>
  [HttpGet("repos/{forge}/{owner}/{repo}/history", Name = "GetCoverageHistory")]
  [ProducesResponseType<IReadOnlyList<CoverageSeriesResponse>>(StatusCodes.Status200OK)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<IReadOnlyList<CoverageSeriesResponse>>> GetCoverageHistoryAsync(
    string forge,
    string owner,
    string repo,
    [FromQuery] string? branch,
    [FromQuery] string[]? flag,
    [FromQuery] DateTimeOffset? from,
    [FromQuery] DateTimeOffset? to,
    [FromQuery] string? interval,
    CancellationToken cancellationToken) =>
    Ok(await sender.GetCoverageHistoryResponseAsync(forge, owner, repo, branch, flag, from, to, interval, cancellationToken).ConfigureAwait(false));

  /// <summary>Activate a repository</summary>
  /// <remarks>
  ///   Enables a repository an administrator disabled: its uploads are accepted and its commits notified again. Already
  ///   enabled, nothing changes and nothing is audited. Requires an administrator of the repository; a reader gets 403.
  ///   A repository its installation no longer serves answers 404.
  /// </remarks>
  /// <param name="forge">The forge of the repository.</param>
  /// <param name="owner">The owner of the repository.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>No content.</returns>
  [HttpPost("repos/{forge}/{owner}/{repo}/activate", Name = "ActivateRepository")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
  public async Task<IActionResult> ActivateAsync(string forge, string owner, string repo, CancellationToken cancellationToken)
  {
    await sender.ActivateRepositoryAsync(forge, owner, repo, cancellationToken).ConfigureAwait(false);

    return NoContent();
  }

  /// <summary>Deactivate a repository</summary>
  /// <remarks>
  ///   Disables a repository: its uploads answer 422 <c>Repository.Inactive</c> and it is no longer notified, while its
  ///   history, its API and its badge stay readable. The synchronization of the installation never enables it again.
  ///   Already disabled, nothing changes and nothing is audited. Requires an administrator of the repository.
  /// </remarks>
  /// <param name="forge">The forge of the repository.</param>
  /// <param name="owner">The owner of the repository.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>No content.</returns>
  [HttpPost("repos/{forge}/{owner}/{repo}/deactivate", Name = "DeactivateRepository")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
  public async Task<IActionResult> DeactivateAsync(string forge, string owner, string repo, CancellationToken cancellationToken)
  {
    await sender.DeactivateRepositoryAsync(forge, owner, repo, cancellationToken).ConfigureAwait(false);

    return NoContent();
  }

  /// <summary>Rotate the badge token of a repository</summary>
  /// <remarks>
  ///   Gives the repository a new badge token, returned once: the badge URLs of a private repository take it as
  ///   <c>?token=</c>, and the previous token stops working. Requires an administrator of the repository; a reader gets
  ///   403.
  /// </remarks>
  /// <param name="forge">The forge of the repository.</param>
  /// <param name="owner">The owner of the repository.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The new token.</returns>
  [HttpPost("repos/{forge}/{owner}/{repo}/badge-token", Name = "RotateBadgeToken")]
  [ProducesResponseType<BadgeTokenResponse>(StatusCodes.Status200OK)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<BadgeTokenResponse>> RotateBadgeTokenAsync(string forge, string owner, string repo, CancellationToken cancellationToken) =>
    await sender.RotateBadgeTokenResponseAsync(forge, owner, repo, cancellationToken).ConfigureAwait(false);

  /// <summary>Import the badge token of a repository</summary>
  /// <remarks>
  ///   Keeps a token given by the caller, e.g. the graphing token of Codecov, so that the badge URLs of a README keep
  ///   working when only their host changes. 1 to 64 letters, digits, underscores or hyphens. Requires an administrator
  ///   of the repository.
  /// </remarks>
  /// <param name="forge">The forge of the repository.</param>
  /// <param name="owner">The owner of the repository.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="request">The token.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>No content.</returns>
  [HttpPut("repos/{forge}/{owner}/{repo}/badge-token", Name = "ImportBadgeToken")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> ImportBadgeTokenAsync(string forge, string owner, string repo, ImportBadgeTokenRequest request, CancellationToken cancellationToken)
  {
    await sender.ImportBadgeTokenAsync(forge, owner, repo, request, cancellationToken).ConfigureAwait(false);

    return NoContent();
  }

  /// <summary>Read the effective configuration of a repository</summary>
  /// <remarks>
  ///   The configuration of the repository at the covered head of its default branch, merged over the one of its
  ///   organization and the defaults, with its source and the YAML that was found. Nothing is fetched from the forge in
  ///   the request: when no configuration was kept for that commit yet, the YAML of the default branch and of the
  ///   organization are merged, with <c>isResolvedAtSha</c> false.
  /// </remarks>
  /// <param name="forge">The forge of the repository.</param>
  /// <param name="owner">The owner of the repository.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The effective configuration.</returns>
  [HttpGet("repos/{forge}/{owner}/{repo}/config", Name = "GetEffectiveConfig")]
  [ProducesResponseType<EffectiveConfigResponse>(StatusCodes.Status200OK)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<EffectiveConfigResponse>> GetEffectiveConfigAsync(string forge, string owner, string repo, CancellationToken cancellationToken) =>
    await sender.GetEffectiveConfigResponseAsync(forge, owner, repo, cancellationToken).ConfigureAwait(false);

  #endregion
}
