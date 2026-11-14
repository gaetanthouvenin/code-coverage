// ------------------------------------------------------------------------------------------------
// <copyright file="CodecovApiClientTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Infrastructure.Codecov;

namespace CodeCoverage.Infrastructure.Tests.Codecov;

// The answers are fixtures written from the documentation of Codecov (tests/fixtures/codecov-api/README.md).
public sealed class CodecovApiClientTests : IDisposable
{
  #region Fields

  private const string Commits = "/api/v2/github/octo/repos/demo/commits/";

  private readonly WireMockServer _server = WireMockServer.Start();

  private readonly HttpClient _http;

  private readonly CodecovApiClient _client;

  #endregion

  #region Ctors

  public CodecovApiClientTests()
  {
    _http = new HttpClient { BaseAddress = new Uri(_server.Url! + "/") };
    _client = new CodecovApiClient(_http);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task ListCommits_ShouldKeepTheCompleteCommitsWithTheirCounters()
  {
    Given(1, 200, Fixture("commits-page-1.json"));

    var page = (await _client.ListCommitsAsync(Forge.GitHub, "octo", "", "demo", "secret-token", 1, Ct)).ShouldNotBeNull();

    page.HasNext.ShouldBeTrue();
    page.Commits.Count.ShouldBe(2);
    var first = page.Commits[0];
    first.Sha.ShouldBe(GitSha.Parse("1111111111111111111111111111111111111111"));
    first.Branch.ShouldBe("main");
    first.Timestamp.ShouldBe(new DateTimeOffset(2026, 10, 2, 10, 15, 0, TimeSpan.Zero));
    first.Files.ShouldBe(12);
    first.Totals.ShouldBe(new CoverageTotals(150, 40, 10, 8, 30));
    // Computed again by the formula of Codecov, not copied.
    first.Totals.Coverage.ShouldBe(75m);
    page.Commits[1].Totals.Coverage.ShouldBe(new CoverageTotals(1, 1, 1, 0, 0).Coverage);
    page.Commits[1].Timestamp.ShouldBe(new DateTimeOffset(2026, 10, 1, 7, 0, 0, TimeSpan.Zero));
  }

  [Fact]
  public async Task ListCommits_ShouldSendTheTokenAsABearerAndAskOneHundredCommits()
  {
    Given(2, 200, Fixture("commits-page-2.json"));

    var page = (await _client.ListCommitsAsync(Forge.GitHub, "octo", "", "demo", "secret-token", 2, Ct)).ShouldNotBeNull();

    page.HasNext.ShouldBeFalse();
    page.Commits.ShouldHaveSingleItem();
    var request = _server.LogEntries.Single().RequestMessage!;
    request.Headers!["Authorization"].ShouldBe(["Bearer secret-token"]);
    request.Query!["page_size"].ShouldBe(["100"]);
    request.Query!["page"].ShouldBe(["2"]);
  }

  [Theory]
  [InlineData(401)]
  [InlineData(403)]
  [InlineData(404)]
  public async Task ListCommits_Refused_ShouldReturnNull(int status)
  {
    Given(1, status, "{\"detail\":\"no\"}");

    (await _client.ListCommitsAsync(Forge.GitHub, "octo", "", "demo", "secret-token", 1, Ct)).ShouldBeNull();
  }

  // A failure of Codecov runs the job again; its message never holds the token.
  [Fact]
  public async Task ListCommits_Failure_ShouldThrowWithoutTheToken()
  {
    Given(1, 500, "oops");

    var exception = await Should.ThrowAsync<HttpRequestException>(() => _client.ListCommitsAsync(Forge.GitHub, "octo", "", "demo", "secret-token", 1, Ct));

    exception.Message.ShouldNotContain("secret-token");
  }

  // A repository of a GitLab subgroup: Codecov names its owner with the levels joined by colons.
  [Fact]
  public async Task ListCommits_Subgroup_ShouldNameTheOwnerWithItsLevels()
  {
    _server.Given(Request.Create().WithPath("/api/v2/gitlab/group:sub:deeper/repos/demo/commits/").UsingGet())
           .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody(Fixture("commits-page-2.json")));

    var page = (await _client.ListCommitsAsync(Forge.GitLab, "group", "sub/deeper", "demo", "secret-token", 1, Ct)).ShouldNotBeNull();

    page.Commits.ShouldHaveSingleItem();
    _server.LogEntries.Single().RequestMessage!.AbsolutePath.ShouldBe("/api/v2/gitlab/group:sub:deeper/repos/demo/commits/");
  }

  [Fact]
  public async Task ListCommits_InvalidArguments_ShouldThrow()
  {
    await Should.ThrowAsync<ArgumentException>(() => _client.ListCommitsAsync(Forge.AzureDevOps, "octo", "", "demo", "t", 1, Ct));
    await Should.ThrowAsync<ArgumentNullException>(() => _client.ListCommitsAsync(Forge.GitHub, null!, "", "demo", "t", 1, Ct));
    await Should.ThrowAsync<ArgumentNullException>(() => _client.ListCommitsAsync(Forge.GitHub, "octo", null!, "demo", "t", 1, Ct));
    await Should.ThrowAsync<ArgumentNullException>(() => _client.ListCommitsAsync(Forge.GitHub, "octo", "", null!, "t", 1, Ct));
    await Should.ThrowAsync<ArgumentNullException>(() => _client.ListCommitsAsync(Forge.GitHub, "octo", "", "demo", null!, 1, Ct));
    await Should.ThrowAsync<ArgumentOutOfRangeException>(() => _client.ListCommitsAsync(Forge.GitHub, "octo", "", "demo", "t", 0, Ct));
  }

  private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "codecov-api", name));

  private void Given(int page, int status, string body) =>
    _server.Given(Request.Create().WithPath(Commits).WithParam("page", page.ToString(System.Globalization.CultureInfo.InvariantCulture)).UsingGet())
           .RespondWith(Response.Create().WithStatusCode(status).WithHeader("Content-Type", "application/json").WithBody(body));

  #endregion

  #region Implementation of IDisposable

  public void Dispose()
  {
    _http.Dispose();
    _server.Dispose();
  }

  #endregion
}
