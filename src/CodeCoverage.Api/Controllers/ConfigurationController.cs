// ------------------------------------------------------------------------------------------------
// <copyright file="ConfigurationController.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

using CodeCoverage.Api.Configuration.RateLimiting;
using CodeCoverage.Api.Contracts.Configuration;
using CodeCoverage.Api.Mapping;

namespace CodeCoverage.Api.Controllers;

/// <summary><c>/api/v1/validate</c>: the validation of the coverage configuration.</summary>
/// <param name="sender">The request sender.</param>
[ApiController]
[Route(CodeCoverageApi.Route)]
[Tags(CodeCoverageApi.Tag)]
public sealed class ConfigurationController(ISender sender) : ControllerBase
{
  #region Fields

  // The DATA_UPLOAD_MAX_MEMORY_SIZE default of Codecov.
  private const long MaximumBodyBytes = 2_621_440;

  #endregion

  #region Methods

  /// <summary>Validate a codecov.yml file</summary>
  /// <remarks>
  ///   Validates the posted YAML text on its own, without authentication: the body is the file, whatever
  ///   its content type, as in <c>curl --data-binary @codecov.yml</c>. The answer is 200 if the file can be
  ///   used, with its normalized content and what could not be applied as written, and 400 with the same
  ///   body if it cannot be read. The requests are limited per client address.
  /// </remarks>
  /// <returns>The result of the validation.</returns>
  [HttpPost("validate", Name = "ValidateCoverageYaml")]
  [AllowAnonymous]
  [EnableRateLimiting(ValidateRateLimiterConfiguration.PolicyName)]
  [RequestSizeLimit(MaximumBodyBytes)]
  [ProducesResponseType<ValidationResultResponse>(StatusCodes.Status200OK)]
  [ProducesResponseType<ValidationResultResponse>(StatusCodes.Status400BadRequest)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
  public async Task<IActionResult> ValidateAsync()
  {
    // No action parameter, not even the token: binding one makes MVC read a form body (the content type
    // curl sends by default) before the action, which would then find the body already consumed.
    var cancellationToken = HttpContext.RequestAborted;

    using var reader = new StreamReader(Request.Body, leaveOpen: true);

    var yaml = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    var response = await sender.GetValidationResultResponseAsync(yaml, cancellationToken).ConfigureAwait(false);

    return response.Valid ? Ok(response) : BadRequest(response);
  }

  #endregion
}
