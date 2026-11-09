// ------------------------------------------------------------------------------------------------
// <copyright file="UploadsControllerBodyTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text;

using Mediator;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using CodeCoverage.Api.Configuration.RateLimiting;
using CodeCoverage.Api.Controllers;
using CodeCoverage.Application.Configuration;
using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Uploads;
using CodeCoverage.Domain.Uploads;

namespace CodeCoverage.Api.Tests.Uploads;

// On the controller directly: the test host of ASP.NET does not answer before the whole body is sent.
public sealed class UploadsControllerBodyTests
{
  #region Fields

  private const string Boundary = "----upload-test";

  private const string Sha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

  private readonly ISender _sender = A.Fake<ISender>();

  #endregion

  #region Properties

  private static string FieldsThenFileHeader =>
    $"--{Boundary}\r\nContent-Disposition: form-data; name=\"slug\"\r\n\r\nocto-org/app\r\n"
    + $"--{Boundary}\r\nContent-Disposition: form-data; name=\"sha\"\r\n\r\n{Sha}\r\n"
    + $"--{Boundary}\r\nContent-Disposition: form-data; name=\"file\"; filename=\"coverage.xml\"\r\n\r\n";

  #endregion

  #region Methods

  // The body never ends: reading it would never complete.
  [Fact]
  public async Task Post_Unauthenticated_ShouldRejectBeforeReadingBody()
  {
    await using var stream = new StallingRequestStream([]);
    using var limiter = Limiter();

    var exception = await Should.ThrowAsync<UnauthorizedException>(() => Controller(stream, limiter, withToken: false).CreateAsync().WaitAsync(TimeSpan.FromSeconds(10)));

    exception.ErrorCode.ShouldBe(UploadErrors.TokenInvalid);
    A.CallTo(_sender).MustNotHaveHappened();
  }

  // The text fields are read, and the file never arrives: the answer does not wait for it.
  [Fact]
  public async Task Post_InvalidToken_ShouldRejectBeforeReadingTheFiles()
  {
    A.CallTo(() => _sender.Send(A<IQuery<UploadAuthorization>>._, A<CancellationToken>._))
     .Throws(new UnauthorizedException(UploadErrors.TokenInvalid, "No."));
    await using var stream = new StallingRequestStream(Encoding.UTF8.GetBytes(FieldsThenFileHeader));
    using var limiter = Limiter();

    var exception = await Should.ThrowAsync<UnauthorizedException>(() => Controller(stream, limiter, withToken: true).CreateAsync().WaitAsync(TimeSpan.FromSeconds(10)));

    exception.ErrorCode.ShouldBe(UploadErrors.TokenInvalid);
  }

  private static UploadRateLimiter Limiter()
  {
    var options = A.Fake<IOptionsMonitor<UploadOptions>>();
    A.CallTo(() => options.CurrentValue).Returns(new UploadOptions());

    return new UploadRateLimiter(options);
  }

  private UploadsController Controller(Stream body, UploadRateLimiter limiter, bool withToken)
  {
    var context = new DefaultHttpContext();
    context.Request.Body = body;
    context.Request.ContentType = $"multipart/form-data; boundary={Boundary}";

    if (withToken)
    {
      context.Request.Headers.Authorization = "Bearer cvc_wrong";
    }

    return new UploadsController(_sender, limiter, A.Fake<IOptionsMonitor<UrlOptions>>()) { ControllerContext = new ControllerContext { HttpContext = context } };
  }

  #endregion
}
