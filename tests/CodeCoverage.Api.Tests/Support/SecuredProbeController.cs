// ------------------------------------------------------------------------------------------------
// <copyright file="SecuredProbeController.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Api.Tests.Support;

// A management endpoint of the tests: it requires the ApiToken scheme and tells who the scope runs for.
// Registered as an ApplicationPart by CodeCoverageApiFactory.
[ApiController]
[Authorize]
[Route(CodeCoverageApi.Route + "/tests/whoami")]
public sealed class SecuredProbeController(ICurrentUser currentUser) : ControllerBase
{
  #region Fields

  public const string Path = CodeCoverageApi.Prefix + "/tests/whoami";

  #endregion

  #region Methods

  [HttpGet(Name = "WhoAmI")]
  public IActionResult Get() =>
    Ok(new { currentUser.UserId, currentUser.Login, currentUser.TokenOrganizationId, currentUser.IsInstanceToken, currentUser.IsAdmin });

  #endregion
}
