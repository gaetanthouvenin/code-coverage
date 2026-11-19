// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubDirectoryGatewayTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Security.Cryptography;

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Infrastructure.GitHub;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.GitHub;

public sealed class GitHubDirectoryGatewayTests : IDisposable
{
  #region Fields

  private const long Installation = 42;

  private static readonly RSA Key = RSA.Create(2048);

  private readonly GitHubApiHarness _github = new();

  #endregion

  #region Ctors

  public GitHubDirectoryGatewayTests()
  {
    _github.UsePrivateKey(Key);
    _github.Server
           .Given(Request.Create().WithPath($"/app/installations/{Installation}/access_tokens").UsingPost())
           .RespondWith(
             Response.Create()
                     .WithStatusCode(201)
                     .WithBody(
                       "{\"token\":\"ghs_installation\",\"expires_at\":\"" + DateTimeOffset.UtcNow.AddHours(1).ToString("O", CultureInfo.InvariantCulture) + "\"}"
                     )
           );
  }

  #endregion

  #region Properties

  private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

  private static ForgeConnectionRef Connection => new(Forge.GitHub, "42");

  private static ForgeAccountInfo OctoOrg => new("1001", "octo-org");

  private static ForgeUser Octocat => new("583231", "octocat", null);

  private GitHubDirectoryGateway Gateway => new(_github.Gateway);

  #endregion

  #region Methods

  [Fact]
  public async Task ListRepositoriesAsync_Installation_ShouldMapNamespaceEmpty()
  {
    _github.Server
           .Given(Request.Create().WithPath("/installation/repositories").WithParam("page", "1").UsingGet())
           .RespondWith(
             Response.Create()
                     .WithStatusCode(200)
                     .WithHeader("Content-Type", "application/json")
                     .WithBody(
                       """{"total_count":1,"repositories":[{"id":7,"name":"app","default_branch":"main","private":true,"owner":{"login":"first-org","id":1001}}]}"""
                     )
           );

    var repository = (await Gateway.ListRepositoriesAsync(Connection, Cancellation)).ShouldHaveSingleItem();

    repository.ShouldBe(new ForgeRepositoryInfo("7", "first-org", string.Empty, "app", "main", IsPrivate: true, OwnerExternalId: "1001"));
  }

  [Fact]
  public async Task GetAccountAsync_Installation_ShouldMapTheAccountOfTheInstallation()
  {
    _github.Server
           .Given(Request.Create().WithPath("/app/installations/42").UsingGet())
           .RespondWith(
             Response.Create()
                     .WithStatusCode(200)
                     .WithBody("""{"id":42,"account":{"id":1001,"login":"first-org","type":"Organization"},"suspended_at":null}""")
           );

    var account = await Gateway.GetAccountAsync(Connection, Cancellation);

    account.ShouldBe(new ForgeAccountInfo("1001", "first-org"));
  }

  [Fact]
  public async Task GetAccountAsync_UnknownInstallation_ShouldReturnNull()
  {
    _github.Server.Given(Request.Create().WithPath("/app/installations/42").UsingGet()).RespondWith(Response.Create().WithStatusCode(404));

    (await Gateway.GetAccountAsync(Connection, Cancellation)).ShouldBeNull();
  }

  [Fact]
  public async Task ListRepositoriesAsync_ConnectionOfAnotherForge_ShouldThrow()
  {
    await Should.ThrowAsync<ArgumentException>(() => Gateway.ListRepositoriesAsync(new ForgeConnectionRef(Forge.AzureDevOps, "42"), Cancellation));

    _github.Server.LogEntries.ShouldBeEmpty();
  }

  [Fact]
  public async Task GetAccountAsync_ConnectionNotAnInstallation_ShouldThrow()
  {
    await Should.ThrowAsync<ArgumentException>(() => Gateway.GetAccountAsync(new ForgeConnectionRef(Forge.GitHub, "contoso"), Cancellation));
  }

  [Fact]
  public async Task GetMembershipAsync_ActiveAdmin_ShouldMapTheMembershipOfTheUser()
  {
    _github.Server
           .Given(Request.Create().WithPath("/orgs/octo-org/memberships/octocat").UsingGet())
           .RespondWith(Response.Create().WithStatusCode(200).WithBody("""{"state":"active","role":"admin","user":{"id":583231,"login":"octocat"}}"""));

    var membership = await Gateway.GetMembershipAsync(Connection, OctoOrg, Octocat, Cancellation);

    membership.ShouldBe(new ForgeMembership(true, true, "583231"));
  }

  [Fact]
  public async Task GetMembershipAsync_NotAMember_ShouldReturnNull()
  {
    _github.Server.Given(Request.Create().WithPath("/orgs/octo-org/memberships/octocat").UsingGet()).RespondWith(Response.Create().WithStatusCode(404).WithBody("{}"));

    (await Gateway.GetMembershipAsync(Connection, OctoOrg, Octocat, Cancellation)).ShouldBeNull();
  }

  [Fact]
  public async Task GetMembershipAsync_InvalidArguments_ShouldThrowWithoutCallingGitHub()
  {
    await Should.ThrowAsync<ArgumentException>(() => Gateway.GetMembershipAsync(new ForgeConnectionRef(Forge.AzureDevOps, "42"), OctoOrg, Octocat, Cancellation));
    await Should.ThrowAsync<ArgumentNullException>(() => Gateway.GetMembershipAsync(null!, OctoOrg, Octocat, Cancellation));
    await Should.ThrowAsync<ArgumentNullException>(() => Gateway.GetMembershipAsync(Connection, null!, Octocat, Cancellation));
    await Should.ThrowAsync<ArgumentNullException>(() => Gateway.GetMembershipAsync(Connection, OctoOrg, null!, Cancellation));

    _github.Server.LogEntries.ShouldBeEmpty();
  }

  #endregion

  #region Implementation of IDisposable

  public void Dispose() => _github.Dispose();

  #endregion
}
