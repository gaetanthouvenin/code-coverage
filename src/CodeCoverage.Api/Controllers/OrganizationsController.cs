// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationsController.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using CodeCoverage.Api.Contracts.Organizations;
using CodeCoverage.Api.Mapping;

namespace CodeCoverage.Api.Controllers;

/// <summary>The organizations of the user, and the settings an administrator of an organization changes.</summary>
/// <param name="sender">The request sender.</param>
[ApiController]
[Authorize]
[Route(CodeCoverageApi.Route + "/orgs")]
[Tags(CodeCoverageApi.Tag)]
public sealed class OrganizationsController(ISender sender) : ControllerBase
{
  #region Methods

  /// <summary>List the organizations of the user</summary>
  /// <remarks>
  ///   The active organizations the user belongs to, with its role. A token of an organization lists that organization
  ///   only; a token of the instance every organization of its user.
  /// </remarks>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The organizations.</returns>
  [HttpGet(Name = "ListMyOrganizations")]
  [ProducesResponseType<IReadOnlyList<OrganizationResponse>>(StatusCodes.Status200OK)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
  public async Task<ActionResult<IReadOnlyList<OrganizationResponse>>> ListAsync(CancellationToken cancellationToken) =>
    Ok(await sender.ListMyOrganizationsResponseAsync(cancellationToken).ConfigureAwait(false));

  /// <summary>Read the settings of an organization</summary>
  /// <remarks>
  ///   The five settings of the scope of an organization, with their value and its source (the organization or the
  ///   catalog). None is secret, and there is no secret route. Requires an administrator of the organization.
  /// </remarks>
  /// <param name="forge">The forge of the organization.</param>
  /// <param name="org">The login of the organization.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The settings.</returns>
  [HttpGet("{forge}/{org}/settings", Name = "GetOrganizationSettings")]
  [ProducesResponseType<IReadOnlyList<OrganizationSettingResponse>>(StatusCodes.Status200OK)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<IReadOnlyList<OrganizationSettingResponse>>> GetSettingsAsync(string forge, string org, CancellationToken cancellationToken) =>
    Ok(await sender.GetOrganizationSettingsResponseAsync(forge, org, cancellationToken).ConfigureAwait(false));

  /// <summary>Change a setting of an organization</summary>
  /// <remarks>
  ///   The value is checked against the type and the bounds of the setting, and applies from the next request or job of
  ///   the organization, without a restart. A setting of the instance answers 404. Requires an administrator of the
  ///   organization.
  /// </remarks>
  /// <param name="forge">The forge of the organization.</param>
  /// <param name="org">The login of the organization.</param>
  /// <param name="key">The key of the setting, e.g. <c>Notifications:StatusContextPrefix</c>.</param>
  /// <param name="request">The new value.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>No content.</returns>
  [HttpPut("{forge}/{org}/settings/{key}", Name = "UpdateOrganizationSetting")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
  public async Task<IActionResult> UpdateSettingAsync(string forge, string org, string key, UpdateOrganizationSettingRequest request, CancellationToken cancellationToken)
  {
    await sender.UpdateOrganizationSettingAsync(forge, org, key, request, cancellationToken).ConfigureAwait(false);

    return NoContent();
  }

  /// <summary>Reset a setting of an organization</summary>
  /// <remarks>Puts the setting back to the default of the catalog. Requires an administrator of the organization.</remarks>
  /// <param name="forge">The forge of the organization.</param>
  /// <param name="org">The login of the organization.</param>
  /// <param name="key">The key of the setting.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>No content.</returns>
  [HttpDelete("{forge}/{org}/settings/{key}", Name = "ResetOrganizationSetting")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> ResetSettingAsync(string forge, string org, string key, CancellationToken cancellationToken)
  {
    await sender.ResetOrganizationSettingAsync(forge, org, key, cancellationToken).ConfigureAwait(false);

    return NoContent();
  }

  #endregion
}
