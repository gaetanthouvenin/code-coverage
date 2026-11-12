// ------------------------------------------------------------------------------------------------
// <copyright file="ApiTokensController.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using CodeCoverage.Api.Contracts.Tokens;
using CodeCoverage.Api.Mapping;

namespace CodeCoverage.Api.Controllers;

/// <summary>The personal API tokens of the user of the token that authenticates the request.</summary>
/// <param name="sender">The request sender.</param>
/// <remarks>
///   Every action is filtered by the user, and stays within the token of the request: a token of an organization reaches
///   the tokens of that organization, a token of the instance the tokens of the instance.
/// </remarks>
[ApiController]
[Authorize]
[Route(CodeCoverageApi.Route + "/api-tokens")]
[Tags(CodeCoverageApi.Tag)]
public sealed class ApiTokensController(ISender sender) : ControllerBase
{
  #region Methods

  /// <summary>Create a personal API token</summary>
  /// <remarks>
  ///   A token of an organization (<c>forge</c> and <c>organization</c>) is created by any member of that active
  ///   organization; a token of the instance (neither) by an administrator of the instance only. The token grants nothing
  ///   beyond the rights of its user. The value (<c>cvp_…</c>) is returned this one time.
  /// </remarks>
  /// <param name="request">The token to create.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The token, with its value in clear.</returns>
  [HttpPost(Name = "CreateApiToken")]
  [ProducesResponseType<IssuedApiTokenResponse>(StatusCodes.Status201Created)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<IssuedApiTokenResponse>> CreateAsync(CreateApiTokenRequest request, CancellationToken cancellationToken) =>
    StatusCode(StatusCodes.Status201Created, await sender.CreateApiTokenResponseAsync(request, cancellationToken).ConfigureAwait(false));

  /// <summary>List the personal API tokens</summary>
  /// <remarks>The tokens of the user, the most recent first, revoked ones included, never with a value.</remarks>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The tokens.</returns>
  [HttpGet(Name = "ListApiTokens")]
  [ProducesResponseType<IReadOnlyList<ApiTokenResponse>>(StatusCodes.Status200OK)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
  public async Task<ActionResult<IReadOnlyList<ApiTokenResponse>>> ListAsync(CancellationToken cancellationToken) =>
    Ok(await sender.ListApiTokensResponseAsync(cancellationToken).ConfigureAwait(false));

  /// <summary>Revoke a personal API token</summary>
  /// <remarks>A token of another user, or out of reach of the token of the request, is unknown.</remarks>
  /// <param name="id">The identifier of the token.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>No content.</returns>
  [HttpDelete("{id:long}", Name = "RevokeApiToken")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> RevokeAsync(long id, CancellationToken cancellationToken)
  {
    await sender.RevokeApiTokenAsync(id, cancellationToken).ConfigureAwait(false);

    return NoContent();
  }

  #endregion
}
