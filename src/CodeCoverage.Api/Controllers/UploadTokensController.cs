// ------------------------------------------------------------------------------------------------
// <copyright file="UploadTokensController.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using CodeCoverage.Api.Contracts.Tokens;
using CodeCoverage.Api.Mapping;

namespace CodeCoverage.Api.Controllers;

/// <summary>The upload tokens of an organization or of a repository, managed with a personal API token.</summary>
/// <param name="sender">The request sender.</param>
/// <remarks>
///   An upload token is always addressed under its organization or its repository (ADR-013): a token of another
///   organization or repository is unknown. The value in clear is returned by the creation only, once.
/// </remarks>
[ApiController]
[Authorize]
[Route(CodeCoverageApi.Route)]
[Tags(CodeCoverageApi.Tag)]
public sealed class UploadTokensController(ISender sender) : ControllerBase
{
  #region Methods

  /// <summary>Create an upload token of an organization</summary>
  /// <remarks>
  ///   Generates a token (<c>cvc_…</c>) for every repository of the organization, or imports the value of a
  ///   <c>CODECOV_TOKEN</c>. The value is returned this one time: only its hash is kept. Requires an administrator of the
  ///   organization.
  /// </remarks>
  /// <param name="forge">The forge of the organization.</param>
  /// <param name="org">The login of the organization.</param>
  /// <param name="request">The token to create.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The token, with its value in clear.</returns>
  [HttpPost("orgs/{forge}/{org}/upload-tokens", Name = "CreateOrganizationUploadToken")]
  [ProducesResponseType<IssuedUploadTokenResponse>(StatusCodes.Status201Created)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
  public async Task<ActionResult<IssuedUploadTokenResponse>> CreateOrganizationTokenAsync(
    string forge,
    string org,
    CreateUploadTokenRequest request,
    CancellationToken cancellationToken) =>
    StatusCode(StatusCodes.Status201Created, await sender.CreateOrganizationUploadTokenResponseAsync(forge, org, request, cancellationToken).ConfigureAwait(false));

  /// <summary>List the upload tokens of an organization</summary>
  /// <remarks>The tokens of the organization itself, the most recent first, revoked ones included, never with a value.</remarks>
  /// <param name="forge">The forge of the organization.</param>
  /// <param name="org">The login of the organization.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The tokens.</returns>
  [HttpGet("orgs/{forge}/{org}/upload-tokens", Name = "ListOrganizationUploadTokens")]
  [ProducesResponseType<IReadOnlyList<UploadTokenResponse>>(StatusCodes.Status200OK)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<IReadOnlyList<UploadTokenResponse>>> ListOrganizationTokensAsync(string forge, string org, CancellationToken cancellationToken) =>
    Ok(await sender.ListOrganizationUploadTokensResponseAsync(forge, org, cancellationToken).ConfigureAwait(false));

  /// <summary>Revoke an upload token of an organization</summary>
  /// <remarks>A token of a repository, or of another organization, is unknown here.</remarks>
  /// <param name="forge">The forge of the organization.</param>
  /// <param name="org">The login of the organization.</param>
  /// <param name="id">The identifier of the token.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>No content.</returns>
  [HttpDelete("orgs/{forge}/{org}/upload-tokens/{id:long}", Name = "RevokeOrganizationUploadToken")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> RevokeOrganizationTokenAsync(string forge, string org, long id, CancellationToken cancellationToken)
  {
    await sender.RevokeOrganizationUploadTokenAsync(forge, org, id, cancellationToken).ConfigureAwait(false);

    return NoContent();
  }

  /// <summary>Create an upload token of a repository</summary>
  /// <remarks>
  ///   Generates a token (<c>cvc_…</c>) for one repository, or imports the value of a <c>CODECOV_TOKEN</c>. The value is
  ///   returned this one time: only its hash is kept. Requires the administration of the repository on its forge.
  /// </remarks>
  /// <param name="forge">The forge of the repository.</param>
  /// <param name="owner">The owner of the repository.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="request">The token to create.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The token, with its value in clear.</returns>
  [HttpPost("repos/{forge}/{owner}/{repo}/upload-tokens", Name = "CreateRepositoryUploadToken")]
  [ProducesResponseType<IssuedUploadTokenResponse>(StatusCodes.Status201Created)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
  public async Task<ActionResult<IssuedUploadTokenResponse>> CreateRepositoryTokenAsync(
    string forge,
    string owner,
    string repo,
    CreateUploadTokenRequest request,
    CancellationToken cancellationToken) =>
    StatusCode(StatusCodes.Status201Created, await sender.CreateRepositoryUploadTokenResponseAsync(forge, owner, repo, request, cancellationToken).ConfigureAwait(false));

  /// <summary>List the upload tokens of a repository</summary>
  /// <remarks>The tokens of the repository, the most recent first, revoked ones included, never with a value.</remarks>
  /// <param name="forge">The forge of the repository.</param>
  /// <param name="owner">The owner of the repository.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The tokens.</returns>
  [HttpGet("repos/{forge}/{owner}/{repo}/upload-tokens", Name = "ListRepositoryUploadTokens")]
  [ProducesResponseType<IReadOnlyList<UploadTokenResponse>>(StatusCodes.Status200OK)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<IReadOnlyList<UploadTokenResponse>>> ListRepositoryTokensAsync(string forge, string owner, string repo, CancellationToken cancellationToken) =>
    Ok(await sender.ListRepositoryUploadTokensResponseAsync(forge, owner, repo, cancellationToken).ConfigureAwait(false));

  /// <summary>Revoke an upload token of a repository</summary>
  /// <remarks>A token of the organization, or of another repository, is unknown here.</remarks>
  /// <param name="forge">The forge of the repository.</param>
  /// <param name="owner">The owner of the repository.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="id">The identifier of the token.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>No content.</returns>
  [HttpDelete("repos/{forge}/{owner}/{repo}/upload-tokens/{id:long}", Name = "RevokeRepositoryUploadToken")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> RevokeRepositoryTokenAsync(string forge, string owner, string repo, long id, CancellationToken cancellationToken)
  {
    await sender.RevokeRepositoryUploadTokenAsync(forge, owner, repo, id, cancellationToken).ConfigureAwait(false);

    return NoContent();
  }

  #endregion
}
