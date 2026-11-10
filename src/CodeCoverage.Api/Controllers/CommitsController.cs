// ------------------------------------------------------------------------------------------------
// <copyright file="CommitsController.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using CodeCoverage.Api.Contracts.Uploads;
using CodeCoverage.Api.Mapping;
using CodeCoverage.Api.Uploads;
using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Uploads;

namespace CodeCoverage.Api.Controllers;

/// <summary>The commits of a repository.</summary>
/// <param name="sender">The request sender.</param>
[ApiController]
[Route(CodeCoverageApi.Route)]
[Tags(CodeCoverageApi.Tag)]
public sealed class CommitsController(ISender sender) : ControllerBase
{
  #region Methods

  /// <summary>Announce the end of the uploads of a commit</summary>
  /// <remarks>
  ///   Asks for the notification of the commit as soon as every upload it received is processed, whatever
  ///   <c>codecov.notify.after_n_builds</c> and <c>manual_trigger</c> say: the last job of a pipeline calls
  ///   it, as <c>upload-complete</c> of Codecov. It takes the credential that sends the uploads of the
  ///   repository, and answers at once: the statuses are published in the background.
  /// </remarks>
  /// <param name="forge">The forge of the repository.</param>
  /// <param name="owner">The owner of the repository.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="sha">The full SHA of the commit.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>Where the uploads of the commit stand.</returns>
  [HttpPost("repos/{forge}/{owner}/{repo}/commits/{sha}/uploads-complete", Name = "CompleteUploads")]
  [AllowAnonymous]
  [ProducesResponseType<UploadsCompleteResponse>(StatusCodes.Status200OK)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<UploadsCompleteResponse>> CompleteUploadsAsync(string forge, string owner, string repo, string sha, CancellationToken cancellationToken)
  {
    var credential = UploadCredentialReader.Read(Request.Headers)
                     ?? throw new UnauthorizedException(UploadErrors.TokenInvalid, "The upload token is not valid.");

    return await sender.CompleteUploadsResponseAsync(credential, forge, owner, repo, sha, cancellationToken).ConfigureAwait(false);
  }

  #endregion
}