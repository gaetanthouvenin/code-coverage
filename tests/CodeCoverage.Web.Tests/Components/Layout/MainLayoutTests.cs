// ------------------------------------------------------------------------------------------------
// <copyright file="MainLayoutTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Bunit;

using Mediator;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using CodeCoverage.Application.Features.Access.Queries.GetCurrentUserAccess;
using CodeCoverage.Application.Features.Organizations.Queries.ListMyOrganizations;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Users;
using CodeCoverage.Web.Components.Layout;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Layout;

public sealed class MainLayoutTests : UiTestContext
{
  #region Fields

  private readonly ISender _sender = A.Fake<ISender>();

  #endregion

  #region Ctors

  public MainLayoutTests()
  {
    Services.AddFakeSenderRunner(_sender);
  }

  #endregion

  #region Methods

  // Signed in, the bar shows what the user administers, read once through the runner.
  [Fact]
  public void Render_SignedIn_ShouldShowTheNavigationOfTheUser()
  {
    var user = AppUser.Create(Forge.GitHub, "583231", "octocat", null);
    A.CallTo(() => _sender.Send(A<GetCurrentUserAccessQuery>._, A<CancellationToken>._)).Returns(new ValueTask<UserAccess>(new UserAccess(user, true)));
    A.CallTo(() => _sender.Send(A<ListMyOrganizationsQuery>._, A<CancellationToken>._)).Returns(new ValueTask<IReadOnlyList<MemberOrganization>>([]));
    AddAuthorization().SetAuthorized("octocat");

    var cut = Render<MainLayout>(p => p.Add(l => l.Body, Page(() => false)));

    cut.WaitForAssertion(() => cut.Find(".cc-user-menu__name").TextContent.ShouldBe("octocat"));
    cut.FindAll(".cc-topnav__tabs a").Count.ShouldBe(3);
  }

  // Signed out (an error page), nothing is read.
  [Fact]
  public void Render_SignedOut_ShouldReadNothing()
  {
    AddAuthorization();

    var cut = Render<MainLayout>(p => p.Add(l => l.Body, Page(() => false)));

    cut.FindAll(".cc-user-menu").ShouldBeEmpty();
    A.CallTo(_sender).MustNotHaveHappened();
  }

  [Fact]
  public void Render_ShouldShowTheShellWithTheNavigationAndThePage()
  {
    var cut = Render<MainLayout>(p => p.Add(l => l.Body, Page(() => false)));

    cut.Find(".cc-shell .cc-topnav").ShouldNotBeNull();
    cut.Find("main .healthy").TextContent.ShouldBe("healthy");
    cut.FindAll(".cc-topnav__tabs a")
       .Select(a => a.TextContent)
       .ShouldBe([Services.GetRequiredService<IStringLocalizer<Common>>()["Nav_Repositories"].Value]);
  }

  [Fact]
  public void Render_ShouldHostTheToastStack()
  {
    var cut = Render<MainLayout>(p => p.Add(l => l.Body, Page(() => false)));

    cut.FindAll("section.cc-toasts").Count.ShouldBe(1);
  }

  [Fact]
  public void NavigationChange_AfterError_ShouldRecoverBoundary()
  {
    var failing = true;
    var cut = Render<MainLayout>(p => p.Add(l => l.Body, Page(() => failing)));
    cut.FindAll(".cc-error-boundary[role=alert]").Count.ShouldBe(1);

    failing = false;
    Navigation.NavigateTo("other-page");

    cut.WaitForAssertion(() =>
    {
      cut.Find("main .healthy").ShouldNotBeNull();
      cut.FindAll(".cc-error-boundary").ShouldBeEmpty();
    });
  }

  private static RenderFragment Page(Func<bool> shouldFail) => builder =>
  {
    builder.OpenComponent<ThrowingComponent>(0);
    builder.AddComponentParameter(1, nameof(ThrowingComponent.ShouldFail), shouldFail);
    builder.CloseComponent();
  };

  #endregion
}
