// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubAppGatewayReadTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

using Microsoft.IdentityModel.JsonWebTokens;

using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Infrastructure.Diagnostics;
using CodeCoverage.Infrastructure.GitHub;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.GitHub;

public sealed class GitHubAppGatewayReadTests : IDisposable
{
  #region Fields

  private const long Installation = 42;

  private const long RepositoryId = 1296269;

  private const string InstallationToken = "ghs_installation";

  private const string Root = "/repositories/1296269";

  private static readonly RSA Key = RSA.Create(2048);

  private static readonly GitSha Head = GitSha.Parse("6dcb09b5b57875f334f61aebed695e2e4193db5e");

  private static readonly GitSha Base = GitSha.Parse("762941318ee16e59dabbacb1b4049eec22f0d303");

  private static readonly GitSha FirstParent = GitSha.Parse("7fd1a60b01f91b314f59955a4e4d4e80d8edf11d");

  private static readonly ForgeRepositoryRef Repository = new(Forge.GitHub, "42", "1296269");

  private readonly GitHubApiHarness _github = new();

  #endregion

  #region Ctors

  public GitHubAppGatewayReadTests()
  {
    _github.UsePrivateKey(Key);
    _github.Server
           .Given(Request.Create().WithPath($"/app/installations/{Installation}/access_tokens").UsingPost())
           .RespondWith(
             Response.Create()
                     .WithStatusCode(201)
                     .WithBody(
                       $$"""{"token":"{{InstallationToken}}","expires_at":"{{DateTimeOffset.UtcNow.AddHours(1).ToString("O", CultureInfo.InvariantCulture)}}"}"""
                     )
           );
  }

  #endregion

  #region Properties

  private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

  private IEnumerable<WireMock.IRequestMessage> ForgeRequests =>
    _github.Server.LogEntries.Select(static e => e.RequestMessage!).Where(static r => r.Method == "GET");

  #endregion

  #region Methods

  [Fact]
  public async Task ListCommits_ShouldMapParents()
  {
    GivenFixture($"{Root}/commits", "commits.json");

    var commits = await _github.Gateway.ListCommitsAsync(Repository, Head, 50, Cancellation);

    commits.Count.ShouldBe(2);
    commits[0].Sha.ShouldBe(Head);
    commits[0].ParentShas.ShouldBe([FirstParent, Base]);
    commits[0].Message.ShouldBe("Merge branch 'topic'");
    commits[0].AuthorLogin.ShouldBe("octocat");
    commits[0].CommittedAt.ShouldBe(new DateTimeOffset(2026, 10, 30, 8, 10, 0, TimeSpan.Zero));
    commits[1].ParentShas.ShouldBeEmpty();
    commits[1].AuthorLogin.ShouldBeNull();
  }

  [Fact]
  public async Task ListCommits_TrackedRepository_ShouldUseRepositoryIdRoute()
  {
    GivenFixture($"{Root}/commits", "commits.json");

    await _github.Gateway.ListCommitsAsync(Repository, Head, 50, Cancellation);

    var request = ForgeRequests.ShouldHaveSingleItem();
    request.Path.ShouldBe($"{Root}/commits");
    request.Query!["sha"].ShouldBe([Head.Value]);
    request.Query!["per_page"].ShouldBe(["50"]);
  }

  [Theory]
  [InlineData(404)]
  [InlineData(409)]
  [InlineData(422)]
  public async Task ListCommits_NoSuchCommit_ShouldReturnEmpty(int status)
  {
    GivenStatus($"{Root}/commits", status);

    var commits = await _github.Gateway.ListCommitsAsync(Repository, Head, 50, Cancellation);

    commits.ShouldBeEmpty();
  }

  [Theory]
  [InlineData(0)]
  [InlineData(-1)]
  public async Task ListCommits_InvalidPageSize_ShouldThrowArgumentOutOfRange(int perPage)
  {
    await Should.ThrowAsync<ArgumentOutOfRangeException>(
      () => _github.Gateway.ListCommitsAsync(Repository, Head, perPage, Cancellation)
    );
  }

  [Fact]
  public async Task GetRepository_ShouldMapTheRepository()
  {
    GivenFixture(Root, "repo.json");

    var repository = await _github.Gateway.GetRepositoryAsync(Repository, Cancellation);

    repository.ShouldBe(new ForgeRepositoryInfo("1296269", "octocat", string.Empty, "Hello-World", "main", true));
  }

  [Fact]
  public async Task GetRepository_NotFound_ShouldReturnNull()
  {
    GivenStatus(Root, 404);

    (await _github.Gateway.GetRepositoryAsync(Repository, Cancellation)).ShouldBeNull();
  }

  [Fact]
  public async Task GetCommit_ShouldMapTheCommit()
  {
    GivenBody($"{Root}/commits/{Head}", FirstCommit().ToJsonString());

    var commit = await _github.Gateway.GetCommitAsync(Repository, Head, Cancellation);

    commit.ShouldNotBeNull();
    commit.Sha.ShouldBe(Head);
    commit.ParentShas.ShouldBe([FirstParent, Base]);
  }

  [Fact]
  public async Task GetCommit_AuthorIsAnEmptyObject_ShouldHaveNoLogin()
  {
    var payload = FirstCommit();
    payload["author"] = new JsonObject();
    GivenBody($"{Root}/commits/{Head}", payload.ToJsonString());

    var commit = await _github.Gateway.GetCommitAsync(Repository, Head, Cancellation);

    commit.ShouldNotBeNull().AuthorLogin.ShouldBeNull();
  }

  [Fact]
  public async Task GetCommit_NoCommitterDate_ShouldUseTheDateOfTheAuthor()
  {
    var payload = FirstCommit();
    payload["commit"]!["committer"] = null;
    GivenBody($"{Root}/commits/{Head}", payload.ToJsonString());

    var commit = await _github.Gateway.GetCommitAsync(Repository, Head, Cancellation);

    commit.ShouldNotBeNull().CommittedAt.ShouldBe(new DateTimeOffset(2026, 10, 30, 8, 0, 0, TimeSpan.Zero));
  }

  [Theory]
  [InlineData(404)]
  [InlineData(409)]
  [InlineData(422)]
  public async Task GetCommit_NoSuchCommit_ShouldReturnNull(int status)
  {
    GivenStatus($"{Root}/commits/{Head}", status);

    (await _github.Gateway.GetCommitAsync(Repository, Head, Cancellation)).ShouldBeNull();
  }

  [Fact]
  public async Task Compare_ShouldMapTheComparison()
  {
    GivenFixture($"{Root}/compare/{Base}...{Head}", "compare.json");

    var compare = await _github.Gateway.CompareAsync(Repository, Base, Head, Cancellation);

    compare.ShouldNotBeNull();
    compare.MergeBaseSha.ShouldBe(FirstParent);
    compare.AheadBy.ShouldBe(2);
    compare.BehindBy.ShouldBe(1);
    compare.Truncated.ShouldBeFalse();
    compare.Files.Select(static f => f.Path)
           .ShouldBe(["src/Calculator.cs", "src/Renamed.cs", "assets/logo.png", "build.sh", "src/Removed.cs"]);
    compare.Files.Select(static f => f.Status)
           .ShouldBe(
             [
               ForgeChangedFileStatus.Modified,
               ForgeChangedFileStatus.Renamed,
               ForgeChangedFileStatus.Added,
               ForgeChangedFileStatus.Modified,
               ForgeChangedFileStatus.Removed,
             ]
           );
    compare.Files[0].Patch.ShouldNotBeNull().ShouldStartWith("@@ -1,3 +1,3 @@");
    compare.Files[1].PreviousPath.ShouldBe("src/Original.cs");
    compare.Files[2].Patch.ShouldBeNull();
  }

  [Fact]
  public async Task Compare_Truncated_ShouldFlag()
  {
    var payload = FixtureNode("compare.json");
    var file = payload["files"]![0]!;
    payload["files"] = new JsonArray([.. Enumerable.Range(0, 300).Select(_ => file.DeepClone())]);
    GivenBody($"{Root}/compare/{Base}...{Head}", payload.ToJsonString());

    var compare = await _github.Gateway.CompareAsync(Repository, Base, Head, Cancellation);

    compare.ShouldNotBeNull();
    compare.Truncated.ShouldBeTrue();
    compare.Files.Count.ShouldBe(300);
  }

  [Fact]
  public async Task Compare_WithoutFiles_ShouldFlag()
  {
    var payload = FixtureNode("compare.json");
    payload.AsObject().Remove("files");
    GivenBody($"{Root}/compare/{Base}...{Head}", payload.ToJsonString());

    var compare = await _github.Gateway.CompareAsync(Repository, Base, Head, Cancellation);

    compare.ShouldNotBeNull();
    compare.Truncated.ShouldBeTrue();
    compare.Files.ShouldBeEmpty();
  }

  [Fact]
  public async Task Compare_UnknownFileStatus_ShouldThrowBadGateway()
  {
    var payload = FixtureNode("compare.json");
    payload["files"]![0]!["status"] = "teleported";
    GivenBody($"{Root}/compare/{Base}...{Head}", payload.ToJsonString());

    var exception = await Should.ThrowAsync<BadGatewayException>(
      () => _github.Gateway.CompareAsync(Repository, Base, Head, Cancellation)
    );

    exception.ErrorCode.ShouldBe(GitHubErrors.Unavailable);
  }

  [Theory]
  [InlineData(404)]
  [InlineData(422)]
  public async Task Compare_CannotCompare_ShouldReturnNull(int status)
  {
    GivenStatus($"{Root}/compare/{Base}...{Head}", status);

    (await _github.Gateway.CompareAsync(Repository, Base, Head, Cancellation)).ShouldBeNull();
  }

  [Fact]
  public async Task GetPull_ShouldMapHeadAndBase()
  {
    GivenFixture($"{Root}/pulls/1347", "pull.json");

    var pull = await _github.Gateway.GetPullAsync(Repository, 1347, Cancellation);

    pull.ShouldBe(
      new ForgePullInfo(1347, ForgePullState.Open, "Amazing new feature", "octocat", Head, "new-topic", "main", Base)
    );
  }

  [Theory]
  [InlineData("open", false, ForgePullState.Open)]
  [InlineData("closed", false, ForgePullState.Closed)]
  [InlineData("closed", true, ForgePullState.Merged)]
  public async Task GetPull_StateAndMerged_ShouldMapTheState(string state, bool merged, ForgePullState expected)
  {
    var payload = FixtureNode("pull.json");
    payload["state"] = state;
    payload["merged"] = merged;
    GivenBody($"{Root}/pulls/1347", payload.ToJsonString());

    var pull = await _github.Gateway.GetPullAsync(Repository, 1347, Cancellation);

    pull.ShouldNotBeNull().State.ShouldBe(expected);
  }

  [Fact]
  public async Task GetPull_AuthorDeleted_ShouldHaveNoLogin()
  {
    var payload = FixtureNode("pull.json");
    payload["user"] = null;
    GivenBody($"{Root}/pulls/1347", payload.ToJsonString());

    var pull = await _github.Gateway.GetPullAsync(Repository, 1347, Cancellation);

    pull.ShouldNotBeNull().AuthorLogin.ShouldBeNull();
  }

  [Fact]
  public async Task GetPull_NotFound_ShouldReturnNull()
  {
    GivenStatus($"{Root}/pulls/1347", 404);

    (await _github.Gateway.GetPullAsync(Repository, 1347, Cancellation)).ShouldBeNull();
  }

  [Fact]
  public async Task GetTree_ShouldLeaveOutSubmodulesAndKeepTheShaRead()
  {
    GivenFixture($"{Root}/git/trees/{Head}", "tree.json");

    var tree = await _github.Gateway.GetTreeAsync(Repository, Head, recursive: true, Cancellation);

    tree.ShouldNotBeNull();
    tree.Sha.ShouldBe(Head);
    tree.Truncated.ShouldBeFalse();
    tree.Entries.Select(static e => (e.Path, e.IsDirectory))
        .ShouldBe([("codecov.yml", false), ("src", true), ("src/Calculator.cs", false)]);
    ForgeRequests.ShouldHaveSingleItem().Query!["recursive"].ShouldBe(["1"]);
  }

  [Fact]
  public async Task GetTree_NotRecursive_ShouldNotAskForRecursion()
  {
    GivenFixture($"{Root}/git/trees/{Head}", "tree.json");

    await _github.Gateway.GetTreeAsync(Repository, Head, recursive: false, Cancellation);

    ForgeRequests.ShouldHaveSingleItem().Query.ShouldBeEmpty();
  }

  [Fact]
  public async Task GetTree_Truncated_ShouldFlag()
  {
    var payload = FixtureNode("tree.json");
    payload["truncated"] = true;
    GivenBody($"{Root}/git/trees/{Head}", payload.ToJsonString());

    var tree = await _github.Gateway.GetTreeAsync(Repository, Head, recursive: true, Cancellation);

    tree.ShouldNotBeNull().Truncated.ShouldBeTrue();
  }

  [Theory]
  [InlineData(404)]
  [InlineData(409)]
  [InlineData(422)]
  public async Task GetTree_NoSuchTree_ShouldReturnNull(int status)
  {
    GivenStatus($"{Root}/git/trees/{Head}", status);

    (await _github.Gateway.GetTreeAsync(Repository, Head, recursive: false, Cancellation)).ShouldBeNull();
  }

  [Fact]
  public async Task GetContent_ShouldUseRawMediaType()
  {
    const string Raw = "coverage:\n  precision: 2\n";
    const string RawMediaType = "application/vnd.github.raw+json";

    _github.Server
           .Given(Request.Create().WithPath($"{Root}/contents/codecov.yml").UsingGet())
           .AtPriority(10)
           .RespondWith(Response.Create().WithBody(Fixture("contents.json")));
    _github.Server
           .Given(
             Request.Create()
                    .WithPath($"{Root}/contents/codecov.yml")
                    .WithHeader("Accept", new ExactMatcher(RawMediaType))
                    .UsingGet()
           )
           .AtPriority(1)
           .RespondWith(Response.Create().WithBody(Raw));

    var content = await _github.Gateway.GetContentAsync(Repository, "codecov.yml", Head, Cancellation);

    content.ShouldBe(Raw);

    var request = ForgeRequests.ShouldHaveSingleItem();
    request.Headers!["Accept"].ShouldBe([RawMediaType]);
    request.Query!["ref"].ShouldBe([Head.Value]);
  }

  [Fact]
  public async Task GetContent_NestedPath_ShouldEscapeEachSegment()
  {
    _github.Server.Given(Request.Create().UsingGet()).RespondWith(Response.Create().WithBody("text"));

    await _github.Gateway.GetContentAsync(Repository, "src/file#1.cs", Head, Cancellation);

    var request = ForgeRequests.ShouldHaveSingleItem();
    request.AbsoluteUrl.ShouldContain($"{Root}/contents/src/file%231.cs?ref=");
    request.Query!["ref"].ShouldBe([Head.Value]);
  }

  [Fact]
  public async Task GetContent_NotFound_ShouldReturnNull()
  {
    GivenStatus($"{Root}/contents/codecov.yml", 404);

    (await _github.Gateway.GetContentAsync(Repository, "codecov.yml", Head, Cancellation)).ShouldBeNull();
  }

  [Theory]
  [InlineData("")]
  [InlineData("../../installation")]
  [InlineData("src/../../secrets")]
  [InlineData("./codecov.yml")]
  [InlineData("/codecov.yml")]
  [InlineData("src//codecov.yml")]
  public async Task GetContent_PathLeavingTheRepository_ShouldReturnNullWithoutCalling(string path)
  {
    var content = await _github.Gateway.GetContentAsync(Repository, path, Head, Cancellation);

    content.ShouldBeNull();
    _github.Server.LogEntries.ShouldBeEmpty();
  }

  [Fact]
  public async Task GetRepositoryConnection_ShouldReadTheInstallationThenTheRepository()
  {
    GivenBody("/repos/octocat/Hello-World/installation", """{"id":42,"account":{"id":7,"login":"octocat"}}""");
    GivenFixture("/repos/octocat/Hello-World", "repo.json");

    var connection = await _github.Gateway.GetRepositoryConnectionAsync("octocat", "Hello-World", Cancellation);

    connection.ShouldBe(new ForgeConnectionInfo("42", "7", "1296269"));

    var requests = ForgeRequests.ToList();
    requests.Count.ShouldBe(2);

    var jwt = BearerOf(requests[0]);
    GitHubApiHarness.IsSignedBy(jwt, Key).ShouldBeTrue();
    new JsonWebToken(jwt).Issuer.ShouldBe(GitHubApiHarness.ClientId);
    BearerOf(requests[1]).ShouldBe(InstallationToken);
  }

  [Fact]
  public async Task GetRepositoryInstallation_NotInstalled_ShouldReturnNull()
  {
    GivenStatus("/repos/octocat/Hello-World/installation", 404);

    var connection = await _github.Gateway.GetRepositoryConnectionAsync("octocat", "Hello-World", Cancellation);

    connection.ShouldBeNull();
    ForgeRequests.ShouldHaveSingleItem();
  }

  [Fact]
  public async Task GetRepositoryConnection_RepositoryOutOfTheInstallation_ShouldReturnNull()
  {
    GivenBody("/repos/octocat/Hello-World/installation", """{"id":42,"account":{"id":7}}""");
    GivenStatus("/repos/octocat/Hello-World", 404);

    (await _github.Gateway.GetRepositoryConnectionAsync("octocat", "Hello-World", Cancellation)).ShouldBeNull();
  }

  [Theory]
  [InlineData("..", "app")]
  [InlineData(".", "x")]
  [InlineData("octocat", "..")]
  [InlineData("", "x")]
  [InlineData("octocat", "")]
  public async Task GetRepositoryConnection_NameLeavingTheRoute_ShouldReturnNullWithoutCalling(string owner, string name)
  {
    var connection = await _github.Gateway.GetRepositoryConnectionAsync(owner, name, Cancellation);

    connection.ShouldBeNull();
    _github.Server.LogEntries.ShouldBeEmpty();
  }

  [Fact]
  public async Task GetRepositoryConnection_NameWithSlash_ShouldStayOneSegment()
  {
    _github.Server.Given(Request.Create().UsingGet()).RespondWith(Response.Create().WithStatusCode(404));

    await _github.Gateway.GetRepositoryConnectionAsync("octocat", "a/b", Cancellation);

    ForgeRequests.ShouldHaveSingleItem().AbsoluteUrl.ShouldContain("/repos/octocat/a%2Fb/installation");
  }

  [Fact]
  public async Task EveryRead_ShouldCarryTheApiVersionAndTheInstallationToken()
  {
    _github.Server.Given(Request.Create().UsingGet()).RespondWith(Response.Create().WithStatusCode(404));

    await ReadEverythingAsync();

    var requests = ForgeRequests.ToList();
    requests.Count.ShouldBe(7);
    requests.ShouldAllBe(static r => r.Headers!["X-GitHub-Api-Version"].Single() == "2022-11-28");
    requests.ShouldAllBe(static r => r.Headers!["Authorization"].Single() == $"Bearer {InstallationToken}");
    requests.ShouldAllBe(static r => r.Path.StartsWith(Root, StringComparison.Ordinal));
  }

  [Fact]
  public async Task GetRepositoryConnection_ShouldCarryTheApiVersion()
  {
    _github.Server.Given(Request.Create().UsingGet()).RespondWith(Response.Create().WithStatusCode(404));

    await _github.Gateway.GetRepositoryConnectionAsync("octocat", "Hello-World", Cancellation);

    ForgeRequests.ShouldHaveSingleItem().Headers!["X-GitHub-Api-Version"].ShouldBe(["2022-11-28"]);
  }

  [Fact]
  public async Task EveryRead_ShouldPublishTheQuotaOfTheInstallation()
  {
    _github.Server
           .Given(Request.Create().UsingGet())
           .RespondWith(Response.Create().WithStatusCode(404).WithHeader("x-ratelimit-remaining", "4321"));

    await ReadEverythingAsync();
    _github.Quota.Collect();

    // The gauge keeps the last value of each installation: a request sent as the App would add a second series.
    var measurement = _github.Quota.Of(CodeCoverageMeter.GitHubRateLimitRemaining).ShouldHaveSingleItem();
    measurement.Value.ShouldBe(4321);
    measurement.Tags["installation"].ShouldBe("42");
  }

  [Theory]
  [InlineData(401)]
  [InlineData(403)]
  public async Task GetRepository_Refused_ShouldThrowBadGateway(int status)
  {
    GivenStatus(Root, status);

    var exception = await Should.ThrowAsync<BadGatewayException>(
      () => _github.Gateway.GetRepositoryAsync(Repository, Cancellation)
    );

    exception.ErrorCode.ShouldBe(GitHubErrors.Unavailable);
  }

  [Fact]
  public async Task GetContent_Refused_ShouldThrowBadGateway()
  {
    GivenStatus($"{Root}/contents/big.bin", 403);

    var exception = await Should.ThrowAsync<BadGatewayException>(
      () => _github.Gateway.GetContentAsync(Repository, "big.bin", Head, Cancellation)
    );

    exception.ErrorCode.ShouldBe(GitHubErrors.Unavailable);
  }

  [Theory]
  [InlineData("<html>")]
  [InlineData("null")]
  [InlineData("{\"id\":1}")]
  public async Task GetRepository_UnreadablePayload_ShouldThrowBadGateway(string body)
  {
    GivenBody(Root, body);

    var exception = await Should.ThrowAsync<BadGatewayException>(
      () => _github.Gateway.GetRepositoryAsync(Repository, Cancellation)
    );

    exception.ErrorCode.ShouldBe(GitHubErrors.Unavailable);
  }

  [Fact]
  public async Task GetCommit_MalformedSha_ShouldThrowBadGateway()
  {
    var payload = FirstCommit();
    payload["sha"] = "not-a-sha";
    GivenBody($"{Root}/commits/{Head}", payload.ToJsonString());

    var exception = await Should.ThrowAsync<BadGatewayException>(
      () => _github.Gateway.GetCommitAsync(Repository, Head, Cancellation)
    );

    exception.ErrorCode.ShouldBe(GitHubErrors.Unavailable);
  }

  [Fact]
  public async Task GetRepository_OtherForge_ShouldThrowArgument()
  {
    await Should.ThrowAsync<ArgumentException>(
      () => _github.Gateway.GetRepositoryAsync(new ForgeRepositoryRef(Forge.GitLab, "42", "1"), Cancellation)
    );

    _github.Server.LogEntries.ShouldBeEmpty();
  }

  [Theory]
  [InlineData("not-a-number", "1")]
  [InlineData("42", "1/../../app")]
  public async Task GetRepository_NonNumericAddress_ShouldThrowArgument(string connection, string externalId)
  {
    await Should.ThrowAsync<ArgumentException>(
      () => _github.Gateway.GetRepositoryAsync(new ForgeRepositoryRef(Forge.GitHub, connection, externalId), Cancellation)
    );

    _github.Server.LogEntries.ShouldBeEmpty();
  }

  private static string BearerOf(WireMock.IRequestMessage request)
  {
    var authorization = request.Headers!["Authorization"].Single();
    authorization.ShouldStartWith("Bearer ");

    return authorization["Bearer ".Length..];
  }

  private static string Fixture(string name) =>
    File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "github", name));

  private static JsonNode FixtureNode(string name) => JsonNode.Parse(Fixture(name))!;

  private static JsonNode FirstCommit() => FixtureNode("commits.json")[0]!.DeepClone();

  [Fact]
  public async Task ConvertManifest_ShouldGiveTheAppAndSendNoCredential()
  {
    _github.Server
           .Given(Request.Create().WithPath("/app-manifests/the-code/conversions").UsingPost())
           .RespondWith(
             Response.Create()
                     .WithStatusCode(201)
                     .WithBody("""{"id":12345,"slug":"code-coverage","client_id":"Iv1.abc","client_secret":"cs","webhook_secret":"ws","pem":"key"}""")
           );

    var app = (await _github.Gateway.ConvertManifestAsync("the-code", Cancellation)).ShouldNotBeNull();

    app.ShouldBe(new GitHubAppCredentials("12345", "code-coverage", "Iv1.abc", "cs", "ws", "key"));
    _github.Server.LogEntries.ShouldHaveSingleItem().RequestMessage!.Headers!.ShouldNotContainKey("Authorization");
  }

  [Theory]
  [InlineData(404)]
  [InlineData(422)]
  public async Task ConvertManifest_CodeRefused_ShouldReturnNull(int status)
  {
    _github.Server
           .Given(Request.Create().WithPath("/app-manifests/the-code/conversions").UsingPost())
           .RespondWith(Response.Create().WithStatusCode(status));

    (await _github.Gateway.ConvertManifestAsync("the-code", Cancellation)).ShouldBeNull();
  }

  [Fact]
  public async Task ListPullFiles_ShouldReadEveryPage()
  {
    GivenFilesPage(1, 50);
    GivenFilesPage(2, 3);

    var files = (await _github.Gateway.ListPullFilesAsync(Repository, 1347, Cancellation)).ShouldNotBeNull();

    files.Files.Count.ShouldBe(53);
    files.Truncated.ShouldBeFalse();
    files.Files[0].Path.ShouldBe("f0.cs");
    files.Files[0].Status.ShouldBe(ForgeChangedFileStatus.Modified);
    files.Files[0].Patch.ShouldBe("@@ -1 +1 @@\n-a\n+b");

    var requests = ForgeRequests.ToList();
    requests.Count.ShouldBe(2);
    requests.ShouldAllBe(static r => r.Path == $"{Root}/pulls/1347/files");
    requests[0].Query!["per_page"].ShouldBe(["50"]);
    requests[1].Query!["page"].ShouldBe(["2"]);
  }

  // A full last page: GitHub lists 3000 files at most and cannot say whether there is one more.
  [Fact]
  public async Task ListPullFiles_ThreeThousandFiles_ShouldStopAndBeTruncated()
  {
    for (var page = 1; page <= 61; page++)
    {
      GivenFilesPage(page, 50);
    }

    var files = (await _github.Gateway.ListPullFilesAsync(Repository, 1347, Cancellation)).ShouldNotBeNull();

    files.Files.Count.ShouldBe(3000);
    files.Truncated.ShouldBeTrue();
    ForgeRequests.Count().ShouldBe(60);
  }

  [Fact]
  public async Task ListPullFiles_LastPageNotFull_ShouldNotBeTruncated()
  {
    for (var page = 1; page <= 59; page++)
    {
      GivenFilesPage(page, 50);
    }

    GivenFilesPage(60, 49);

    var files = (await _github.Gateway.ListPullFilesAsync(Repository, 1347, Cancellation)).ShouldNotBeNull();

    files.Files.Count.ShouldBe(2999);
    files.Truncated.ShouldBeFalse();
  }

  [Fact]
  public async Task ListPullFiles_WithoutPatch_ShouldKeepItUnknown()
  {
    GivenBody($"{Root}/pulls/1347/files", """[{"filename":"binary.png","status":"added"}]""");

    var files = (await _github.Gateway.ListPullFilesAsync(Repository, 1347, Cancellation)).ShouldNotBeNull();

    files.Files.ShouldHaveSingleItem().Patch.ShouldBeNull();
  }

  [Fact]
  public async Task ListPullFiles_NoSuchPull_ShouldReturnNull()
  {
    GivenStatus($"{Root}/pulls/1347/files", 404);

    (await _github.Gateway.ListPullFilesAsync(Repository, 1347, Cancellation)).ShouldBeNull();
  }

  private void GivenFilesPage(int page, int count)
  {
    var files = new JsonArray();

    for (var index = 0; index < count; index++)
    {
      files.Add(new JsonObject { ["filename"] = $"f{index}.cs", ["status"] = "modified", ["patch"] = "@@ -1 +1 @@\n-a\n+b" });
    }

    _github.Server
           .Given(Request.Create().WithPath($"{Root}/pulls/1347/files").WithParam("page", page.ToString(CultureInfo.InvariantCulture)).UsingGet())
           .RespondWith(Response.Create().WithBody(files.ToJsonString()));
  }

  private async Task ReadEverythingAsync()
  {
    var gateway = _github.Gateway;

    await gateway.GetRepositoryAsync(Repository, Cancellation);
    await gateway.ListCommitsAsync(Repository, Head, 50, Cancellation);
    await gateway.GetCommitAsync(Repository, Head, Cancellation);
    await gateway.CompareAsync(Repository, Base, Head, Cancellation);
    await gateway.GetPullAsync(Repository, 1347, Cancellation);
    await gateway.GetTreeAsync(Repository, Head, recursive: true, Cancellation);
    await gateway.GetContentAsync(Repository, "codecov.yml", Head, Cancellation);
  }

  private void GivenBody(string path, string body) =>
    _github.Server.Given(Request.Create().WithPath(path).UsingGet()).RespondWith(Response.Create().WithBody(body));

  private void GivenFixture(string path, string fixture) => GivenBody(path, Fixture(fixture));

  private void GivenStatus(string path, int status) =>
    _github.Server.Given(Request.Create().WithPath(path).UsingGet()).RespondWith(Response.Create().WithStatusCode(status));

  #endregion

  #region Implementation of IDisposable

  public void Dispose() => _github.Dispose();

  #endregion
}
