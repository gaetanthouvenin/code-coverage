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
  ///   The active repositories of the organization that the user of the token may read on its forge, by namespace and
  ///   name. <c>search</c> keeps the names that contain it, whatever its case. A repository the user cannot read is left
  ///   out as if it did not exist; an organization the user takes no part in answers 404.
  /// </remarks>
  /// <param name="forge">The forge of the organization.</param>
  /// <param name="org">The login of the organization.</param>
  /// <param name="search">A text the name must contain.</param>
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
    CancellationToken cancellationToken) =>
    Ok(await sender.ListRepositoriesResponseAsync(forge, org, search, cancellationToken).ConfigureAwait(false));

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
