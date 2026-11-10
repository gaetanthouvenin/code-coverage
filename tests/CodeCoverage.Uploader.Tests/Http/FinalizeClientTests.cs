// ------------------------------------------------------------------------------------------------
// <copyright file="FinalizeClientTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;

using CodeCoverage.Uploader.Http;
using CodeCoverage.Uploader.Output;

namespace CodeCoverage.Uploader.Tests.Http;

public sealed class FinalizeClientTests
{
  #region Fields

  private const string Credential = "cvc_token";

  private static readonly Uri Server = new("https://coverage.example.com");

  private static readonly string Sha = new('a', 40);

  private readonly FakeHttpHandler _handler = new();

  private readonly StringWriter _output = new();

  private readonly List<TimeSpan> _waits = [];

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Complete_ShouldPostTheAnnouncementWithTheCredentialOfTheUpload()
  {
    _handler.Then(HttpStatusCode.OK, """{"total":3,"processed":2,"processing":1,"failed":0}""");

    (await Client().CompleteAsync(Server, "octo-org/app", Sha, Credential, Ct)).ShouldBeTrue();

    var (request, body) = _handler.Received.ShouldHaveSingleItem();
    request.Method.ShouldBe(HttpMethod.Post);
    request.RequestUri!.AbsoluteUri.ShouldBe($"https://coverage.example.com/api/v1/repos/github/octo-org/app/commits/{Sha}/uploads-complete");
    request.Headers.Authorization!.Scheme.ShouldBe("Bearer");
    request.Headers.Authorization.Parameter.ShouldBe(Credential);
    body.ShouldBeEmpty();
    _output.ToString().ShouldContain("The uploads of the commit are declared complete: 3 received, 1 still being processed.");
    _output.ToString().ShouldNotContain(Credential);
  }

  [Theory]
  [InlineData("""{"total":2,"processed":2,"processing":0,"failed":0}""", "declared complete: 2 received.")]
  [InlineData("""{"total":2,"processed":1,"processing":0,"failed":1}""", "declared complete: 2 received, 1 failed.")]
  [InlineData("{}", "The uploads of the commit are declared complete.")]
  [InlineData("not json", "The uploads of the commit are declared complete.")]
  [InlineData("""{"total":"many"}""", "The uploads of the commit are declared complete.")]
  public async Task Complete_ShouldSayWhatTheServerAnswered(string body, string expected)
  {
    _handler.Then(HttpStatusCode.OK, body);

    (await Client().CompleteAsync(Server, "octo-org/app", Sha, Credential, Ct)).ShouldBeTrue();

    _output.ToString().ShouldContain(expected);
  }

  [Theory]
  [InlineData(HttpStatusCode.Unauthorized, """{"errorCode":"Upload.TokenInvalid"}""", "401 (Upload.TokenInvalid)")]
  [InlineData(HttpStatusCode.Forbidden, "{}", "403.")]
  [InlineData(HttpStatusCode.NotFound, """{"errorCode":"Commit.NotFound"}""", "404 (Commit.NotFound)")]
  public async Task Complete_Refused_ShouldReportItAndReturnFalse(HttpStatusCode status, string body, string expected)
  {
    _handler.Then(status, body);

    (await Client().CompleteAsync(Server, "octo-org/app", Sha, Credential, Ct)).ShouldBeFalse();

    _handler.Received.Count.ShouldBe(1);
    _output.ToString().ShouldContain($"The server refused to declare the uploads complete: {expected}");
  }

  [Fact]
  public async Task Complete_ServerBusy_ShouldTryAgain()
  {
    _handler.Then(HttpStatusCode.ServiceUnavailable).Then(HttpStatusCode.TooManyRequests).Then(HttpStatusCode.OK, """{"total":1,"processed":1,"processing":0,"failed":0}""");

    (await Client().CompleteAsync(Server, "octo-org/app", Sha, Credential, Ct)).ShouldBeTrue();

    _handler.Received.Count.ShouldBe(3);
    _waits.Count.ShouldBe(2);
  }

  // A failure is reported, never thrown: the coverage tooling does not break a build.
  [Fact]
  public async Task Complete_ServerUnreachable_ShouldReturnFalseWithoutThrowing()
  {
    for (var attempt = 0; attempt < RetryPolicy.MaxAttempts; attempt++)
    {
      _handler.ThenThrow(new HttpRequestException("connection refused"));
    }

    (await Client().CompleteAsync(Server, "octo-org/app", Sha, Credential, Ct)).ShouldBeFalse();

    _handler.Received.Count.ShouldBe(RetryPolicy.MaxAttempts);
    _output.ToString().ShouldContain("The uploads could not be declared complete: connection refused");
  }

  [Theory]
  [InlineData("no-separator")]
  [InlineData("/app")]
  [InlineData("octo-org/")]
  [InlineData("")]
  public async Task Complete_SlugThatIsNotOwnerAndName_ShouldSendNothing(string slug)
  {
    (await Client().CompleteAsync(Server, slug, Sha, Credential, Ct)).ShouldBeFalse();

    _handler.Received.ShouldBeEmpty();
  }

  [Theory]
  [InlineData("https://coverage.example.com", "octo-org/app", "https://coverage.example.com/api/v1/repos/github/octo-org/app/commits/abc/uploads-complete")]
  [InlineData("https://coverage.example.com/", "octo-org/app", "https://coverage.example.com/api/v1/repos/github/octo-org/app/commits/abc/uploads-complete")]
  [InlineData("https://example.com/coverage", "octo-org/app", "https://example.com/coverage/api/v1/repos/github/octo-org/app/commits/abc/uploads-complete")]
  [InlineData("https://coverage.example.com", "octo-org/group/app", "https://coverage.example.com/api/v1/repos/github/octo-org/group%2Fapp/commits/abc/uploads-complete")]
  [InlineData("https://coverage.example.com", "octo org/a?b", "https://coverage.example.com/api/v1/repos/github/octo%20org/a%3Fb/commits/abc/uploads-complete")]
  public void BuildAddress_ShouldKeepThePathOfTheServerAndEscapeEachSegment(string server, string slug, string expected)
  {
    FinalizeClient.BuildAddress(new Uri(server), slug, "abc").ShouldNotBeNull().AbsoluteUri.ShouldBe(expected);
  }

  [Fact]
  public void BuildAddress_EmptySha_ShouldReturnNull()
  {
    FinalizeClient.BuildAddress(Server, "octo-org/app", string.Empty).ShouldBeNull();
  }

  private FinalizeClient Client()
  {
    var client = new HttpClient(_handler, disposeHandler: false);
    var reporter = new ConsoleReporter(_output) { IsVerbose = true };

    Task Delay(TimeSpan wait, CancellationToken token)
    {
      _waits.Add(wait);

      return Task.CompletedTask;
    }

    return new FinalizeClient(client, new RetryPolicy(Delay, reporter), reporter);
  }

  #endregion
}