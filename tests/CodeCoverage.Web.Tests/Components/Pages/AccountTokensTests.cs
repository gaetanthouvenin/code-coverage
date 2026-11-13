// ------------------------------------------------------------------------------------------------
// <copyright file="AccountTokensTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Bunit;

using Mediator;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using CodeCoverage.Application.Features.Access.Commands.CreateApiToken;
using CodeCoverage.Application.Features.Access.Commands.RevokeApiToken;
using CodeCoverage.Application.Features.Access.Queries.GetCurrentUserAccess;
using CodeCoverage.Application.Features.Access.Queries.ListApiTokens;
using CodeCoverage.Application.Features.Organizations.Queries.ListMyOrganizations;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Tokens;
using CodeCoverage.Domain.Users;
using CodeCoverage.Web.Components.Pages;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Pages;

// /account/tokens (M3.36) against a fake sender.
public sealed class AccountTokensTests : UiTestContext
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 13, 8, 0, 0, TimeSpan.Zero);

  private readonly ISender _sender = A.Fake<ISender>();

  private readonly List<ApiToken> _tokens = [];

  private bool _isInstanceAdministrator;

  #endregion

  #region Ctors

  public AccountTokensTests()
  {
    Services.AddFakeSenderRunner(_sender);

    _tokens.Add(Token(1, "grafana-export", 7));
    A.CallTo(() => _sender.Send(A<ListApiTokensQuery>._, A<CancellationToken>._))
     .ReturnsLazily(() => new ValueTask<IReadOnlyList<ApiToken>>(_tokens));
    A.CallTo(() => _sender.Send(A<ListMyOrganizationsQuery>._, A<CancellationToken>._))
     .Returns(new ValueTask<IReadOnlyList<MemberOrganization>>([new MemberOrganization(Organization(7, "acme-labs"), OrganizationRole.Admin)]));
    A.CallTo(() => _sender.Send(A<GetCurrentUserAccessQuery>._, A<CancellationToken>._))
     .ReturnsLazily(() => new ValueTask<UserAccess>(new UserAccess(User(), _isInstanceAdministrator)));
  }

  #endregion

  #region Methods

  [Fact]
  public void Render_ShouldShowEachTokenWithItsScope()
  {
    var cut = Render<AccountTokens>();

    var row = cut.Find("[data-panel=tokens] tbody tr");
    row.TextContent.ShouldContain("grafana-export");
    row.TextContent.ShouldContain("acme-labs");
  }

  // A token of the instance, which only an administrator of the instance holds, shows that scope.
  [Fact]
  public void Render_InstanceToken_ShouldShowTheInstanceScope()
  {
    _tokens.Add(Token(2, "instance-backup", null));

    var cut = Render<AccountTokens>();

    cut.Find("tr[data-token='2']").TextContent.ShouldContain(L("Nav_Instance"));
  }

  [Fact]
  public void Render_WithoutToken_ShouldShowTheEmptyState()
  {
    _tokens.Clear();

    var cut = Render<AccountTokens>();

    cut.Find("[data-state=empty]").TextContent.ShouldContain(L("ApiTokens_EmptyTitle"));
  }

  // A revoked token can no longer be used: it is hidden until the toggle shows it.
  [Fact]
  public void Render_RevokedToken_ShouldHideItBehindTheToggle()
  {
    var revoked = Token(2, "release-script", 7);
    revoked.Revoke(Now);
    _tokens.Add(revoked);

    var cut = Render<AccountTokens>();

    cut.FindAll("[data-panel=tokens] tbody tr").ShouldHaveSingleItem();
    cut.Find("input[data-toggle=revoked]").Change(true);
    cut.FindAll("[data-panel=tokens] tbody tr").Count.ShouldBe(2);
    cut.Find("tr[data-token='2']").TextContent.ShouldContain(L("Token_Revoked"));
  }

  // The instance scope is offered to the administrators of the instance only.
  [Fact]
  public void Create_AsMember_ShouldOfferTheOrganizationsOnly()
  {
    var cut = Render<AccountTokens>();

    cut.Find(".cc-header button").Click();

    cut.FindAll(".cc-dialog option").Select(static o => o.TextContent).ShouldBe(["acme-labs"]);
  }

  [Fact]
  public void Create_AsInstanceAdministrator_ShouldAlsoOfferTheInstance()
  {
    _isInstanceAdministrator = true;
    var cut = Render<AccountTokens>();

    cut.Find(".cc-header button").Click();

    cut.FindAll(".cc-dialog option").Select(static o => o.TextContent).ShouldBe(["acme-labs", L("Nav_Instance")]);
  }

  [Fact]
  public void Create_ShouldShowValueOnce()
  {
    A.CallTo(() => _sender.Send(A<CreateApiTokenCommand>._, A<CancellationToken>._))
     .Returns(new ValueTask<IssuedApiToken>(new IssuedApiToken(Token(3, "grafana-export", 7), "cvp_abcdef")));
    var cut = Render<AccountTokens>();

    cut.Find(".cc-header button").Click();
    cut.Find(".cc-dialog input").Input("grafana-export");
    cut.FindAll(".cc-dialog__actions button").Last().Click();

    cut.WaitForAssertion(() => cut.Find(".cc-dialog pre.cc-code").TextContent.ShouldBe("cvp_abcdef"));
    A.CallTo(() => _sender.Send(A<CreateApiTokenCommand>.That.Matches(c => c.Organization == "acme-labs" && c.Forge == Forge.GitHub), A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();

    cut.FindAll(".cc-dialog__actions button").Last().Click();

    cut.WaitForAssertion(() => cut.Markup.ShouldNotContain("cvp_abcdef"));
  }

  [Fact]
  public void Revoke_ShouldAskThenRevoke()
  {
    var cut = Render<AccountTokens>();

    cut.Find("[data-panel=tokens] tbody tr button").Click();

    cut.Find(".cc-dialog").TextContent.ShouldContain(L("Token_RevokeText"));
    A.CallTo(() => _sender.Send(A<RevokeApiTokenCommand>._, A<CancellationToken>._)).MustNotHaveHappened();

    cut.FindAll(".cc-dialog__actions button").Last().Click();

    A.CallTo(() => _sender.Send(A<RevokeApiTokenCommand>.That.Matches(c => c.Id == 1), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _sender.Send(A<ListApiTokensQuery>._, A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
  }

  private static AppUser User()
  {
    var user = AppUser.Create(Forge.GitHub, "583231", "octocat", null);
    typeof(AppUser).GetProperty(nameof(AppUser.Id))!.SetValue(user, 42L);

    return user;
  }

  private static Organization Organization(long id, string login)
  {
    var organization = Domain.Organizations.Organization.CreatePending(Forge.GitHub, "1001", login, OrganizationAccountType.Organization, Now);
    organization.Admit("octocat", Now);
    typeof(Organization).GetProperty(nameof(Domain.Organizations.Organization.Id))!.SetValue(organization, id);

    return organization;
  }

  private static ApiToken Token(long id, string name, long? organizationId)
  {
    var token = ApiToken.Create(42, organizationId, name, "cvp_abcd", new byte[32], Now, null);
    typeof(ApiToken).GetProperty(nameof(ApiToken.Id))!.SetValue(token, id);

    return token;
  }

  private string L(string key) => Services.GetRequiredService<IStringLocalizer<Common>>()[key];

  #endregion
}
