// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubWebhooksController.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using CodeCoverage.Api.Mapping;

namespace CodeCoverage.Api.Controllers;

/// <summary><c>/api/v1/github/webhooks</c>: the reception of the webhooks of the GitHub App.</summary>
/// <param name="sender">The request sender.</param>
[ApiController]
[Route(CodeCoverageApi.Route)]
[Tags(CodeCoverageApi.Tag)]
public sealed class GitHubWebhooksController(ISender sender) : ControllerBase
{
  #region Fields

  // GitHub caps a webhook payload at 25 MB.
  private const long MaximumBodyBytes = 26_214_400;

  #endregion

  #region Methods

  /// <summary>Receive a webhook of the GitHub App</summary>
  /// <remarks>
  ///   Stores the delivery and queues its handling, without calling GitHub. The request is authenticated by its
  ///   <c>X-Hub-Signature-256</c> header. The answer is 202 for a delivery accepted, or dropped because no active
  ///   organization needs it, and 200 for a delivery already known.
  /// </remarks>
  /// <returns>No content.</returns>
  [HttpPost("github/webhooks", Name = "ReceiveGitHubWebhook")]
  [AllowAnonymous]
  [RequestSizeLimit(MaximumBodyBytes)]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status202Accepted)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
  public async Task<IActionResult> ReceiveAsync()
  {
    // No action parameter, not even the token: the signature covers the raw body, which nothing may read before.
    var cancellationToken = HttpContext.RequestAborted;

    using var body = new MemoryStream();

    await Request.Body.CopyToAsync(body, cancellationToken).ConfigureAwait(false);

    var status = await sender.ReceiveGitHubWebhookAsync(
                   Request.Headers["X-GitHub-Delivery"].ToString(),
                   Request.Headers["X-GitHub-Event"].ToString(),
                   Request.Headers["X-Hub-Signature-256"].FirstOrDefault(),
                   body.ToArray(),
                   cancellationToken
                 ).ConfigureAwait(false);

    return StatusCode(status);
  }

  #endregion
}
