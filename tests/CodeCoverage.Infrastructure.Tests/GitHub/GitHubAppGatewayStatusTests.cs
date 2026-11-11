// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubAppGatewayStatusTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Notifications;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Infrastructure.GitHub;
using CodeCoverage.Infrastructure.Tests.Support;

using ApplicationException = CodeCoverage.Application.Exceptions.ApplicationException;

namespace CodeCoverage.Infrastructure.Tests.GitHub;

public sealed class GitHubAppGatewayStatusTests : IDisposable
{
  #region Fields

  private const long Installation = 42;

  private const string InstallationToken = "ghs_installation";

  private static readonly RSA Key = RSA.Create(2048);

  private static readonly GitSha Head = GitSha.Parse("6dcb09b5b57875f334f61aebed695e2e4193db5e");

  private static readonly ForgeRepositoryRef Repository = new(Forge.GitHub, "42", "1296269");

  private static readonly string Path = $"/repositories/1296269/statuses/{Head.Value}";

  private readonly GitHubApiHarness _github = new();

  #endregion

  #region Ctors

  public GitHubAppGatewayStatusTests()
  {
    _github.UsePrivateKey(Key);
    _github.Server
           .Given(Request.Create().WithPath($"/app/installations/{Installation}/access_tokens").UsingPost())
           .RespondWith(
             Response.Create()
                     .WithStatusCode(201)
                     .WithBody("{\"token\":\"" + InstallationToken + "\",\"expires_at\":\"" + DateTimeOffset.UtcNow.AddHours(1).ToString("O", CultureInfo.InvariantCulture) + "\"}")
           );
  }

  #endregion

  #region Properties

  private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task CreateStatus_ShouldPostTheStatusOnTheShaByTheIdentifierOfTheRepository()
  {
    GivenStatus(201);

    await _github.Gateway.CreateStatusAsync(
      Repository,
      Head,
      new CommitStatus("codecov/project/backend", StatusState.Failure, "79.00% (-1.00%) compared to bbbbbbb"),
      "https://coverage.example.com/github/octo/app/pull/12",
      Cancellation
    );

    var request = _github.Server.LogEntries.Select(static e => e.RequestMessage!).Single(r => r.Path == Path);
    request.Method.ShouldBe("POST");
    request.Headers!["Authorization"].ShouldHaveSingleItem().ShouldBe($"Bearer {InstallationToken}");

    using var body = JsonDocument.Parse(request.Body!);
    var root = body.RootElement;
    root.GetProperty("state").GetString().ShouldBe("failure");
    root.GetProperty("context").GetString().ShouldBe("codecov/project/backend");
    root.GetProperty("description").GetString().ShouldBe("79.00% (-1.00%) compared to bbbbbbb");
    root.GetProperty("target_url").GetString().ShouldBe("https://coverage.example.com/github/octo/app/pull/12");
  }

  [Theory]
  [InlineData(StatusState.Success, "success")]
  [InlineData(StatusState.Failure, "failure")]
  [InlineData(StatusState.Error, "error")]
  public async Task CreateStatus_ShouldSendTheStateAsGitHubNamesIt(StatusState state, string expected)
  {
    GivenStatus(201);

    await _github.Gateway.CreateStatusAsync(Repository, Head, new CommitStatus("coverage/patch", state, "ok"), null, Cancellation);

    using var body = JsonDocument.Parse(_github.Server.LogEntries.Select(static e => e.RequestMessage!).Single(r => r.Path == Path).Body!);
    body.RootElement.GetProperty("state").GetString().ShouldBe(expected);
    // No address: nothing is invented in its place.
    body.RootElement.GetProperty("target_url").ValueKind.ShouldBe(JsonValueKind.Null);
  }

  // A status GitHub did not take fails the job: it is never counted as published. A failure of GitHub is tried again.
  [Fact]
  public async Task CreateStatus_GitHubFails_ShouldThrowBadGateway()
  {
    GivenStatus(500);

    await Should.ThrowAsync<BadGatewayException>(
      () => _github.Gateway.CreateStatusAsync(Repository, Head, new CommitStatus("coverage/project", StatusState.Success, "ok"), null, Cancellation)
    );
  }

  // T1. A refusal no retry can heal is permanent: a repository or a commit GitHub does not know (404), a status it
  // refuses (403 without a rate limit: a missing permission, an archived repository), a SHA it does not know or a
  // commit that holds 1000 statuses already (422).
  [Theory]
  [InlineData(403, typeof(UnprocessableEntityException))]
  [InlineData(404, typeof(NotFoundException))]
  [InlineData(422, typeof(UnprocessableEntityException))]
  public async Task CreateStatus_RefusedForGood_ShouldThrowAPermanentRefusal(int status, Type expected)
  {
    GivenStatus(status);

    var exception = await Should.ThrowAsync<ApplicationException>(
      () => _github.Gateway.CreateStatusAsync(Repository, Head, new CommitStatus("coverage/project", StatusState.Success, "ok"), null, Cancellation)
    );

    exception.ShouldBeOfType(expected);
    exception.ErrorCode.ShouldBe("GitHub.StatusRefused");
  }

  // T1. A rate limit stays a rate limit, whatever its status: the job waits for the reset and tries again.
  [Fact]
  public async Task CreateStatus_RateLimited_ShouldStayARateLimit()
  {
    _github.Server.Given(Request.Create().WithPath(Path).UsingPost())
           .RespondWith(Response.Create().WithStatusCode(403).WithHeader("x-ratelimit-remaining", "0").WithBody("{}"));

    await Should.ThrowAsync<GitHubRateLimitedException>(
      () => _github.Gateway.CreateStatusAsync(Repository, Head, new CommitStatus("coverage/project", StatusState.Success, "ok"), null, Cancellation)
    );
  }

  // A status sent twice replaces itself: unlike a comment, it is retried.
  [Fact]
  public async Task CreateStatus_TransientFailure_ShouldBeRetried()
  {
    const string Scenario = "transient";
    _github.Server.Given(Request.Create().WithPath(Path).UsingPost())
           .InScenario(Scenario)
           .WillSetStateTo("recovered")
           .RespondWith(Response.Create().WithStatusCode(502));
    _github.Server.Given(Request.Create().WithPath(Path).UsingPost())
           .InScenario(Scenario)
           .WhenStateIs("recovered")
           .RespondWith(Response.Create().WithStatusCode(201));

    await _github.Gateway.CreateStatusAsync(Repository, Head, new CommitStatus("coverage/project", StatusState.Success, "ok"), null, Cancellation);

    _github.Server.LogEntries.Count(static e => e.RequestMessage!.Path == Path).ShouldBe(2);
  }

  [Fact]
  public async Task CreateStatus_NullArgument_ShouldThrow()
  {
    var status = new CommitStatus("coverage/project", StatusState.Success, "ok");

    await Should.ThrowAsync<ArgumentNullException>(() => _github.Gateway.CreateStatusAsync(null!, Head, status, null, Cancellation));
    await Should.ThrowAsync<ArgumentNullException>(() => _github.Gateway.CreateStatusAsync(Repository, null!, status, null, Cancellation));
    await Should.ThrowAsync<ArgumentNullException>(() => _github.Gateway.CreateStatusAsync(Repository, Head, null!, null, Cancellation));
  }

  [Fact]
  public async Task CreateStatus_RepositoryOfAnotherForge_ShouldThrow()
  {
    var other = new ForgeRepositoryRef(Forge.GitLab, "42", "1296269");

    await Should.ThrowAsync<ArgumentException>(
      () => _github.Gateway.CreateStatusAsync(other, Head, new CommitStatus("coverage/project", StatusState.Success, "ok"), null, Cancellation)
    );
  }

  private void GivenStatus(int status) =>
    _github.Server.Given(Request.Create().WithPath(Path).UsingPost()).RespondWith(Response.Create().WithStatusCode(status).WithBody("{}"));

  #endregion

  #region Implementation of IDisposable

  public void Dispose() => _github.Dispose();

  #endregion
}