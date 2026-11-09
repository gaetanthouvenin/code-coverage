// ------------------------------------------------------------------------------------------------
// <copyright file="RequestBodyLimitMiddlewareTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;

using CodeCoverage.Api.Configuration.Uploads;
using CodeCoverage.Application.Configuration;

namespace CodeCoverage.Api.Tests.Uploads;

public sealed class RequestBodyLimitMiddlewareTests
{
  #region Methods

  // The routing also sends the address with a trailing slash to the upload endpoint.
  [Theory]
  [InlineData(CodeCoverageApi.Prefix + "/uploads")]
  [InlineData(CodeCoverageApi.Prefix + "/uploads/")]
  [InlineData("/API/V1/Uploads/")]
  public async Task Invoke_Upload_ShouldApplyTheSetting(string path)
  {
    var limit = await RunAsync(path, "POST");

    limit.ShouldBe(5L * 1024 * 1024);
  }

  [Theory]
  [InlineData("/health", "POST")]
  [InlineData(CodeCoverageApi.Prefix + "/uploads", "GET")]
  [InlineData(CodeCoverageApi.Prefix + "/uploads/other", "POST")]
  public async Task Invoke_OtherRequest_ShouldKeepTheDefault(string path, string method)
  {
    (await RunAsync(path, method)).ShouldBe(30_000_000);
  }

  private static async Task<long?> RunAsync(string path, string method)
  {
    var context = new DefaultHttpContext();
    var feature = A.Fake<IHttpMaxRequestBodySizeFeature>();
    A.CallTo(() => feature.IsReadOnly).Returns(false);
    feature.MaxRequestBodySize = 30_000_000;
    context.Features.Set(feature);
    context.Request.Path = path;
    context.Request.Method = method;

    var options = A.Fake<IOptionsMonitor<UploadOptions>>();
    A.CallTo(() => options.CurrentValue).Returns(new UploadOptions { MaxRequestMegabytes = 5 });

    await new RequestBodyLimitMiddleware(static _ => Task.CompletedTask, options).InvokeAsync(context);

    return feature.MaxRequestBodySize;
  }

  #endregion
}
