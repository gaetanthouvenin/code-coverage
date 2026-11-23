// ------------------------------------------------------------------------------------------------
// <copyright file="AzureDevOpsWebhooksController.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using CodeCoverage.Api.Mapping;

namespace CodeCoverage.Api.Controllers;

/// <summary><c>/api/v1/azure-devops/webhooks/{connectionId}</c>: the reception of the service hooks of Azure DevOps.</summary>
/// <param name="sender">The request sender.</param>
[ApiController]
[Route(CodeCoverageApi.Route)]
[Tags(CodeCoverageApi.Tag)]
public sealed class AzureDevOpsWebhooksController(ISender sender) : ControllerBase
{
  #region Fields

  // The same ceiling as the deliveries of GitHub: a Minimal service hook is far smaller.
  private const long MaximumBodyBytes = 26_214_400;

  #endregion

  #region Methods

  /// <summary>Receive a service hook of an Azure DevOps connection</summary>
  /// <remarks>
  ///   Stores the delivery and queues its handling, without calling Azure DevOps. The address names the connection, and
  ///   the request is authenticated by its <c>X-CodeCoverage-Webhook-Secret</c> header, compared with the current and
  ///   the previous webhook secret of that connection: Azure DevOps does not sign its service hooks (ADR-014). The
  ///   identifier of the delivery and the event are the <c>id</c> and the <c>eventType</c> of the body. The answer is 202
  ///   for a delivery accepted, or dropped because its organization or its connection is not active, and 200 for a
  ///   delivery already known.
  /// </remarks>
  /// <param name="connectionId">The identifier of the connection whose subscription sent the service hook.</param>
  /// <returns>No content.</returns>
  [HttpPost("azure-devops/webhooks/{connectionId}", Name = "ReceiveAzureDevOpsWebhook")]
  [AllowAnonymous]
  [RequestSizeLimit(MaximumBodyBytes)]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status202Accepted)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
  public async Task<IActionResult> ReceiveAsync(string connectionId)
  {
    ArgumentNullException.ThrowIfNull(connectionId);

    // No body parameter, not even the token: the raw body is stored as it was received.
    var cancellationToken = HttpContext.RequestAborted;

    using var body = new MemoryStream();

    await Request.Body.CopyToAsync(body, cancellationToken).ConfigureAwait(false);

    var status = await sender.ReceiveAzureDevOpsWebhookAsync(
                   connectionId,
                   Request.Headers,
                   body.ToArray(),
                   cancellationToken
                 ).ConfigureAwait(false);

    return StatusCode(status);
  }

  #endregion
}
