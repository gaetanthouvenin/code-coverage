// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubAppGatewayCheckRunTests.cs" company="Gaëtan Thouvenin">
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
using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Notifications;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.GitHub;

// M5.05: check runs in place of the statuses, behind Notifications:ChecksEnabled.
public sealed class GitHubAppGatewayCheckRunTests : IDisposable
{
  #region Fields

  private const long Installation = 42;

  private const string CheckRuns = "/repositories/1296269/check-runs";

  private const string CheckRun = "/repositories/1296269/check-runs/501";

  private static readonly RSA Key = RSA.Create(2048);

  private static readonly ForgeRepositoryRef Repository = new(Forge.GitHub, "42", "1296269");

  private static readonly GitSha Sha = GitSha.Parse("a94a8fe5ccb19ba61c4c0873d391e987982fbbd3");

  private readonly GitHubApiHarness _github = new();

  #endregion

  #region Ctors

  public GitHubAppGatewayCheckRunTests()
  {
    _github.UsePrivateKey(Key);
    _github.Server
           .Given(Request.Create().WithPath($"/app/installations/{Installation}/access_tokens").UsingPost())
           .RespondWith(
             Response.Create()
                     .WithStatusCode(201)
                     .WithBody("{\"token\":\"ghs_installation\",\"expires_at\":\"" + DateTimeOffset.UtcNow.AddHours(1).ToString("O", CultureInfo.InvariantCulture) + "\"}")
           );
  }

  #endregion

  #region Properties

  private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task CreateCheckRun_ShouldPostACompletedRunWithItsAnnotations()
  {
    _github.Server.Given(Request.Create().WithPath(CheckRuns).UsingPost()).RespondWith(Response.Create().WithStatusCode(201).WithBody("""{"id":501}"""));
    var run = Run(StatusState.Failure, [new CheckRunAnnotation("src/a.cs", 3, 5, "Added lines #L3 - L5 were not covered by tests")]);

    (await _github.Gateway.CreateCheckRunAsync(Repository, run, Cancellation)).ShouldBe(501);

    using var body = JsonDocument.Parse(Single(CheckRuns).Body!);
    var root = body.RootElement;
    root.GetProperty("name").GetString().ShouldBe("coverage/patch");
    root.GetProperty("head_sha").GetString().ShouldBe(Sha.Value);
    root.GetProperty("status").GetString().ShouldBe("completed");
    root.GetProperty("conclusion").GetString().ShouldBe("failure");
    root.GetProperty("details_url").GetString().ShouldBe("https://coverage.example.com/github/octo/app/pull/1");
    var output = root.GetProperty("output");
    output.GetProperty("title").GetString().ShouldBe("50.00% of diff hit");
    output.GetProperty("summary").GetString().ShouldBe("50.00% of diff hit");
    var annotation = output.GetProperty("annotations").EnumerateArray().ShouldHaveSingleItem();
    annotation.GetProperty("path").GetString().ShouldBe("src/a.cs");
    annotation.GetProperty("start_line").GetInt32().ShouldBe(3);
    annotation.GetProperty("end_line").GetInt32().ShouldBe(5);
    annotation.GetProperty("annotation_level").GetString().ShouldBe("warning");
    annotation.GetProperty("message").GetString().ShouldBe("Added lines #L3 - L5 were not covered by tests");
  }

  [Theory]
  [InlineData(StatusState.Success, "success")]
  [InlineData(StatusState.Error, "failure")]
  public async Task CreateCheckRun_ShouldMapTheStateToAConclusion(StatusState state, string conclusion)
  {
    _github.Server.Given(Request.Create().WithPath(CheckRuns).UsingPost()).RespondWith(Response.Create().WithStatusCode(201).WithBody("""{"id":501}"""));

    await _github.Gateway.CreateCheckRunAsync(Repository, Run(state, []) with { DetailsUrl = null }, Cancellation);

    using var body = JsonDocument.Parse(Single(CheckRuns).Body!);
    body.RootElement.GetProperty("conclusion").GetString().ShouldBe(conclusion);
    body.RootElement.TryGetProperty("details_url", out _).ShouldBeFalse();
  }

  // The Checks permission is not granted (403 "Resource not accessible by integration"): told as a value, the caller
  // publishes the status. Never a permanent failure of the job.
  [Theory]
  [InlineData(403)]
  [InlineData(404)]
  public async Task CreateCheckRun_Refused_ShouldReturnNull(int status)
  {
    _github.Server.Given(Request.Create().WithPath(CheckRuns).UsingPost())
           .RespondWith(Response.Create().WithStatusCode(status).WithBody("""{"message":"Resource not accessible by integration"}"""));

    (await _github.Gateway.CreateCheckRunAsync(Repository, Run(StatusState.Success, []), Cancellation)).ShouldBeNull();
  }

  // A request GitHub cannot take: a status would not fix it, so it is a failure, not a fallback.
  [Fact]
  public async Task CreateCheckRun_Unprocessable_ShouldThrowBadGateway()
  {
    _github.Server.Given(Request.Create().WithPath(CheckRuns).UsingPost()).RespondWith(Response.Create().WithStatusCode(422).WithBody("""{"message":"Validation Failed"}"""));

    await Should.ThrowAsync<BadGatewayException>(() => _github.Gateway.CreateCheckRunAsync(Repository, Run(StatusState.Success, []), Cancellation));
  }

  // GitHub appends the annotations of a PATCH: sent again after a lost answer, a batch would show twice.
  [Theory]
  [InlineData(500)]
  [InlineData(502)]
  [InlineData(503)]
  public async Task UpdateCheckRun_TransientFailure_ShouldBeSentOnce(int status)
  {
    _github.Server.Given(Request.Create().WithPath(CheckRun).UsingPatch()).RespondWith(Response.Create().WithStatusCode(status));

    await Should.ThrowAsync<BadGatewayException>(() => _github.Gateway.UpdateCheckRunAsync(Repository, 501, Run(StatusState.Success, [new CheckRunAnnotation("src/b.cs", 9, 9, "m")]), Cancellation));

    _github.Server.LogEntries.Count(static e => e.RequestMessage!.Path == CheckRun).ShouldBe(1);
  }

  [Fact]
  public async Task CreateCheckRun_Failure_ShouldThrowBadGateway()
  {
    _github.Server.Given(Request.Create().WithPath(CheckRuns).UsingPost()).RespondWith(Response.Create().WithStatusCode(500));

    await Should.ThrowAsync<BadGatewayException>(() => _github.Gateway.CreateCheckRunAsync(Repository, Run(StatusState.Success, []), Cancellation));
  }

  // A PATCH adds its annotations to the run: neither its name nor its commit is sent again.
  [Fact]
  public async Task UpdateCheckRun_ShouldPatchTheRunWithItsAnnotations()
  {
    _github.Server.Given(Request.Create().WithPath(CheckRun).UsingPatch()).RespondWith(Response.Create().WithStatusCode(200).WithBody("""{"id":501}"""));

    (await _github.Gateway.UpdateCheckRunAsync(Repository, 501, Run(StatusState.Failure, [new CheckRunAnnotation("src/b.cs", 9, 9, "m")]), Cancellation)).ShouldBeTrue();

    var request = Single(CheckRun);
    request.Method.ShouldBe("PATCH");
    using var body = JsonDocument.Parse(request.Body!);
    body.RootElement.TryGetProperty("name", out _).ShouldBeFalse();
    body.RootElement.TryGetProperty("head_sha", out _).ShouldBeFalse();
    body.RootElement.GetProperty("output").GetProperty("annotations").GetArrayLength().ShouldBe(1);
  }

  [Theory]
  [InlineData(403)]
  [InlineData(404)]
  public async Task UpdateCheckRun_Refused_ShouldReturnFalse(int status)
  {
    _github.Server.Given(Request.Create().WithPath(CheckRun).UsingPatch()).RespondWith(Response.Create().WithStatusCode(status));

    (await _github.Gateway.UpdateCheckRunAsync(Repository, 501, Run(StatusState.Success, []), Cancellation)).ShouldBeFalse();
  }

  [Fact]
  public async Task CheckRuns_InvalidArguments_ShouldThrow()
  {
    var other = new ForgeRepositoryRef(Forge.GitLab, "42", "1296269");

    await Should.ThrowAsync<ArgumentNullException>(() => _github.Gateway.CreateCheckRunAsync(null!, Run(StatusState.Success, []), Cancellation));
    await Should.ThrowAsync<ArgumentNullException>(() => _github.Gateway.CreateCheckRunAsync(Repository, null!, Cancellation));
    await Should.ThrowAsync<ArgumentException>(() => _github.Gateway.CreateCheckRunAsync(other, Run(StatusState.Success, []), Cancellation));
    await Should.ThrowAsync<ArgumentOutOfRangeException>(() => _github.Gateway.UpdateCheckRunAsync(Repository, 0, Run(StatusState.Success, []), Cancellation));
    await Should.ThrowAsync<ArgumentNullException>(() => _github.Gateway.UpdateCheckRunAsync(Repository, 501, null!, Cancellation));
  }

  private static GitHubCheckRun Run(StatusState state, IReadOnlyList<CheckRunAnnotation> annotations) =>
    new("coverage/patch", Sha, state, "50.00% of diff hit", "https://coverage.example.com/github/octo/app/pull/1", annotations);

  private WireMock.IRequestMessage Single(string path) =>
    _github.Server.LogEntries.Select(static e => e.RequestMessage!).Single(r => r.Path == path);

  #endregion

  #region Implementation of IDisposable

  public void Dispose() => _github.Dispose();

  #endregion
}
