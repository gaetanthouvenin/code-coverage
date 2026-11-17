// ------------------------------------------------------------------------------------------------
// <copyright file="AzureDevOpsRateLimitHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Net;

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Infrastructure.Forges;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.AzureDevOps;

public sealed class AzureDevOpsRateLimitHandlerTests : IDisposable
{
  #region Fields

  private const string Path = "/contoso/_apis/connectionData";

  private const string ThrottledBody =
    """{"$id":"1","innerException":null,"message":"TF400733: The request has been canceled: Request was blocked due to exceeding usage of resource 'Global' in namespace 'User'.","typeName":"Microsoft.TeamFoundation.Framework.Server.RequestBlockedException, Microsoft.TeamFoundation.Framework.Server","typeKey":"RequestBlockedException","errorCode":0,"eventId":3000}""";

  private readonly AzureDevOpsApiHarness _azure = new();

  #endregion

  #region Properties

  private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task SendAsync_TooManyRequests_ShouldThrowForgeRateLimited()
  {
    Given(
      Response.Create()
              .WithStatusCode(429)
              .WithHeader("Retry-After", "30")
              .WithHeader("X-RateLimit-Resource", "Global")
              .WithHeader("X-RateLimit-Remaining", "0")
              .WithBody(ThrottledBody)
    );

    var exception = await Should.ThrowAsync<ForgeRateLimitedException>(() => SendAsync());

    exception.Forge.ShouldBe(Forge.AzureDevOps);
    exception.RetryAfter.ShouldBe(AzureDevOpsApiHarness.Now.AddSeconds(30));
    _azure.Server.LogEntries.Count().ShouldBe(1, "a limit is waited for, never retried");
  }

  [Fact]
  public async Task SendAsync_TooManyRequestsWithoutRetryAfter_ShouldWaitUntilTheReset()
  {
    var reset = AzureDevOpsApiHarness.Now.AddMinutes(5);
    Given(
      Response.Create()
              .WithStatusCode(429)
              .WithHeader("X-RateLimit-Reset", reset.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture))
              .WithBody(ThrottledBody)
    );

    var exception = await Should.ThrowAsync<ForgeRateLimitedException>(() => SendAsync());

    exception.RetryAfter.ShouldBe(reset);
  }

  [Fact]
  public async Task SendAsync_AccessDenied_ShouldReturnTheResponseToTheCaller()
  {
    Given(Response.Create().WithStatusCode(401).WithBody("""{"message":"TF400813: The user is not authorized to access this resource."}"""));

    using var response = await SendAsync();

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    (await response.Content.ReadAsStringAsync(Cancellation)).ShouldContain("TF400813");
  }

  [Fact]
  public async Task SendAsync_5xxExhausted_ShouldThrowBadGateway()
  {
    Given(Response.Create().WithStatusCode(503));

    var exception = await Should.ThrowAsync<BadGatewayException>(() => SendAsync());

    exception.ErrorCode.ShouldBe(ForgeErrors.Unavailable);
    _azure.Server.LogEntries.Count().ShouldBe(4, "one attempt and three retries");
  }

  private void Given(IResponseBuilder response)
  {
    _azure.Server.Given(Request.Create().WithPath(Path).UsingGet()).RespondWith(response);
  }

  private async Task<HttpResponseMessage> SendAsync()
  {
    using var client = _azure.CreateClient();

    return await client.GetAsync(new Uri(Path.TrimStart('/'), UriKind.Relative), Cancellation);
  }

  #endregion

  #region Implementation of IDisposable

  public void Dispose()
  {
    _azure.Dispose();
  }

  #endregion
}
