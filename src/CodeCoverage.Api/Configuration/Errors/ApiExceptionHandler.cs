// ------------------------------------------------------------------------------------------------
// <copyright file="ApiExceptionHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Diagnostics;

using CodeCoverage.Application.Exceptions;

using ApplicationException = CodeCoverage.Application.Exceptions.ApplicationException;

namespace CodeCoverage.Api.Configuration.Errors;

/// <summary>
///   Maps the Application's refusals to problem details: 400 for an invalid request, 401 without
///   an identity, 403 without the permission, 404 for an unknown entity, 409 for a conflict, 413 for a request too large, 422
///   for a refused business rule, 502 when an external service cannot answer, 500 otherwise.
/// </summary>
/// <remarks>
///   A refusal carries its <c>errorCode</c> extension, and a validation failure its <c>errors</c>
///   grouped by property. The message of a bug is never sent to the caller.
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
      BadGatewayException => (StatusCodes.Status502BadGateway, "External service unavailable"),
      ApplicationException => (StatusCodes.Status400BadRequest, "Bad request"),

      // Refused by the server while the body is read, such as 413 beyond its size limit.
      BadHttpRequestException refused => (refused.StatusCode, "Bad request"),
      var _ => (StatusCodes.Status500InternalServerError, "Internal server error"),
    };

    if (status >= StatusCodes.Status500InternalServerError)
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
        // The message of a bug is never sent to the caller.
        Detail = exception is ApplicationException ? exception.Message : null,
      },
    };

    if (exception is ApplicationException refusal)
    {
      context.ProblemDetails.Extensions[ErrorCodeExtension] = refusal.ErrorCode;
    }

    if (exception is ValidationException validation)
    {
      context.ProblemDetails.Extensions[ErrorsExtension] = validation.Errors;
    }

    return await problemDetails.TryWriteAsync(context).ConfigureAwait(false);
  }

  #endregion
}
