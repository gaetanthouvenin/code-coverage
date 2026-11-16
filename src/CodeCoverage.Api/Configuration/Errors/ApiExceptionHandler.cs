// ------------------------------------------------------------------------------------------------
// <copyright file="ApiExceptionHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Microsoft.AspNetCore.Diagnostics;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Infrastructure.GitHub;

using ApplicationException = CodeCoverage.Application.Exceptions.ApplicationException;

namespace CodeCoverage.Api.Configuration.Errors;

/// <summary>
///   Maps the Application's refusals to problem details: 400 for an invalid request, 401 without
///   an identity, 403 without the permission, 404 for an unknown entity, 409 for a conflict, 413 for a request too large, 422
///   for a refused business rule, 502 when an external service cannot answer, 500 otherwise.
/// </summary>
/// <remarks>
///   A refusal carries its <c>errorCode</c> extension, and a validation failure its <c>errors</c>
///   grouped by property. The message of a bug is never sent to the caller. A rate limit of GitHub met
///   during the request is a 502 that names <see cref="GitHubErrors.RateLimited" /> and the moment of the
///   reset, logged as a warning; it sends no <c>Retry-After</c>, since the reset may be an hour away.
/// </remarks>
/// <param name="problemDetails">The problem details writer.</param>
/// <param name="logger">The logger.</param>
internal sealed class ApiExceptionHandler(
  IProblemDetailsService problemDetails,
  ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
  #region Fields

  /// <summary>The problem details extension that carries the error code of a refusal.</summary>
  public const string ErrorCodeExtension = "errorCode";

  /// <summary>The problem details extension that carries the validation errors.</summary>
  public const string ErrorsExtension = "errors";

  #endregion

  #region Methods

  /// <summary>Gives what the caller may know of an exception: the code and the message of a refusal.</summary>
  /// <param name="exception">The exception.</param>
  /// <returns>
  ///   The error code and the detail, both <see langword="null" /> for a bug, whose message is never sent to the caller.
  /// </returns>
  private static (string? ErrorCode, string? Detail) Describe(Exception exception) => exception switch
  {
    ApplicationException refusal => (refusal.ErrorCode, refusal.Message),
    GitHubRateLimitedException limited => (
      GitHubErrors.RateLimited,
      $"GitHub limits the requests of this instance until {limited.RetryAfter.ToString("O", CultureInfo.InvariantCulture)}. Try again after this moment."),
    var _ => (null, null),
  };

  #endregion

  #region Implementation of IExceptionHandler

  /// <inheritdoc />
  public async ValueTask<bool> TryHandleAsync(
    HttpContext httpContext,
    Exception exception,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(httpContext);
    ArgumentNullException.ThrowIfNull(exception);

    var (status, title) = exception switch
    {
      ValidationException => (StatusCodes.Status400BadRequest, "Invalid request"),
      UnauthorizedException => (StatusCodes.Status401Unauthorized, "Authentication required"),
      ForbiddenException => (StatusCodes.Status403Forbidden, "Permission denied"),
      NotFoundException => (StatusCodes.Status404NotFound, "Not found"),
      ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
      PayloadTooLargeException => (StatusCodes.Status413PayloadTooLarge, "Payload too large"),
      UnprocessableEntityException => (StatusCodes.Status422UnprocessableEntity, "Unprocessable entity"),
      BadGatewayException or GitHubRateLimitedException => (StatusCodes.Status502BadGateway, "External service unavailable"),
      ApplicationException => (StatusCodes.Status400BadRequest, "Bad request"),

      // Refused by the server while the body is read, such as 413 beyond its size limit.
      BadHttpRequestException refused => (refused.StatusCode, "Bad request"),
      var _ => (StatusCodes.Status500InternalServerError, "Internal server error"),
    };

    var (errorCode, detail) = Describe(exception);

    // A rate limit of GitHub is a wait, not a bug: the job worker treats it the same way.
    if (exception is GitHubRateLimitedException limited)
    {
      logger.LogWarning("GitHub rate limited {Path} until {ResetAt}", httpContext.Request.Path, limited.RetryAfter);
    }
    else if (status >= StatusCodes.Status500InternalServerError)
    {
      logger.LogError(exception, "Unhandled exception on {Path}", httpContext.Request.Path);
    }
    else
    {
      logger.LogWarning("Refused {Path}: {Title}", httpContext.Request.Path, title);
    }

    httpContext.Response.StatusCode = status;

    var context = new ProblemDetailsContext
    {
      HttpContext = httpContext,
      Exception = exception,
      ProblemDetails =
      {
        Status = status,
        Title = title,
        Detail = detail,
      },
    };

    if (errorCode is not null)
    {
      context.ProblemDetails.Extensions[ErrorCodeExtension] = errorCode;
    }

    if (exception is ValidationException validation)
    {
      context.ProblemDetails.Extensions[ErrorsExtension] = validation.Errors;
    }

    return await problemDetails.TryWriteAsync(context).ConfigureAwait(false);
  }

  #endregion
}
