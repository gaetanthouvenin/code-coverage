// ------------------------------------------------------------------------------------------------
// <copyright file="AzureDevOpsGatewayReadTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Text.Json.Nodes;

using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Notifications;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Infrastructure.Forges;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.AzureDevOps;

public sealed class AzureDevOpsGatewayReadTests : IDisposable
{
  #region Fields

  private const string RepositoryId = "1701bc47-1145-4739-a805-64b02e40dbe7";

  private const string Root = "/contoso/_apis/git/repositories/" + RepositoryId;

  private const string AccessToken = "eyJ.application";

  private static readonly string TokenPath = string.Create(CultureInfo.InvariantCulture, $"/{AzureDevOpsApiHarness.TenantId:D}/oauth2/v2.0/token");

  private static readonly GitSha Head = GitSha.Parse("be67f8871a4d2c75f13a51c1d3c30ac0d74d4ef4");

  private static readonly ForgeRepositoryRef Repository = new(Forge.AzureDevOps, AzureDevOpsApiHarness.ConnectionId, RepositoryId);

  private readonly AzureDevOpsApiHarness _azure = new();

  #endregion

  #region Ctors

  public AzureDevOpsGatewayReadTests()
  {
    _azure.Server
          .Given(Request.Create().WithPath(TokenPath).UsingPost())
          .RespondWith(Response.Create().WithStatusCode(200).WithBody($$"""{"access_token":"{{AccessToken}}","expires_in":3600,"token_type":"Bearer"}"""));
  }

  #endregion

  #region Properties

  private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

  private IEnumerable<WireMock.IRequestMessage> ForgeRequests =>
    _azure.Server.LogEntries.Select(static e => e.RequestMessage!).Where(static r => r.Method == "GET");

  #endregion

  #region Methods

  [Fact]
  public async Task GetPullAsync_Active_ShouldUseTheSourceCommit()
  {
    GivenFixture($"{Root}/pullrequests/22", "pull.json");

    var pull = await _azure.Gateway.GetPullAsync(Repository, 22, Cancellation);

    pull.ShouldNotBeNull();
    pull.Number.ShouldBe(22);
    pull.State.ShouldBe(ForgePullState.Open);
    pull.Title.ShouldBe("Add the coverage of the api");
    pull.AuthorLogin.ShouldBe("norman@contoso.example");
    pull.HeadSha.ShouldBe(GitSha.Parse("b60280bc6e62e2f880f1b63c1e24987664d3bda3"));
    pull.HeadBranch.ShouldBe("feature/coverage");
    pull.BaseBranch.ShouldBe("main");
    pull.BaseSha.ShouldBe(GitSha.Parse("f47bbc106853afe3c1b07a81754bce5f4b8dbf62"));
    pull.MergeCommitSha.ShouldBe(GitSha.Parse("39f52d24533cc712fc845ed9fd1b6c06b3942588"));
  }

  [Fact]
  public async Task GetPullAsync_Completed_ShouldMapMerged()
  {
    GivenPull("completed");

    var pull = await _azure.Gateway.GetPullAsync(Repository, 22, Cancellation);

    pull.ShouldNotBeNull().State.ShouldBe(ForgePullState.Merged);
  }

  [Fact]
  public async Task GetPullAsync_Abandoned_ShouldMapClosed()
  {
    GivenPull("abandoned");

    var pull = await _azure.Gateway.GetPullAsync(Repository, 22, Cancellation);

    pull.ShouldNotBeNull().State.ShouldBe(ForgePullState.Closed);
  }

  [Fact]
  public async Task GetPullAsync_Missing_ShouldReturnNull()
  {
    GivenStatus($"{Root}/pullrequests/23", 404);

    var pull = await _azure.Gateway.GetPullAsync(Repository, 23, Cancellation);

    pull.ShouldBeNull();
  }

  [Fact]
  public async Task GetPullAsync_UnknownStatus_ShouldThrowUnavailable()
  {
    GivenPull("notSet");

    var exception = await Should.ThrowAsync<BadGatewayException>(() => _azure.Gateway.GetPullAsync(Repository, 22, Cancellation));

    exception.ErrorCode.ShouldBe(ForgeErrors.Unavailable);
  }

  [Fact]
  public async Task GetCommitAsync_Missing_ShouldReturnNull()
  {
    GivenStatus($"{Root}/commits/{Head.Value}", 404);

    var commit = await _azure.Gateway.GetCommitAsync(Repository, Head, Cancellation);

    commit.ShouldBeNull();
  }

  [Fact]
  public async Task GetCommitAsync_ShouldMapTheParentsAndTheWholeMessage()
  {
    GivenFixture($"{Root}/commits/{Head.Value}", "commit.json");

    var commit = await _azure.Gateway.GetCommitAsync(Repository, Head, Cancellation);

    commit.ShouldNotBeNull();
    commit.Sha.ShouldBe(Head);
    commit.ParentShas.ShouldBe([GitSha.Parse("20510720ffe1443d6ed3cb98ca340bc732ba04e5"), GitSha.Parse("9991b4f66def4c0a9ad8f9f27043ece7eddcf1c7")]);
    commit.Message.ShouldBe("Merged PR 2: Add the coverage of the api\n\nA body longer than the listing would keep.");
    commit.AuthorLogin.ShouldBeNull();
    commit.CommittedAt.ShouldBe(new DateTimeOffset(2026, 11, 15, 17, 6, 53, TimeSpan.Zero));
  }

  [Fact]
  public async Task ListCommitsAsync_Paged_ShouldStopAtPerPage()
  {
    GivenCommitChain();

    var commits = await _azure.Gateway.ListCommitsAsync(Repository, Head, 2, Cancellation);

    commits.Select(static c => c.Sha.Value).ShouldBe(["be67f8871a4d2c75f13a51c1d3c30ac0d74d4ef4", "20510720ffe1443d6ed3cb98ca340bc732ba04e5"]);
    ForgeRequests.Count().ShouldBe(2);
  }

  [Fact]
  public async Task ListCommitsAsync_RootReached_ShouldStopThere()
  {
    GivenCommitChain();

    var commits = await _azure.Gateway.ListCommitsAsync(Repository, Head, 50, Cancellation);

    commits.Count.ShouldBe(4);
    commits[^1].ParentShas.ShouldBeEmpty();
    ForgeRequests.Count().ShouldBe(4);
  }

  [Fact]
  public async Task ListCommitsAsync_MissingCommit_ShouldBeEmpty()
  {
    GivenStatus($"{Root}/commits/{Head.Value}", 404);

    var commits = await _azure.Gateway.ListCommitsAsync(Repository, Head, 50, Cancellation);

    commits.ShouldBeEmpty();
  }

  [Fact]
  public async Task GetRepositoryAsync_ShouldPutTheProjectInTheNamespace()
  {
    GivenFixture(Root, "repo.json");

    var repository = await _azure.Gateway.GetRepositoryAsync(Repository, Cancellation);

    repository.ShouldNotBeNull();
    repository.ExternalId.ShouldBe(RepositoryId);
    repository.Owner.ShouldBe("contoso");
    repository.Namespace.ShouldBe("Projet A");
    repository.Name.ShouldBe("api");
    repository.DefaultBranch.ShouldBe("main");
    repository.IsPrivate.ShouldBeTrue();
    repository.OwnerExternalId.ShouldBe(_azure.Connection.ExternalAccountId);
  }

  [Fact]
  public async Task GetRepositoryAsync_ShouldSendTheTokenOfTheApplicationAndTheApiVersion()
  {
    GivenFixture(Root, "repo.json");

    await _azure.Gateway.GetRepositoryAsync(Repository, Cancellation);

    var request = ForgeRequests.ShouldHaveSingleItem();
    request.Headers!["Authorization"].ShouldHaveSingleItem().ShouldBe($"Bearer {AccessToken}");
    request.Query!["api-version"].ShouldBe(["7.1"]);
  }

  [Fact]
  public async Task GetRepositoryAsync_Forbidden_ShouldThrowUnavailable()
  {
    GivenStatus(Root, 403);

    var exception = await Should.ThrowAsync<BadGatewayException>(() => _azure.Gateway.GetRepositoryAsync(Repository, Cancellation));

    exception.ErrorCode.ShouldBe(ForgeErrors.Unavailable);
  }

  [Fact]
  public async Task GetRepositoryAsync_GitHubReference_ShouldThrowArgument()
  {
    await Should.ThrowAsync<ArgumentException>(
      () => _azure.Gateway.GetRepositoryAsync(new ForgeRepositoryRef(Forge.GitHub, "42", "1296269"), Cancellation)
    );

    ForgeRequests.ShouldBeEmpty();
  }

  [Fact]
  public async Task GetRepositoryConnectionAsync_ProjectWithSpace_ShouldFindTheRepositoryByName()
  {
    _azure.Server
          .Given(Request.Create().WithPath(new WildcardMatcher("/contoso/Projet*A/_apis/git/repositories/api")).UsingGet())
          .RespondWith(Response.Create().WithStatusCode(200).WithBody(Fixture("repo.json")));

    var connection = await _azure.Gateway.GetRepositoryConnectionAsync("contoso:Projet A", "api", Cancellation);

    connection.ShouldBe(
      new ForgeConnectionInfo(AzureDevOpsApiHarness.ConnectionId, _azure.Connection.ExternalAccountId, RepositoryId, Forge.AzureDevOps)
    );
  }

  [Fact]
  public async Task GetRepositoryConnectionAsync_Missing_ShouldReturnNull()
  {
    _azure.Server
          .Given(Request.Create().WithPath(new WildcardMatcher("/contoso/*/_apis/git/repositories/*")).UsingGet())
          .RespondWith(Response.Create().WithStatusCode(404));

    var connection = await _azure.Gateway.GetRepositoryConnectionAsync("contoso:Projet A", "unknown", Cancellation);

    connection.ShouldBeNull();
  }

  [Theory]
  [InlineData("fabrikam:Projet A")]
  [InlineData("contoso")]
  [InlineData("contoso:")]
  [InlineData(":Projet A")]
  [InlineData("contoso:..")]
  public async Task GetRepositoryConnectionAsync_OwnerNotServed_ShouldReturnNullWithoutAsking(string owner)
  {
    var connection = await _azure.Gateway.GetRepositoryConnectionAsync(owner, "api", Cancellation);

    connection.ShouldBeNull();
    ForgeRequests.ShouldBeEmpty();
  }

  [Fact]
  public async Task GetBranchHeadAsync_PrefixedNames_ShouldPickTheBranch()
  {
    GivenFixture($"{Root}/refs", "refs.json");

    var head = await _azure.Gateway.GetBranchHeadAsync(Repository, "main", Cancellation);

    head.ShouldBe(GitSha.Parse("f47bbc106853afe3c1b07a81754bce5f4b8dbf62"));
    ForgeRequests.ShouldHaveSingleItem().Query!["filter"].ShouldBe(["heads/main"]);
  }

  [Fact]
  public async Task GetBranchHeadAsync_Unknown_ShouldReturnNull()
  {
    GivenFixture($"{Root}/refs", "refs.json");

    var head = await _azure.Gateway.GetBranchHeadAsync(Repository, "mai", Cancellation);

    head.ShouldBeNull();
  }

  [Fact]
  public async Task GetBranchHeadAsync_MissingRepository_ShouldReturnNull()
  {
    GivenStatus($"{Root}/refs", 404);

    var head = await _azure.Gateway.GetBranchHeadAsync(Repository, "main", Cancellation);

    head.ShouldBeNull();
  }

  [Fact]
  public async Task CompareAsync_ShouldNotBeBuiltYet()
  {
    await Should.ThrowAsync<NotSupportedException>(() => _azure.Gateway.CompareAsync(Repository, Head, Head, Cancellation));
  }

  [Fact]
  public async Task ListPullFilesAsync_ShouldNotBeBuiltYet()
  {
    await Should.ThrowAsync<NotSupportedException>(() => _azure.Gateway.ListPullFilesAsync(Repository, 22, Cancellation));
  }

  [Fact]
  public async Task CreateStatusAsync_ShouldNotBeBuiltYet()
  {
    var status = new CommitStatus("coverage/patch", StatusState.Success, "ok");

    await Should.ThrowAsync<NotSupportedException>(() => _azure.Gateway.CreateStatusAsync(Repository, Head, status, null, Cancellation));
  }

  [Fact]
  public async Task ListIssueCommentsAsync_ShouldNotBeBuiltYet()
  {
    await Should.ThrowAsync<NotSupportedException>(() => _azure.Gateway.ListIssueCommentsAsync(Repository, 22, Cancellation));
  }

  [Fact]
  public async Task CreateIssueCommentAsync_ShouldNotBeBuiltYet()
  {
    await Should.ThrowAsync<NotSupportedException>(() => _azure.Gateway.CreateIssueCommentAsync(Repository, 22, "body", Cancellation));
  }

  [Fact]
  public async Task UpdateIssueCommentAsync_ShouldNotBeBuiltYet()
  {
    await Should.ThrowAsync<NotSupportedException>(() => _azure.Gateway.UpdateIssueCommentAsync(Repository, 1, "body", Cancellation));
  }

  [Fact]
  public async Task DeleteIssueCommentAsync_ShouldNotBeBuiltYet()
  {
    await Should.ThrowAsync<NotSupportedException>(() => _azure.Gateway.DeleteIssueCommentAsync(Repository, 1, Cancellation));
  }

  private static string Fixture(string name) =>
    File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "azure-devops", name));

  private void GivenCommitChain()
  {
    foreach (var commit in JsonNode.Parse(Fixture("commits.json"))!.AsArray())
    {
      GivenBody($"{Root}/commits/{commit!["commitId"]!.GetValue<string>()}", commit.ToJsonString());
    }
  }

  private void GivenPull(string status)
  {
    var pull = JsonNode.Parse(Fixture("pull.json"))!;
    pull["status"] = status;
    GivenBody($"{Root}/pullrequests/22", pull.ToJsonString());
  }

  private void GivenBody(string path, string body) =>
    _azure.Server
          .Given(Request.Create().WithPath(path).UsingGet())
          .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody(body));

  private void GivenFixture(string path, string fixture) => GivenBody(path, Fixture(fixture));

  private void GivenStatus(string path, int status) =>
    _azure.Server.Given(Request.Create().WithPath(path).UsingGet()).RespondWith(Response.Create().WithStatusCode(status));

  #endregion

  #region Implementation of IDisposable

  public void Dispose() => _azure.Dispose();

  #endregion
}
