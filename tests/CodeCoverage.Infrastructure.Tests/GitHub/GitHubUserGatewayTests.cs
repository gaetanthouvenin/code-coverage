// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubUserGatewayTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Users;
using CodeCoverage.Infrastructure.Diagnostics;
using CodeCoverage.Infrastructure.Forges;
using CodeCoverage.Infrastructure.GitHub;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.GitHub;

public sealed class GitHubUserGatewayTests : IDisposable
{
  #region Fields

  private const string Token = "ghu_user_token";

  private const string Repositories = "/user/installations/42/repositories";

  private readonly GitHubApiHarness _github = new();

  #endregion

  #region Properties

  private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task GetUser_ShouldReadTheAccountWithTheTokenOfTheUser()
  {
    _github.Server.Given(Request.Create().WithPath("/user").UsingGet())
           .RespondWith(Response.Create().WithStatusCode(200).WithHeader("x-ratelimit-remaining", "4999").WithBody("""{"id":583231,"login":"Octocat","avatar_url":"https://avatars.example/u/583231","type":"User"}"""));

    var user = await _github.UserGateway.GetUserAsync(Token, Cancellation);

    user.ExternalId.ShouldBe("583231");
    user.Login.ShouldBe("Octocat");
    user.AvatarUrl.ShouldBe("https://avatars.example/u/583231");

    var request = _github.Server.LogEntries.ShouldHaveSingleItem().RequestMessage!;
    request.Headers!["Authorization"].ShouldHaveSingleItem().ShouldBe($"Bearer {Token}");
    request.Headers["X-GitHub-Api-Version"].ShouldHaveSingleItem().ShouldBe("2022-11-28");

    // The quota of a user is not the one of an installation nor of the App.
    _github.Quota.Collect();
    _github.Quota.Of(CodeCoverageMeter.GitHubRateLimitRemaining).ShouldBeEmpty();
  }

  // GitHub no longer accepts the token: the session is over, the user signs in again.
  [Fact]
  public async Task GetUser_TokenRefused_ShouldThrowUnauthenticated()
  {
    _github.Server.Given(Request.Create().WithPath("/user").UsingGet()).RespondWith(Response.Create().WithStatusCode(401).WithBody("""{"message":"Bad credentials"}"""));

    var exception = await Should.ThrowAsync<UnauthorizedException>(() => _github.UserGateway.GetUserAsync(Token, Cancellation));

    exception.ErrorCode.ShouldBe(SecurityErrors.Unauthenticated);
  }

  [Theory]
  [InlineData(403, "{}")]
  [InlineData(404, "{}")]
  [InlineData(500, "{}")]
  [InlineData(200, "not json")]
  [InlineData(200, """{"id":1}""")]
  public async Task GetUser_RefusedOrUnreadable_ShouldThrowBadGateway(int status, string body)
  {
    _github.Server.Given(Request.Create().WithPath("/user").UsingGet()).RespondWith(Response.Create().WithStatusCode(status).WithBody(body));

    var exception = await Should.ThrowAsync<BadGatewayException>(() => _github.UserGateway.GetUserAsync(Token, Cancellation));

    exception.ErrorCode.ShouldBe(ForgeErrors.Unavailable);
  }

  [Fact]
  public async Task ListInstallationRepositories_ShouldMapTheHighestPermissionOfEachRepository()
  {
    GivenPage(
      1,
      """
      {"total_count":4,"repositories":[
        {"id":1,"permissions":{"admin":true,"push":true,"pull":true}},
        {"id":2,"permissions":{"admin":false,"push":true,"pull":true}},
        {"id":3,"permissions":{"admin":false,"push":false,"pull":true}},
        {"id":4}
      ]}
      """
    );

    var repositories = await _github.UserGateway.ListInstallationRepositoriesAsync(Token, 42, Cancellation);

    repositories.Select(static r => (r.ExternalId, r.Permission))
                .ShouldBe([("1", RepositoryPermission.Admin), ("2", RepositoryPermission.Write), ("3", RepositoryPermission.Read), ("4", RepositoryPermission.Read)]);
    _github.Server.LogEntries.ShouldHaveSingleItem().RequestMessage!.Headers!["Authorization"].ShouldHaveSingleItem().ShouldBe($"Bearer {Token}");
  }

  // A listing cut short would silently take the access of a user to the repositories of the next pages away.
  [Fact]
  public async Task ListInstallationRepositories_FullPage_ShouldReadTheNextOne()
  {
    GivenPage(1, Page(203, 1, 100));
    GivenPage(2, Page(203, 101, 100));
    GivenPage(3, Page(203, 201, 3));

    var repositories = await _github.UserGateway.ListInstallationRepositoriesAsync(Token, 42, Cancellation);

    repositories.Count.ShouldBe(203);
    repositories.Select(static r => r.ExternalId).ShouldBeUnique();
  }

  [Fact]
  public async Task ListInstallationRepositories_InstallationTheUserCannotReach_ShouldBeEmpty()
  {
    _github.Server.Given(Request.Create().WithPath(Repositories).UsingGet()).RespondWith(Response.Create().WithStatusCode(404));

    (await _github.UserGateway.ListInstallationRepositoriesAsync(Token, 42, Cancellation)).ShouldBeEmpty();
  }

  [Fact]
  public async Task ListInstallationRepositories_ListThatShiftedWhileItWasRead_ShouldThrowBadGateway()
  {
    GivenPage(1, Page(5, 1, 3));

    await Should.ThrowAsync<BadGatewayException>(() => _github.UserGateway.ListInstallationRepositoriesAsync(Token, 42, Cancellation));
  }

  [Fact]
  public async Task ListInstallationRepositories_TokenRefused_ShouldThrowUnauthenticated()
  {
    _github.Server.Given(Request.Create().WithPath(Repositories).UsingGet()).RespondWith(Response.Create().WithStatusCode(401));

    var exception = await Should.ThrowAsync<UnauthorizedException>(() => _github.UserGateway.ListInstallationRepositoriesAsync(Token, 42, Cancellation));

    exception.ErrorCode.ShouldBe(SecurityErrors.Unauthenticated);
  }

  [Fact]
  public async Task Gateway_InvalidArguments_ShouldThrow()
  {
    await Should.ThrowAsync<ArgumentException>(() => _github.UserGateway.GetUserAsync(" ", Cancellation));
    await Should.ThrowAsync<ArgumentException>(() => _github.UserGateway.GetUserAsync(null!, Cancellation));
    await Should.ThrowAsync<ArgumentException>(() => _github.UserGateway.ListInstallationRepositoriesAsync(string.Empty, 42, Cancellation));
    await Should.ThrowAsync<ArgumentOutOfRangeException>(() => _github.UserGateway.ListInstallationRepositoriesAsync(Token, 0, Cancellation));
  }

  // M7.15: the neutral port reads GitHub the same way; an account is an installation, its connection the installation.
  [Fact]
  public async Task ForgeGetUser_ShouldMapTheAccount()
  {
    _github.Server.Given(Request.Create().WithPath("/user").UsingGet())
           .RespondWith(Response.Create().WithStatusCode(200).WithBody("""{"id":583231,"login":"Octocat","avatar_url":"https://avatars.example/u/583231"}"""));

    var user = await ((IForgeUserGateway)_github.UserGateway).GetUserAsync(Token, Cancellation);

    user.ShouldBe(new ForgeUser("583231", "Octocat", "https://avatars.example/u/583231"));
  }

  [Fact]
  public async Task ForgeListAccounts_ShouldMapEachInstallation()
  {
    _github.Server.Given(Request.Create().WithPath("/user/installations").UsingGet())
           .RespondWith(
             Response.Create()
                     .WithStatusCode(200)
                     .WithBody(
                       """
                       {"total_count":2,"installations":[
                         {"id":42,"account":{"id":1001,"login":"octo-org","type":"Organization"}},
                         {"id":44,"account":{"id":583231,"login":"octocat","type":"User"},"suspended_at":"2026-11-01T00:00:00Z"}
                       ]}
                       """
                     )
           );

    var accounts = await ((IForgeUserGateway)_github.UserGateway).ListAccountsAsync(Token, Cancellation);

    accounts.ShouldBe(
      [
        new ForgeUserAccount("42", "1001", "octo-org", OrganizationAccountType.Organization, false),
        new ForgeUserAccount("44", "583231", "octocat", OrganizationAccountType.User, true),
      ]
    );
  }

  [Fact]
  public async Task ForgeListRepositoryPermissions_ShouldReadTheRepositoriesOfTheInstallation()
  {
    GivenPage(1, """{"total_count":2,"repositories":[{"id":1,"permissions":{"admin":true}},{"id":2,"permissions":{"push":true}}]}""");
    var account = new ForgeUserAccount("42", "1001", "octo-org", OrganizationAccountType.Organization, false);

    var permissions = await ((IForgeUserGateway)_github.UserGateway).ListRepositoryPermissionsAsync(Token, account, Cancellation);

    permissions.ShouldBe([new ForgeRepositoryPermission("1", RepositoryPermission.Admin), new ForgeRepositoryPermission("2", RepositoryPermission.Write)]);
  }

  [Theory]
  [InlineData("octo-org")]
  [InlineData("0")]
  [InlineData("-42")]
  public async Task ForgeListRepositoryPermissions_ConnectionThatIsNoInstallation_ShouldThrow(string connectionId)
  {
    var account = new ForgeUserAccount(connectionId, "1001", "octo-org", OrganizationAccountType.Organization, false);

    await Should.ThrowAsync<ArgumentException>(() => ((IForgeUserGateway)_github.UserGateway).ListRepositoryPermissionsAsync(Token, account, Cancellation));
    await Should.ThrowAsync<ArgumentNullException>(() => ((IForgeUserGateway)_github.UserGateway).ListRepositoryPermissionsAsync(Token, null!, Cancellation));
    _github.Server.LogEntries.ShouldBeEmpty();
  }

  private static string Page(int total, int first, int count) =>
    "{\"total_count\":" + total.ToString(CultureInfo.InvariantCulture) + ",\"repositories\":["
    + string.Join(',', Enumerable.Range(first, count).Select(static id => "{\"id\":" + id.ToString(CultureInfo.InvariantCulture) + ",\"permissions\":{\"pull\":true}}"))
    + "]}";

  private void GivenPage(int page, string body) =>
    _github.Server.Given(Request.Create().WithPath(Repositories).WithParam("page", page.ToString(CultureInfo.InvariantCulture)).WithParam("per_page", "100").UsingGet())
           .RespondWith(Response.Create().WithStatusCode(200).WithBody(body));

  #endregion

  #region Implementation of IDisposable

  public void Dispose() => _github.Dispose();

  #endregion
}
