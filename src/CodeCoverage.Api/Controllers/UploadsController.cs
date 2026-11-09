// ------------------------------------------------------------------------------------------------
// <copyright file="UploadsController.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using CodeCoverage.Api.Configuration.Errors;
using CodeCoverage.Api.Configuration.RateLimiting;
using CodeCoverage.Api.Contracts.Uploads;
using CodeCoverage.Api.Mapping;
using CodeCoverage.Api.Uploads;
using CodeCoverage.Application.Configuration;
using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Uploads;

namespace CodeCoverage.Api.Controllers;

/// <summary><c>/api/v1/uploads</c>: the reception of the coverage reports.</summary>
/// <param name="sender">The request sender.</param>
/// <param name="rateLimiter">The limiter of the uploads.</param>
/// <param name="urls">The public addresses of the hosts.</param>
[ApiController]
[Route(CodeCoverageApi.Route)]
[Tags(CodeCoverageApi.Tag)]
public sealed class UploadsController(ISender sender, UploadRateLimiter rateLimiter, IOptionsMonitor<UrlOptions> urls) : ControllerBase
{
  #region Fields

  private const string SlugField = "slug";

  private const string IdempotencyKeyHeader = "Idempotency-Key";

  #endregion

  #region Methods

  /// <summary>Upload coverage reports</summary>
  /// <remarks>
  ///   Reads a <c>multipart/form-data</c> request as it arrives, its text fields before its <c>file</c> and
  ///   <c>network</c> parts. The credential is checked before anything of the files is stored, and nothing is parsed
  ///   during the request: the answer is 202, or 200 for an upload already received. A repository that is not tracked
  ///   yet is added when the GitHub App is installed on its account.
  /// </remarks>
  /// <returns>The upload that was accepted.</returns>
  [HttpPost("uploads", Name = "CreateUpload")]
  [AllowAnonymous]
  [ProducesResponseType<UploadAcceptedResponse>(StatusCodes.Status200OK)]
  [ProducesResponseType<UploadAcceptedResponse>(StatusCodes.Status202Accepted)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
  public async Task<IActionResult> CreateAsync()
  {
    var cancellationToken = HttpContext.RequestAborted;
    var credential = UploadCredentialReader.Read(Request.Headers)
                     ?? throw new UnauthorizedException(UploadErrors.TokenInvalid, "The upload token is not valid.");
    var reader = new UploadMultipartReader(Request);
    var first = await reader.ReadNextFileAsync(cancellationToken).ConfigureAwait(false);

    if (!reader.Fields.TryGetValue(SlugField, out var slug))
    {
      throw new BadRequestException(UploadErrors.MetadataMustPrecedeFiles, "The slug field must come before the files.");
    }

    if (first is null)
    {
      throw new BadRequestException(UploadErrors.NoReport, "An upload holds at least one report.");
    }

    if (!rateLimiter.TryAcquire(credential, slug, out var retryAfter))
    {
      return TooManyRequests(retryAfter);
    }

    await sender.AuthorizeUploadAsync(credential, reader.Fields, cancellationToken).ConfigureAwait(false);

    var (response, isReplay) = await sender.ReceiveUploadResponseAsync(
                                 credential,
                                 reader.Fields,
                                 reader.ReadFilesAsync(first, cancellationToken),
                                 Request.Headers[IdempotencyKeyHeader].FirstOrDefault(),
                                 $"{Request.Scheme}://{Request.Host}",
                                 urls.CurrentValue,
                                 cancellationToken
                               ).ConfigureAwait(false);

    return StatusCode(isReplay ? StatusCodes.Status200OK : StatusCodes.Status202Accepted, response);
  }

  /// <summary>Read an upload</summary>
  /// <remarks>
  ///   Gives the state of an upload and its processing log, with the credential that sends the uploads of the repository.
  ///   An upload that the credential cannot read answers like one that does not exist.
  /// </remarks>
  /// <param name="forge">The forge of the repository.</param>
  /// <param name="owner">The owner of the repository.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="id">The public identifier of the upload.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The upload.</returns>
  [HttpGet("repos/{forge}/{owner}/{repo}/uploads/{id:guid}", Name = "GetUpload")]
  [AllowAnonymous]
  [ProducesResponseType<UploadResponse>(StatusCodes.Status200OK)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<UploadResponse>> GetAsync(string forge, string owner, string repo, Guid id, CancellationToken cancellationToken)
  {
    var credential = UploadCredentialReader.Read(Request.Headers)
                     ?? throw new UnauthorizedException(UploadErrors.TokenInvalid, "The upload token is not valid.");

    return await sender.GetUploadResponseAsync(credential, forge, owner, repo, id, cancellationToken).ConfigureAwait(false);
  }

  /// <summary>Refuses an upload over the limit of its credential and its repository.</summary>
  /// <param name="retryAfter">How long to wait.</param>
  /// <returns>The answer.</returns>
  private ObjectResult TooManyRequests(TimeSpan retryAfter)
  {
    Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);

    var problem = new ProblemDetails { Status = StatusCodes.Status429TooManyRequests, Title = "Too many requests", Detail = "Too many uploads for this repository." };

    problem.Extensions[ApiExceptionHandler.ErrorCodeExtension] = UploadErrors.RateLimited;

    return StatusCode(StatusCodes.Status429TooManyRequests, problem);
  }

  #endregion
}
