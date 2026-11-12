// ------------------------------------------------------------------------------------------------
// <copyright file="AdminOrganizationsTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Bunit;

using Mediator;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Organizations.Commands.AdmitOrganization;
using CodeCoverage.Application.Features.Organizations.Commands.SuspendOrganization;
using CodeCoverage.Application.Features.Organizations.Queries.ListOrganizations;
using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Web.Components.Pages;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Pages;

// /admin/organizations (M3.41a) against a fake sender.
public sealed class AdminOrganizationsTests : UiTestContext
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  private readonly ISender _sender = A.Fake<ISender>();

  #endregion

  #region Ctors

  public AdminOrganizationsTests()
  {
    Services.AddFakeSenderRunner(_sender);

    var active = Organization.CreatePending(Forge.GitHub, "1001", "zeta-org", OrganizationAccountType.Organization, Now);
    active.Admit("octocat", Now);
    var pending = Organization.CreatePending(Forge.GitHub, "1002", "acme", OrganizationAccountType.User, Now);
    var suspended = Organization.CreatePending(Forge.GitHub, "1003", "blue-harbor", OrganizationAccountType.Organization, Now);
    suspended.Suspend(SuspensionReason.AppUninstalled, Now);

    List(new OrganizationOverview(pending, 0), new OrganizationOverview(suspended, 0), new OrganizationOverview(active, 6));
  }

  #endregion

  #region Methods

  [Fact]
  public void Render_ShouldListTheOrganizationsWithoutTheirData()
  {
    var cut = Render<AdminOrganizations>();

    cut.FindAll("tbody tr").Select(static r => r.GetAttribute("data-login")).ShouldBe(["acme", "blue-harbor", "zeta-org"]);
    var active = Row(cut, "zeta-org");
    active.TextContent.ShouldContain(L("OrganizationState_Active"));
    active.TextContent.ShouldContain("octocat");
    active.QuerySelector(".cc-r.cc-num")!.TextContent.ShouldBe("6");
    Row(cut, "acme").QuerySelector(".cc-r.cc-num")!.TextContent.ShouldBe("—");
    Row(cut, "acme").TextContent.ShouldContain(L("AccountType_User"));
    Row(cut, "blue-harbor").TextContent.ShouldContain(L("SuspensionReason_AppUninstalled"));
    Row(cut, "blue-harbor").QuerySelectorAll("button").ShouldBeEmpty();
    cut.Find(".cc-panel__tools .cc-badge").TextContent.ShouldContain("1");
  }

  [Fact]
  public void Click_Admit_ShouldActivate()
  {
    var cut = Render<AdminOrganizations>();

    Row(cut, "acme").QuerySelector("button")!.Click();

    A.CallTo(() => _sender.Send(A<AdmitOrganizationCommand>.That.Matches(c => c.Forge == Forge.GitHub && c.Login == "acme" && c.ExternalAccountId == "1002"), A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
    A.CallTo(() => _sender.Send(A<ListOrganizationsQuery>._, A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
  }

  [Fact]
  public void Suspend_ShouldAskThenSuspend()
  {
    var cut = Render<AdminOrganizations>();

    Row(cut, "zeta-org").QuerySelector("button")!.Click();
    cut.Find("[role=dialog]").TextContent.ShouldContain(L("AdminOrganizations_SuspendText"));
    cut.FindAll("[role=dialog] .cc-dialog__actions button").Last().Click();

    A.CallTo(() => _sender.Send(A<SuspendOrganizationCommand>.That.Matches(c => c.Login == "zeta-org"), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    cut.FindAll("[role=dialog]").ShouldBeEmpty();
  }

  [Fact]
  public void AdmitAnAccount_ShouldSendWhatTheDialogAsks()
  {
    var cut = Render<AdminOrganizations>();

    cut.FindAll(".cc-header button").Single(b => b.TextContent.Contains(L("Action_AdmitAccount"), StringComparison.Ordinal)).Click();
    var inputs = cut.FindAll("[role=dialog] input");
    inputs[0].Input("northwind-dev");
    cut.FindAll("[role=dialog] input")[1].Input("88120441");
    cut.FindAll("[role=dialog] .cc-dialog__actions button").Last().Click();

    A.CallTo(() => _sender.Send(A<AdmitOrganizationCommand>.That.Matches(c => c.Login == "northwind-dev" && c.ExternalAccountId == "88120441"), A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  [Fact]
  public void Render_NonInstanceAdmin_ShouldBeDenied()
  {
    A.CallTo(() => _sender.Send(A<ListOrganizationsQuery>._, A<CancellationToken>._)).ThrowsAsync(new ForbiddenException(SecurityErrors.Forbidden, "Forbidden."));

    var cut = Render<AdminOrganizations>();

    cut.Find(".cc-status-page").TextContent.ShouldContain(L("AdminOrganizations_Denied"));
    cut.FindAll("table").ShouldBeEmpty();
  }

  [Fact]
  public void Render_NoOrganization_ShouldOfferToAdmitOne()
  {
    List();

    var cut = Render<AdminOrganizations>();

    cut.Find(".cc-status-page").TextContent.ShouldContain(L("AdminOrganizations_EmptyTitle"));
  }

  [Fact]
  public void Search_ShouldKeepTheMatchingLogins()
  {
    var cut = Render<AdminOrganizations>();

    cut.Find(".cc-search input").Input("ZETA");

    cut.FindAll("tbody tr").Select(static r => r.GetAttribute("data-login")).ShouldBe(["zeta-org"]);
  }

  private void List(params OrganizationOverview[] rows) =>
    A.CallTo(() => _sender.Send(A<ListOrganizationsQuery>._, A<CancellationToken>._)).Returns(new ValueTask<IReadOnlyList<OrganizationOverview>>(rows));

  private static AngleSharp.Dom.IElement Row(IRenderedComponent<AdminOrganizations> cut, string login) => cut.Find($"tr[data-login='{login}']");

  private string L(string key) => Services.GetRequiredService<IStringLocalizer<Common>>()[key];

  #endregion
}
