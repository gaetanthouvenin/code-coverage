// ------------------------------------------------------------------------------------------------
// <copyright file="OperationsController.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using CodeCoverage.Api.Contracts.Operations;
using CodeCoverage.Api.Mapping;

namespace CodeCoverage.Api.Controllers;

/// <summary>The operations of the instance: jobs, webhook deliveries and status, for an administrator of the instance.</summary>
/// <param name="sender">The request sender.</param>
/// <remarks>
///   Metadata only: never a payload, a key, the text of an error or data of an organization. A token of an organization
///   is refused (403): these routes need a token of the instance, or the Web console.
/// </remarks>
[ApiController]
[Authorize]
[Route(CodeCoverageApi.Route + "/ops")]
[Tags(CodeCoverageApi.Tag)]
public sealed class OperationsController(ISender sender) : ControllerBase
{
  #region Methods

  /// <summary>List the background jobs</summary>
  /// <remarks>One page of the jobs, the most recent first, 25 unless <c>pageSize</c> asks for 1 to 100; <c>state</c> keeps one state.</remarks>
  /// <param name="state">The state of the jobs: Queued, Running, Succeeded, Failed or Dead.</param>
  /// <param name="page">The number of the page, from 1.</param>
  /// <param name="pageSize">The number of jobs of a page, 1 to 100.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The page.</returns>
  [HttpGet("jobs", Name = "ListJobs")]
  [ProducesResponseType<JobPageResponse>(StatusCodes.Status200OK)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
  public async Task<ActionResult<JobPageResponse>> ListJobsAsync(
    [FromQuery] string? state,
    [FromQuery] int? page,
    [FromQuery] int? pageSize,
    CancellationToken cancellationToken) =>
    await sender.GetJobPageResponseAsync(state, page, pageSize, cancellationToken).ConfigureAwait(false);

  /// <summary>Retry a job</summary>
  /// <remarks>
  ///   Puts a failed or dead job back in the queue, its attempts at zero, to run at once in the organization of its lane.
  ///   Any other job, or one whose twin already waits, answers 409.
  /// </remarks>
  /// <param name="id">The identifier of the job.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>Accepted: the job runs in the background.</returns>
  [HttpPost("jobs/{id:long}/retry", Name = "RetryJob")]
  [ProducesResponseType(StatusCodes.Status202Accepted)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
  public async Task<IActionResult> RetryJobAsync(long id, CancellationToken cancellationToken)
  {
    await sender.RetryJobAsync(id, cancellationToken).ConfigureAwait(false);

    return Accepted();
  }

  /// <summary>List the webhook deliveries</summary>
  /// <remarks>The latest deliveries, 50 unless <c>count</c> asks for 1 to 200, with a flag when one failed.</remarks>
  /// <param name="count">How many deliveries.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The deliveries.</returns>
  [HttpGet("webhooks", Name = "ListWebhookDeliveries")]
  [ProducesResponseType<IReadOnlyList<WebhookDeliveryResponse>>(StatusCodes.Status200OK)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
  public async Task<ActionResult<IReadOnlyList<WebhookDeliveryResponse>>> ListWebhooksAsync([FromQuery] int? count, CancellationToken cancellationToken) =>
    Ok(await sender.GetWebhookDeliveryResponsesAsync(count, cancellationToken).ConfigureAwait(false));

  /// <summary>Read the status of the system</summary>
  /// <remarks>
  ///   The jobs by state, the age of the oldest job due, when a worker loop last turned and the GitHub quota left. The
  ///   heartbeat and the quota are those of the answering process: one Api replica runs the worker.
  /// </remarks>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The status.</returns>
  [HttpGet("status", Name = "GetSystemStatus")]
  [ProducesResponseType<SystemStatusResponse>(StatusCodes.Status200OK)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
  public async Task<ActionResult<SystemStatusResponse>> GetStatusAsync(CancellationToken cancellationToken) =>
    await sender.GetSystemStatusResponseAsync(cancellationToken).ConfigureAwait(false);

  #endregion
}
