// ------------------------------------------------------------------------------------------------
// <copyright file="RetryPolicyTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;

using CodeCoverage.Uploader.Http;
using CodeCoverage.Uploader.Output;

namespace CodeCoverage.Uploader.Tests.Http;

public sealed class RetryPolicyTests : IDisposable
{
  #region Fields

  private static readonly Uri Address = new("https://coverage.example.com/api/v1/uploads");

  private readonly FakeHttpHandler _handler = new();

  private readonly StringWriter _output = new();

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Send_ServerErrorThenAccepted_ShouldPrintTheStatusOfTheFailedAttempt()
  {
    _handler.Then(HttpStatusCode.BadGateway, "<html>Bad Gateway</html>").Then(HttpStatusCode.Accepted);

    using var response = await SendAsync();

    response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    _output.ToString().ShouldContain("warning: Attempt 1 of 5 failed (HTTP 502): trying again in 1 s.");
  }

  [Fact]
  public async Task Send_ServerErrorWithProblemDetails_ShouldPrintItsErrorCode()
  {
    _handler.Then(HttpStatusCode.BadGateway, """{"status":502,"errorCode":"GitHub.Unavailable"}""").Then(HttpStatusCode.Accepted);

    using var response = await SendAsync();

    _output.ToString().ShouldContain("warning: Attempt 1 of 5 failed (HTTP 502, GitHub.Unavailable): trying again in 1 s.");
  }

  [Fact]
  public async Task Send_NetworkError_ShouldPrintItsMessageWithoutVerboseMode()
  {
    _handler.ThenThrow(new HttpRequestException("Connection refused (coverage.example.com:443).")).Then(HttpStatusCode.Accepted);

    using var response = await SendAsync();

    _output.ToString().ShouldContain("warning: Attempt 1 of 5 failed (Connection refused (coverage.example.com:443)): trying again in 1 s.");
  }

  [Fact]
  public async Task Send_EveryAttemptFails_ShouldPrintTheStatusOfEachRetriedAttempt()
  {
    _handler.Then(HttpStatusCode.InternalServerError)
            .Then(HttpStatusCode.GatewayTimeout)
            .ThenThrow(new HttpRequestException("reset"))
            .Then(HttpStatusCode.TooManyRequests)
            .Then(HttpStatusCode.BadGateway);

    using var response = await SendAsync();

    response.StatusCode.ShouldBe(HttpStatusCode.BadGateway);

    var output = _output.ToString();

    output.ShouldContain("Attempt 1 of 5 failed (HTTP 500)");
    output.ShouldContain("Attempt 2 of 5 failed (HTTP 504)");
    output.ShouldContain("Attempt 3 of 5 failed (reset)");
    output.ShouldContain("Attempt 4 of 5 failed (HTTP 429)");
    output.ShouldNotContain("Attempt 5 of 5");
  }

  public void Dispose()
  {
    _handler.Dispose();
    _output.Dispose();
  }

  private async Task<HttpResponseMessage> SendAsync()
  {
    using var client = new HttpClient(_handler, disposeHandler: false);
    var retry = new RetryPolicy(static (_, _) => Task.CompletedTask, new ConsoleReporter(_output));

    return await retry.SendAsync(token => client.SendAsync(new HttpRequestMessage(HttpMethod.Post, Address), token), Ct);
  }

  #endregion
}
