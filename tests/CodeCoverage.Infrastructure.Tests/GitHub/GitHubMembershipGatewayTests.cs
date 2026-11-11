// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubMembershipGatewayTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Security.Cryptography;

using Microsoft.Extensions.Logging;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Infrastructure.Tests.Support;

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace CodeCoverage.Infrastructure.Tests.GitHub;

// What the refresh of the memberships reads: the installations a user reaches, with its token, and its membership
// of an organization, with the token of the installation.
public sealed class GitHubMembershipGatewayTests : IDisposable
{
  #region Fields

  private const string UserToken = "ghu_user_token";

  private const string InstallationToken = "ghs_installation";

  private const long Installation = 42;

  private const string Membership = "/orgs/octo-org/memberships/octocat";

  private static readonly RSA Key = RSA.Create(2048);

  private readonly GitHubApiHarness _github = new();

  #endregion

  #region Ctors

  public GitHubMembershipGatewayTests()
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
  public async Task ListInstallations_ShouldMapTheAccountsTheUserReachesWithItsToken()
  {
    GivenInstallations(
      1,
      """
      {"total_count":3,"installations":[
        {"id":42,"account":{"id":1001,"login":"octo-org","type":"Organization"},"suspended_at":null},
        {"id":43,"account":{"id":583231,"login":"octocat","type":"User"}},
        {"id":44,"account":{"id":2002,"login":"asleep-org","type":"Organization"},"suspended_at":"2026-11-01T00:00:00Z"}
      ]}
      """
    );

    var installations = await _github.UserGateway.ListInstallationsAsync(UserToken, Cancellation);

    installations.Select(static i => (i.Id, i.AccountId, i.AccountLogin, i.AccountType, i.Suspended))
                 .ShouldBe(
                   [
                     (42, "1001", "octo-org", OrganizationAccountType.Organization, false),
                     (43, "583231", "octocat", OrganizationAccountType.User, false),
                     (44, "2002", "asleep-org", OrganizationAccountType.Organization, true),
                   ]
                 );
    _github.Server.LogEntries.ShouldHaveSingleItem().RequestMessage!.Headers!["Authorization"].ShouldHaveSingleItem().ShouldBe($"Bearer {UserToken}");
  }

  // A listing cut short would take organizations away from the user.
  [Fact]
  public async Task ListInstallations_FullPage_ShouldReadTheNextOne()
  {
    GivenInstallations(1, Page(103, 1, 100));
    GivenInstallations(2, Page(103, 101, 3));

    var installations = await _github.UserGateway.ListInstallationsAsync(UserToken, Cancellation);

    installations.Count.ShouldBe(103);
    installations.Select(static i => i.Id).ShouldBeUnique();
  }

  [Fact]
  public async Task ListInstallations_None_ShouldBeEmpty()
  {
    GivenInstallations(1, """{"total_count":0,"installations":[]}""");

    (await _github.UserGateway.ListInstallationsAsync(UserToken, Cancellation)).ShouldBeEmpty();
  }

  [Fact]
  public async Task ListInstallations_ListThatShiftedWhileItWasRead_ShouldThrowBadGateway()
  {
    GivenInstallations(1, Page(5, 1, 3));

    await Should.ThrowAsync<BadGatewayException>(() => _github.UserGateway.ListInstallationsAsync(UserToken, Cancellation));
  }

  [Fact]
  public async Task ListInstallations_TokenRefused_ShouldThrowUnauthenticated()
  {
    _github.Server.Given(Request.Create().WithPath("/user/installations").UsingGet()).RespondWith(Response.Create().WithStatusCode(401));

    var exception = await Should.ThrowAsync<UnauthorizedException>(() => _github.UserGateway.ListInstallationsAsync(UserToken, Cancellation));

    exception.ErrorCode.ShouldBe(SecurityErrors.Unauthenticated);
  }

  [Theory]
  [InlineData(403, "{}")]
  [InlineData(404, "{}")]
  [InlineData(500, "{}")]
  [InlineData(200, "not json")]
  public async Task ListInstallations_RefusedOrUnreadable_ShouldThrowBadGateway(int status, string body)
  {
    _github.Server.Given(Request.Create().WithPath("/user/installations").UsingGet()).RespondWith(Response.Create().WithStatusCode(status).WithBody(body));

    await Should.ThrowAsync<BadGatewayException>(() => _github.UserGateway.ListInstallationsAsync(UserToken, Cancellation));
    await Should.ThrowAsync<ArgumentException>(() => _github.UserGateway.ListInstallationsAsync(" ", Cancellation));
  }

  [Theory]
  [InlineData("active", "admin", true, true)]
  [InlineData("active", "member", true, false)]
  [InlineData("active", "billing_manager", true, false)]
  // Invited to own the organization, not accepted yet.
  [InlineData("pending", "admin", false, true)]
  [InlineData("pending", "member", false, false)]
  // The comparison is exact: what GitHub does not spell as documented grants nothing.
  [InlineData("Active", "Admin", false, false)]
  public async Task GetOrganizationMembership_ShouldMapTheStateAndTheRole(string state, string role, bool active, bool admin)
  {
    _github.Server.Given(Request.Create().WithPath(Membership).UsingGet())
           .RespondWith(Response.Create().WithStatusCode(200).WithBody("{\"state\":\"" + state + "\",\"role\":\"" + role + "\",\"user\":{\"id\":583231,\"login\":\"octocat\"}}"));

    var membership = (await _github.Gateway.GetOrganizationMembershipAsync(Installation, "octo-org", "octocat", Cancellation)).ShouldNotBeNull();

    membership.Active.ShouldBe(active);
    membership.Admin.ShouldBe(admin);
    // F11. Whose membership it is.
    membership.UserExternalId.ShouldBe("583231");

    // Asked with the token of the installation: the token of a user would need the scope read:org.
    _github.Server.LogEntries.Single(static e => e.RequestMessage!.Path == Membership)
           .RequestMessage!.Headers!["Authorization"].ShouldHaveSingleItem().ShouldBe($"Bearer {InstallationToken}");
  }

  [Fact]
  public async Task GetOrganizationMembership_PayloadWithoutStateNorRole_ShouldGrantNothing()
  {
    _github.Server.Given(Request.Create().WithPath(Membership).UsingGet()).RespondWith(Response.Create().WithStatusCode(200).WithBody("{}"));

    var membership = (await _github.Gateway.GetOrganizationMembershipAsync(Installation, "octo-org", "octocat", Cancellation)).ShouldNotBeNull();

    membership.Active.ShouldBeFalse();
    membership.Admin.ShouldBeFalse();
    membership.UserExternalId.ShouldBeNull();
  }

  // 404: not a member, an outside collaborator for instance.
  [Fact]
  public async Task GetOrganizationMembership_NotAMember_ShouldBeNull()
  {
    _github.Server.Given(Request.Create().WithPath(Membership).UsingGet()).RespondWith(Response.Create().WithStatusCode(404).WithBody("{}"));

    (await _github.Gateway.GetOrganizationMembershipAsync(Installation, "octo-org", "octocat", Cancellation)).ShouldBeNull();
  }

  // F12. The App lacks the permission Organization > Members: read. The user takes part and never administers; the
  // administrator of the instance is told, since every owner of the organization is a participant meanwhile.
  [Fact]
  public async Task GetOrganizationMembership_MembersNotReadableByTheApp_ShouldBeNullAndWarn()
  {
    _github.Server.Given(Request.Create().WithPath(Membership).UsingGet())
           .RespondWith(Response.Create().WithStatusCode(403).WithBody("""{"message":"Resource not accessible by integration","documentation_url":"https://docs.github.com/rest"}"""));

    (await _github.Gateway.GetOrganizationMembershipAsync(Installation, "octo-org", "octocat", Cancellation)).ShouldBeNull();

    _github.GatewayLog.Entries.ShouldContain(static entry => entry.EventId.Id == 1500 && entry.Level == LogLevel.Warning);
  }

  // F12, S8. Any other refusal is a refusal of this organization: the caller fails closed for it alone.
  [Theory]
  [InlineData("""{"message":"Resource protected by organization SAML enforcement."}""")]
  [InlineData("{}")]
  [InlineData("not json")]
  public async Task GetOrganizationMembership_OtherRefusal_ShouldThrowForbidden(string body)
  {
    _github.Server.Given(Request.Create().WithPath(Membership).UsingGet()).RespondWith(Response.Create().WithStatusCode(403).WithBody(body));

    var exception = await Should.ThrowAsync<ForbiddenException>(() => _github.Gateway.GetOrganizationMembershipAsync(Installation, "octo-org", "octocat", Cancellation));

    exception.ErrorCode.ShouldBe(SecurityErrors.Forbidden);
  }

  [Theory]
  [InlineData(500, "{}")]
  [InlineData(502, "{}")]
  [InlineData(200, "not json")]
  public async Task GetOrganizationMembership_RefusedOrUnreadable_ShouldThrowBadGateway(int status, string body)
  {
    _github.Server.Given(Request.Create().WithPath(Membership).UsingGet()).RespondWith(Response.Create().WithStatusCode(status).WithBody(body));

    await Should.ThrowAsync<BadGatewayException>(() => _github.Gateway.GetOrganizationMembershipAsync(Installation, "octo-org", "octocat", Cancellation));
  }

  [Fact]
  public async Task GetOrganizationMembership_ShouldEscapeTheLogins()
  {
    _github.Server.Given(Request.Create().WithPath("/orgs/octo%2Forg/memberships/octo%3Fcat").UsingGet())
           .RespondWith(Response.Create().WithStatusCode(200).WithBody("""{"state":"active","role":"admin"}"""));
    _github.Server.Given(Request.Create().WithPath("/orgs/octo/org/memberships/octo").UsingGet())
           .RespondWith(Response.Create().WithStatusCode(200).WithBody("""{"state":"active","role":"admin"}"""));

    await _github.Gateway.GetOrganizationMembershipAsync(Installation, "octo/org", "octo?cat", Cancellation);

    var request = _github.Server.LogEntries.Select(static e => e.RequestMessage!).Single(static r => r.Path.StartsWith("/orgs/", StringComparison.Ordinal));

    request.AbsoluteUrl.ShouldEndWith("/orgs/octo%2Forg/memberships/octo%3Fcat");
  }

  [Fact]
  public async Task GetOrganizationMembership_InvalidArguments_ShouldThrow()
  {
    await Should.ThrowAsync<ArgumentOutOfRangeException>(() => _github.Gateway.GetOrganizationMembershipAsync(0, "octo-org", "octocat", Cancellation));
    await Should.ThrowAsync<ArgumentException>(() => _github.Gateway.GetOrganizationMembershipAsync(Installation, " ", "octocat", Cancellation));
    await Should.ThrowAsync<ArgumentException>(() => _github.Gateway.GetOrganizationMembershipAsync(Installation, "octo-org", null!, Cancellation));
  }

  private static string Page(int total, int first, int count) =>
    "{\"total_count\":" + total.ToString(CultureInfo.InvariantCulture) + ",\"installations\":["
    + string.Join(
      ',',
      Enumerable.Range(first, count)
                .Select(static id => "{\"id\":" + id.ToString(CultureInfo.InvariantCulture) + ",\"account\":{\"id\":" + (1000 + id).ToString(CultureInfo.InvariantCulture) + ",\"login\":\"org-" + id.ToString(CultureInfo.InvariantCulture) + "\",\"type\":\"Organization\"}}")
    )
    + "]}";

  private void GivenInstallations(int page, string body) =>
    _github.Server.Given(Request.Create().WithPath("/user/installations").WithParam("page", page.ToString(CultureInfo.InvariantCulture)).WithParam("per_page", "100").UsingGet())
           .RespondWith(Response.Create().WithStatusCode(200).WithBody(body));

  #endregion

  #region Implementation of IDisposable

  public void Dispose() => _github.Dispose();

  #endregion
}
