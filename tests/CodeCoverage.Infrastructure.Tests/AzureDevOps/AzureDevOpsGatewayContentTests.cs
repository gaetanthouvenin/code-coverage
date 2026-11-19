// ------------------------------------------------------------------------------------------------
// <copyright file="AzureDevOpsGatewayContentTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Text.Json.Nodes;

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Infrastructure.Forges;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.AzureDevOps;

public sealed class AzureDevOpsGatewayContentTests : IDisposable
{
  #region Fields

  private const string RepositoryId = "1701bc47-1145-4739-a805-64b02e40dbe7";

  private const string Root = "/contoso/_apis/git/repositories/" + RepositoryId;

  private const string AccessToken = "eyJ.application";

  private const string Yaml = "coverage:\n  status:\n    project: true\n";

  private static readonly string TokenPath = string.Create(CultureInfo.InvariantCulture, $"/{AzureDevOpsApiHarness.TenantId:D}/oauth2/v2.0/token");

  private static readonly GitSha Head = GitSha.Parse("be67f8871a4d2c75f13a51c1d3c30ac0d74d4ef4");

  private static readonly GitSha RootTree = GitSha.Parse("7fa1a3523ffef51c525ea476bffff7d648b8cb3d");

  private static readonly GitSha GithubTree = GitSha.Parse("ea6765e1976b9e8a6d4981fd8febebd574a91571");

  private static readonly ForgeRepositoryRef Repository = new(Forge.AzureDevOps, AzureDevOpsApiHarness.ConnectionId, RepositoryId);

  private readonly AzureDevOpsApiHarness _azure = new();

  #endregion

  #region Ctors

  public AzureDevOpsGatewayContentTests()
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
  public async Task GetTreeAsync_Recursive_ShouldListBlobsOnly()
  {
    GivenFixture($"{Root}/commits/{Head.Value}", "commit.json");
    GivenFixture($"{Root}/trees/{RootTree.Value}", "tree.json");

    var tree = await _azure.Gateway.GetTreeAsync(Repository, Head, recursive: true, Cancellation);

    tree.ShouldNotBeNull();
    tree.Sha.ShouldBe(Head);
    tree.Truncated.ShouldBeFalse();
    tree.Entries.Where(static e => !e.IsDirectory).Select(static e => e.Path).ShouldBe([".github/codecov.yml", "src/Program.cs", "README.md"]);
    tree.Entries.Where(static e => e.IsDirectory).Select(static e => e.Path).ShouldBe([".github", "src"]);
    tree.Entries.ShouldNotContain(static e => e.Path == "vendor/shared");
    tree.Entries.Single(static e => e.Path == ".github").Sha.ShouldBe(GithubTree);
    ForgeRequests.Last().Query!["recursive"].ShouldBe(["true"]);
  }

  [Fact]
  public async Task GetTreeAsync_NotRecursive_ShouldAskForOneLevel()
  {
    GivenFixture($"{Root}/commits/{Head.Value}", "commit.json");
    GivenFixture($"{Root}/trees/{RootTree.Value}", "tree.json");

    await _azure.Gateway.GetTreeAsync(Repository, Head, recursive: false, Cancellation);

    ForgeRequests.Last().Query!["recursive"].ShouldBe(["false"]);
  }

  [Fact]
  public async Task GetTreeAsync_TreeSha_ShouldListTheTreeOfTheDirectory()
  {
    GivenStatus($"{Root}/commits/{GithubTree.Value}", 404);
    GivenBody(
      $"{Root}/trees/{GithubTree.Value}",
      """{"objectId":"ea6765e1976b9e8a6d4981fd8febebd574a91571","treeEntries":[{"objectId":"9093f030aa7dd8c802cad228fae4c6bafae4b32f","relativePath":"codecov.yml","gitObjectType":"blob"}]}"""
    );

    var tree = await _azure.Gateway.GetTreeAsync(Repository, GithubTree, recursive: false, Cancellation);

    tree.ShouldNotBeNull();
    tree.Sha.ShouldBe(GithubTree);
    tree.Entries.ShouldHaveSingleItem().ShouldBe(new ForgeTreeEntry("codecov.yml", GitSha.Parse("9093f030aa7dd8c802cad228fae4c6bafae4b32f"), false));
  }

  [Fact]
  public async Task GetTreeAsync_Missing_ShouldReturnNull()
  {
    GivenStatus($"{Root}/commits/{Head.Value}", 404);
    GivenStatus($"{Root}/trees/{Head.Value}", 404);

    var tree = await _azure.Gateway.GetTreeAsync(Repository, Head, recursive: true, Cancellation);

    tree.ShouldBeNull();
  }

  [Fact]
  public async Task GetTreeAsync_CommitWithoutTree_ShouldThrowUnavailable()
  {
    var commit = JsonNode.Parse(Fixture("commit.json"))!.AsObject();
    commit.Remove("treeId");
    GivenBody($"{Root}/commits/{Head.Value}", commit.ToJsonString());

    var exception = await Should.ThrowAsync<BadGatewayException>(() => _azure.Gateway.GetTreeAsync(Repository, Head, recursive: true, Cancellation));

    exception.ErrorCode.ShouldBe(ForgeErrors.Unavailable);
  }

  [Fact]
  public async Task GetContentAsync_ShouldReadTheRawFileAtTheCommit()
  {
    GivenRaw($"{Root}/items", Yaml);

    var content = await _azure.Gateway.GetContentAsync(Repository, ".github/codecov.yml", Head, Cancellation);

    content.ShouldBe(Yaml);
    var request = ForgeRequests.ShouldHaveSingleItem();
    request.Headers!["Authorization"].ShouldHaveSingleItem().ShouldBe($"Bearer {AccessToken}");
    request.Query!["path"].ShouldBe(["/.github/codecov.yml"]);
    request.Query!["versionDescriptor.version"].ShouldBe([Head.Value]);
    request.Query!["versionDescriptor.versionType"].ShouldBe(["commit"]);
    request.Query!["api-version"].ShouldBe(["7.1"]);
  }

  [Fact]
  public async Task GetContentAsync_Missing_ShouldReturnNull()
  {
    GivenStatus($"{Root}/items", 404);

    var content = await _azure.Gateway.GetContentAsync(Repository, "codecov.yml", Head, Cancellation);

    content.ShouldBeNull();
  }

  [Fact]
  public async Task GetContentAsync_Forbidden_ShouldThrowUnavailable()
  {
    GivenStatus($"{Root}/items", 403);

    var exception = await Should.ThrowAsync<BadGatewayException>(() => _azure.Gateway.GetContentAsync(Repository, "codecov.yml", Head, Cancellation));

    exception.ErrorCode.ShouldBe(ForgeErrors.Unavailable);
  }

  [Theory]
  [InlineData("../codecov.yml")]
  [InlineData("dev//codecov.yml")]
  [InlineData("")]
  public async Task GetContentAsync_PathLeavingTheRepository_ShouldReturnNullWithoutAsking(string path)
  {
    var content = await _azure.Gateway.GetContentAsync(Repository, path, Head, Cancellation);

    content.ShouldBeNull();
    ForgeRequests.ShouldBeEmpty();
  }

  [Fact]
  public async Task GetBoundedContentAsync_WithinTheBound_ShouldReturnTheText()
  {
    GivenRaw($"{Root}/items", Yaml);

    var content = await _azure.Gateway.GetBoundedContentAsync(Repository, "src/Program.cs", Head, 1024, Cancellation);

    content.ShouldNotBeNull().Text.ShouldBe(Yaml);
  }

  [Fact]
  public async Task GetBoundedContentAsync_TooLarge_ShouldReportTruncated()
  {
    GivenRaw($"{Root}/items", new string('x', 2048));

    var content = await _azure.Gateway.GetBoundedContentAsync(Repository, "src/Program.cs", Head, 1024, Cancellation);

    content.ShouldNotBeNull().IsTooLarge.ShouldBeTrue();
  }

  [Fact]
  public async Task GetBoundedContentAsync_Missing_ShouldReturnNull()
  {
    GivenStatus($"{Root}/items", 404);

    var content = await _azure.Gateway.GetBoundedContentAsync(Repository, "src/Program.cs", Head, 1024, Cancellation);

    content.ShouldBeNull();
  }

  private static string Fixture(string name) =>
    File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "azure-devops", name));

  private void GivenBody(string path, string body) =>
    _azure.Server
          .Given(Request.Create().WithPath(path).UsingGet())
          .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody(body));

  private void GivenFixture(string path, string fixture) => GivenBody(path, Fixture(fixture));

  private void GivenRaw(string path, string body) =>
    _azure.Server
          .Given(Request.Create().WithPath(path).UsingGet())
          .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/octet-stream").WithBody(body));

  private void GivenStatus(string path, int status) =>
    _azure.Server.Given(Request.Create().WithPath(path).UsingGet()).RespondWith(Response.Create().WithStatusCode(status));

  #endregion

  #region Implementation of IDisposable

  public void Dispose() => _azure.Dispose();

  #endregion
}
