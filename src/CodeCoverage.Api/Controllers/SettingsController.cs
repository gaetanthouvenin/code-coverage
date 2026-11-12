// ------------------------------------------------------------------------------------------------
// <copyright file="SettingsController.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using CodeCoverage.Api.Contracts.Settings;
using CodeCoverage.Api.Mapping;

namespace CodeCoverage.Api.Controllers;

/// <summary>The settings of the instance, changed by its administrators without a restart.</summary>
/// <param name="sender">The request sender.</param>
[ApiController]
[Authorize]
[Route(CodeCoverageApi.Route + "/settings")]
[Tags(CodeCoverageApi.Tag)]
public sealed class SettingsController(ISender sender) : ControllerBase
{
  #region Methods

  /// <summary>Read the settings of the instance</summary>
  /// <remarks>
  ///   Every setting of the instance with its effective value and where it comes from. A secret only says whether it is
  ///   defined: its value never leaves the server. Requires an administrator of the instance and a token of the instance;
  ///   a token of an organization answers 403.
  /// </remarks>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The settings.</returns>
  [HttpGet(Name = "GetSettings")]
  [ProducesResponseType<IReadOnlyList<SettingResponse>>(StatusCodes.Status200OK)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
  public async Task<ActionResult<IReadOnlyList<SettingResponse>>> GetAsync(CancellationToken cancellationToken) =>
    Ok(await sender.GetSettingsResponseAsync(cancellationToken).ConfigureAwait(false));

  /// <summary>Change a setting of the instance</summary>
  /// <remarks>
  ///   Saves the value, checked against the type and the bounds of the setting, and applies it without a restart unless
  ///   the setting says otherwise. A value provided by the environment is read-only (422
  ///   <c>Setting.ProvidedByEnvironment</c>); a secret is replaced through its own route (422 <c>Setting.IsSecret</c>);
  ///   a setting of an organization answers 404.
  /// </remarks>
  /// <param name="key">The key of the setting, e.g. <c>Uploads:MaxSizeMb</c>.</param>
  /// <param name="request">The new value.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>No content.</returns>
  [HttpPut("{key}", Name = "UpdateSetting")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
  public async Task<IActionResult> UpdateAsync(string key, UpdateSettingRequest request, CancellationToken cancellationToken)
  {
    await sender.UpdateSettingAsync(key, request, cancellationToken).ConfigureAwait(false);

    return NoContent();
  }

  /// <summary>Replace a secret of the instance</summary>
  /// <remarks>
  ///   Stores the secret protected; it is never read back. A plain setting answers 422 <c>Setting.NotSecret</c>, a secret
  ///   provided by the environment 422 <c>Setting.ProvidedByEnvironment</c>.
  /// </remarks>
  /// <param name="key">The key of the secret, e.g. <c>GitHub:WebhookSecret</c>.</param>
  /// <param name="request">The new secret.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>No content.</returns>
  [HttpPut("{key}/secret", Name = "ReplaceSecret")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
  public async Task<IActionResult> ReplaceSecretAsync(string key, ReplaceSecretRequest request, CancellationToken cancellationToken)
  {
    await sender.ReplaceSecretAsync(key, request, cancellationToken).ConfigureAwait(false);

    return NoContent();
  }

  /// <summary>Reset a setting of the instance</summary>
  /// <remarks>
  ///   Removes the value saved from the console: the setting goes back to its default, and a secret saved from the
  ///   console is cleared. A value provided by the environment answers 422 <c>Setting.ProvidedByEnvironment</c>.
  /// </remarks>
  /// <param name="key">The key of the setting.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>No content.</returns>
  [HttpDelete("{key}", Name = "ResetSetting")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
  public async Task<IActionResult> ResetAsync(string key, CancellationToken cancellationToken)
  {
    await sender.ResetSettingAsync(key, cancellationToken).ConfigureAwait(false);

    return NoContent();
  }

  #endregion
}
