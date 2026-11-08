// ------------------------------------------------------------------------------------------------
// <copyright file="PingController.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using Microsoft.AspNetCore.Mvc;

using CodeCoverage.Api.Contracts.Diagnostics;
using CodeCoverage.Application.Features.Diagnostics.Queries.Ping;

namespace CodeCoverage.Api.Controllers;

/// <summary><c>/api/v1/diagnostics</c>: the diagnostics of the service.</summary>
/// <param name="sender">The request sender.</param>
[ApiController]
[Route(CodeCoverageApi.Route + "/diagnostics")]
[Tags(CodeCoverageApi.Tag)]
public sealed class PingController(ISender sender) : ControllerBase
{
  #region Methods

  /// <summary>Ping</summary>
  /// <remarks>
  ///   The version of the service. It checks that the host answers and that it is wired to the
  ///   Application.
  /// </remarks>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The version of the service.</returns>
  [HttpGet("ping", Name = "Ping")]
  [ProducesResponseType<PingResponse>(StatusCodes.Status200OK)]
  public async Task<PingResponse> PingAsync(CancellationToken cancellationToken)
  {
    var version = await sender.Send(new PingQuery(), cancellationToken).ConfigureAwait(false);

    return new PingResponse(version);
  }

  #endregion
}
