// ------------------------------------------------------------------------------------------------
// <copyright file="AzureDevOpsUserGatewayTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Forges;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.AzureDevOps;

// The profile service is played by WireMock behind the real pipeline of the profile client; the token is the one
// Entra ID delegated to the user, sent as it is.
public sealed class AzureDevOpsUserGatewayTests : IDisposable
{
  #region Fields

  private const string Token = "eyJ.user";

  private const string ProfilePath = "/_apis/profile/profiles/me";

  private const string AccountsPath = "/_apis/accounts";

  private const string ProfileId = "6c1e8a0b-3f4d-4e5a-9b7c-2d1e0f9a8b7c";

  private readonly AzureDevOpsApiHarness _azure = new();

  #endregion

  #region Properties

  private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

  private IForgeUserGateway Gateway => _azure.UserGateway;

  #endregion

  #region Methods

  [Fact]
  public async Task GetUserAsync_Profile_ShouldMapTheId()
  {
    GivenProfile();

    var user = await Gateway.GetUserAsync(Token, Cancellation);

    user.ShouldBe(new ForgeUser(ProfileId, "Ada Lovelace", null));

    var request = _azure.Server.LogEntries.ShouldHaveSingleItem().RequestMessage!;
    request.Headers!["Authorization"].ShouldHaveSingleItem().ShouldBe($"Bearer {Token}");
    request.Query!["api-version"].ShouldHaveSingleItem().ShouldBe("7.1");
  }

  [Fact]
  public async Task ListAccountsAsync_TwoOrganizations_ShouldListBoth()
  {
    GivenProfile();
    _azure.Server
          .Given(Request.Create().WithPath(AccountsPath).WithParam("memberId", ProfileId).UsingGet())
          .RespondWith(Response.Create().WithStatusCode(200).WithBody(Fixture("accounts.json")));

    var accounts = await Gateway.ListAccountsAsync(Token, Cancellation);

    accounts.ShouldBe(
      [
        new ForgeUserAccount("contoso", "0f6b6c4e-3d1a-4a52-9b0e-3c2d1e0f9a8b", "contoso", OrganizationAccountType.Organization, false),
        new ForgeUserAccount("fabrikam", "9a8b7c6d-5e4f-4a3b-8c2d-1e0f9a8b7c6d", "fabrikam", OrganizationAccountType.Organization, false),
      ]
    );
    _azure.Server.LogEntries.Select(static e => e.RequestMessage!.Headers!["Authorization"].ShouldHaveSingleItem()).ShouldAllBe(static h => h == $"Bearer {Token}");
  }

  // Azure DevOps answers a token it refuses with a 401, or with a 203 that carries its sign-in page.
  [Theory]
  [InlineData(401, """{"message":"TF400813"}""")]
  [InlineData(203, "<html>Sign in</html>")]
  public async Task GetUserAsync_TokenRefused_ShouldThrowUnauthenticated(int status, string body)
  {
    _azure.Server.Given(Request.Create().WithPath(ProfilePath).UsingGet()).RespondWith(Response.Create().WithStatusCode(status).WithBody(body));

    var exception = await Should.ThrowAsync<UnauthorizedException>(() => Gateway.GetUserAsync(Token, Cancellation));

    exception.ErrorCode.ShouldBe(SecurityErrors.Unauthenticated);
    exception.Message.ShouldNotContain(Token);
  }

  [Theory]
  [InlineData(403, "{}")]
  [InlineData(404, "{}")]
  [InlineData(200, "not json")]
  [InlineData(200, """{"displayName":"Ada"}""")]
  public async Task GetUserAsync_RefusedOrUnreadable_ShouldThrowBadGateway(int status, string body)
  {
    _azure.Server.Given(Request.Create().WithPath(ProfilePath).UsingGet()).RespondWith(Response.Create().WithStatusCode(status).WithBody(body));

    var exception = await Should.ThrowAsync<BadGatewayException>(() => Gateway.GetUserAsync(Token, Cancellation));

    exception.ErrorCode.ShouldBe(ForgeErrors.Unavailable);
  }

  [Fact]
  public async Task ListAccountsAsync_TokenRefusedOnTheAccounts_ShouldThrowUnauthenticated()
  {
    GivenProfile();
    _azure.Server.Given(Request.Create().WithPath(AccountsPath).UsingGet()).RespondWith(Response.Create().WithStatusCode(401));

    (await Should.ThrowAsync<UnauthorizedException>(() => Gateway.ListAccountsAsync(Token, Cancellation))).ErrorCode.ShouldBe(SecurityErrors.Unauthenticated);
  }

  [Fact]
  public async Task ListRepositoryPermissionsAsync_NotBuiltYet_ShouldThrowNotSupported()
  {
    var account = new ForgeUserAccount("contoso", "0f6b6c4e-3d1a-4a52-9b0e-3c2d1e0f9a8b", "contoso", OrganizationAccountType.Organization, false);

    await Should.ThrowAsync<NotSupportedException>(() => Gateway.ListRepositoryPermissionsAsync(Token, account, Cancellation));
  }

  [Fact]
  public async Task GetUserAsync_EmptyToken_ShouldThrowArgumentException()
  {
    await Should.ThrowAsync<ArgumentException>(() => Gateway.GetUserAsync(" ", Cancellation));

    _azure.Server.LogEntries.ShouldBeEmpty();
  }

  private static string Fixture(string name) =>
    File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "azure-devops", name));

  private void GivenProfile() =>
    _azure.Server
          .Given(Request.Create().WithPath(ProfilePath).UsingGet())
          .RespondWith(Response.Create().WithStatusCode(200).WithBody(Fixture("profile.json")));

  #endregion

  #region Implementation of IDisposable

  public void Dispose() => _azure.Dispose();

  #endregion
}
