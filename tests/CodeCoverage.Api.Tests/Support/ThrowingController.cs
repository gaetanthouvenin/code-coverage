// ------------------------------------------------------------------------------------------------
// <copyright file="ThrowingController.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using FluentValidation.Results;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using CodeCoverage.Application.Exceptions;

namespace CodeCoverage.Api.Tests.Support;

// Throws the exception named by the route, so that the tests observe how the host answers it.
// Registered as an ApplicationPart by CodeCoverageApiFactory.
[ApiController]
[Route(CodeCoverageApi.Route + "/tests/throw")]
public sealed class ThrowingController : ControllerBase
{
  #region Fields

  public const string Path = CodeCoverageApi.Prefix + "/tests/throw";

  public const string BugMessage = "Connection string Password=secret leaked";

  #endregion

  #region Methods

  [HttpGet("{kind}", Name = "Throw")]
  public IActionResult Throw(string kind)
  {
    throw kind switch
    {
      "validation" => new ValidationException(
        [
          new ValidationFailure("Name", "'Name' must not be empty."),
          new ValidationFailure("Name", "'Name' must be 100 characters or fewer."),
          new ValidationFailure("Branch", "'Branch' must not be empty."),
        ]),
      "bad-request" => new BadRequestException("Test.BadRequest", "The request is inconsistent."),
      "unauthorized" => new UnauthorizedException("Access.Unauthenticated", "Nobody is signed in."),
      "forbidden" => new ForbiddenException("Access.Forbidden", "The permission is missing."),
      "not-found" => NotFoundException.For("Repository", "octo/demo"),
      "conflict" => new ConflictException("Test.Conflict", "The entity already exists."),
      "unprocessable" => new UnprocessableEntityException("Test.Unprocessable", "The rule refuses it."),
      "bad-gateway" => new BadGatewayException("GitHub.Unavailable", "GitHub cannot answer."),
      "retry-later" => new RetryLaterException(TimeSpan.FromSeconds(30), "Internal to the worker."),
      "body-too-large" => new BadHttpRequestException("Too large.", StatusCodes.Status413PayloadTooLarge),
      var _ => new InvalidOperationException(BugMessage),
    };
  }

  #endregion
}
