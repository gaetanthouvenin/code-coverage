// ------------------------------------------------------------------------------------------------
// <copyright file="RateLimitHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Net;

using Microsoft.Extensions.Logging.Abstractions;

using Polly.CircuitBreaker;
using Polly.Timeout;

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Diagnostics;
using CodeCoverage.Infrastructure.GitHub;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.GitHub;

public sealed class RateLimitHandlerTests : IDisposable
{
  #region Fields

  private const string Path = "/repositories/1";

  private const string SecondaryLimitBody = """{"message":"You have exceeded a secondary rate limit."}""";

  private readonly GitHubApiHarness _github = new();

  #endregion

  #region Properties

  private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Send_SecondaryLimit_ShouldThrowRateLimitedWithReset()
  {
    Given(Response.Create().WithStatusCode(403).WithHeader("retry-after", "30").WithBody(SecondaryLimitBody));

    var exception = await Should.ThrowAsync<GitHubRateLimitedException>(() => SendAsync());

    exception.ResetAt.ShouldBe(GitHubApiHarness.Now.AddSeconds(30));
    _github.Server.LogEntries.Count().ShouldBe(1);
  }

  [Fact]
  public async Task Send_SecondaryLimitWithoutDelay_ShouldWaitOneMinute()
  {
    Given(Response.Create().WithStatusCode(403).WithHeader("x-ratelimit-remaining", "100").WithHeader("x-ratelimit-reset", "1893456000").WithBody(SecondaryLimitBody));

    var exception = await Should.ThrowAsync<GitHubRateLimitedException>(() => SendAsync());

    exception.ResetAt.ShouldBe(GitHubApiHarness.Now.AddMinutes(1));
  }

  [Fact]
  public async Task Send_PrimaryLimit_ShouldThrowRateLimitedUntilTheReset()
  {
    var reset = GitHubApiHarness.Now.AddMinutes(20);
    Given(
      Response.Create()
              .WithStatusCode(403)
              .WithHeader("x-ratelimit-remaining", "0")
              .WithHeader("x-ratelimit-reset", reset.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture))
              .WithBody("""{"message":"API rate limit exceeded"}""")
    );

    var exception = await Should.ThrowAsync<GitHubRateLimitedException>(() => SendAsync());

    exception.ResetAt.ShouldBe(reset);
  }

  [Fact]
  public async Task Send_TooManyRequests_ShouldThrowRateLimitedWithoutRetrying()
  {
    Given(Response.Create().WithStatusCode(429));

    var exception = await Should.ThrowAsync<GitHubRateLimitedException>(() => SendAsync());

    exception.ResetAt.ShouldBe(GitHubApiHarness.Now.AddMinutes(1));
    _github.Server.LogEntries.Count().ShouldBe(1);
  }

  [Fact]
  public async Task Send_PlainForbidden_ShouldReturnTheResponseToTheCaller()
  {
    Given(Response.Create().WithStatusCode(403).WithHeader("x-ratelimit-remaining", "100").WithBody("""{"message":"Resource not accessible by integration"}"""));

    using var response = await SendAsync();

    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    (await response.Content.ReadAsStringAsync(Cancellation)).ShouldContain("not accessible");
    _github.Server.LogEntries.Count().ShouldBe(1);
  }

  [Fact]
  public async Task Send_5xxExhausted_ShouldThrowBadGateway()
  {
    Given(Response.Create().WithStatusCode(503));

    var exception = await Should.ThrowAsync<BadGatewayException>(() => SendAsync());

    exception.ErrorCode.ShouldBe(GitHubErrors.Unavailable);
    _github.Server.LogEntries.Count().ShouldBe(4, "one attempt and three retries");
  }

  [Fact]
  public async Task Send_5xxThenSuccess_ShouldRetryAndReturnTheResponse()
  {
    const string Scenario = "transient";
    _github.Server.Given(Request.Create().WithPath(Path).UsingGet())
           .InScenario(Scenario)
           .WillSetStateTo("recovered")
           .RespondWith(Response.Create().WithStatusCode(502));
    _github.Server.Given(Request.Create().WithPath(Path).UsingGet())
           .InScenario(Scenario)
           .WhenStateIs("recovered")
           .RespondWith(Response.Create().WithStatusCode(200));

    using var response = await SendAsync();

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    _github.Server.LogEntries.Count().ShouldBe(2);
  }

  [Fact]
  public async Task Send_NetworkFailureExhausted_ShouldThrowBadGateway()
  {
    await ShouldBeBadGatewayAsync(new HttpRequestException("Connection refused."));
  }

  [Fact]
  public async Task Send_TimeoutRejected_ShouldThrowBadGateway()
  {
    await ShouldBeBadGatewayAsync(new TimeoutRejectedException());
  }

  [Fact]
  public async Task Send_CircuitOpen_ShouldThrowBadGateway()
  {
    await ShouldBeBadGatewayAsync(new BrokenCircuitException());
  }

  [Fact]
  public async Task Send_WithInstallation_ShouldPublishTheQuotaOfThatInstallation()
  {
    Given(Response.Create().WithStatusCode(200).WithHeader("x-ratelimit-remaining", "4321"));

    using var response = await SendAsync(42);
    _github.Quota.Collect();

    var measurement = _github.Quota.Of(CodeCoverageMeter.GitHubRateLimitRemaining).ShouldHaveSingleItem();
    measurement.Value.ShouldBe(4321);
    measurement.Tags["installation"].ShouldBe("42");
  }

  [Fact]
  public async Task Send_AsTheApp_ShouldPublishTheQuotaOfTheApp()
  {
    Given(Response.Create().WithStatusCode(200).WithHeader("x-ratelimit-remaining", "4999"));

    using var response = await SendAsync();
    _github.Quota.Collect();

    var measurement = _github.Quota.Of(CodeCoverageMeter.GitHubRateLimitRemaining).ShouldHaveSingleItem();
    measurement.Value.ShouldBe(4999);
    measurement.Tags["installation"].ShouldBe(RateLimitHandler.AppQuota);
  }

  // The quota of a user token is the one of that user: it says nothing about an installation nor about the App.
  [Fact]
  public async Task Send_WithAUserToken_ShouldPublishNoQuota()
  {
    Given(Response.Create().WithStatusCode(200).WithHeader("x-ratelimit-remaining", "4999"));

    using var client = _github.CreateClient();
    using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("repositories/1", UriKind.Relative));
    request.Options.Set(RateLimitHandler.UserToken, true);

    using var response = await client.SendAsync(request, Cancellation);
    _github.Quota.Collect();

    _github.Quota.Of(CodeCoverageMeter.GitHubRateLimitRemaining).ShouldBeEmpty();
  }

  [Fact]
  public async Task Send_WhenRateLimited_ShouldStillPublishTheQuota()
  {
    Given(Response.Create().WithStatusCode(403).WithHeader("x-ratelimit-remaining", "0").WithHeader("x-ratelimit-reset", "1893456000"));

    await Should.ThrowAsync<GitHubRateLimitedException>(() => SendAsync(42));
    _github.Quota.Collect();

    _github.Quota.Of(CodeCoverageMeter.GitHubRateLimitRemaining).ShouldHaveSingleItem().Value.ShouldBe(0);
  }

  private static async Task ShouldBeBadGatewayAsync(Exception failure)
  {
    using var meters = new TestMeterFactory();
    var inner = A.Fake<HttpMessageHandler>();
    A.CallTo(inner).WithReturnType<Task<HttpResponseMessage>>().Throws(failure);
    using var invoker = new HttpMessageInvoker(
      new RateLimitHandler(new CodeCoverageMeter(meters), A.Fake<IClock>(), NullLogger<RateLimitHandler>.Instance) { InnerHandler = inner }
    );
    using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("https://github.test/repositories/1"));

    var exception = await Should.ThrowAsync<BadGatewayException>(() => invoker.SendAsync(request, Cancellation));

    exception.ErrorCode.ShouldBe(GitHubErrors.Unavailable);
    exception.InnerException.ShouldBeSameAs(failure);
  }

  private void Given(IResponseBuilder response) =>
    _github.Server.Given(Request.Create().WithPath(Path).UsingGet()).RespondWith(response);

  private async Task<HttpResponseMessage> SendAsync(long? installation = null)
  {
    using var client = _github.CreateClient();
    using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("repositories/1", UriKind.Relative));

    if (installation is { } id)
    {
      request.Options.Set(RateLimitHandler.InstallationId, id);
    }

    return await client.SendAsync(request, Cancellation);
  }

  #endregion

  #region Implementation of IDisposable

  public void Dispose() => _github.Dispose();

  #endregion
}
