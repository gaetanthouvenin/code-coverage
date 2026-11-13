// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryHeaderTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Bunit;

using Mediator;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Repositories;
using CodeCoverage.Application.Features.Repositories.Queries.GetRepositoryPermission;
using CodeCoverage.Web.Components.Repositories;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Repositories;

// The header of the pages of a repository (M3.34): it shows the settings tab only to those who administer it.
public sealed class RepositoryHeaderTests : UiTestContext
{
  #region Fields

  private readonly ISender _sender = A.Fake<ISender>();

  #endregion

  #region Ctors

  public RepositoryHeaderTests()
  {
    Services.AddFakeSenderRunner(_sender);
  }

  #endregion

  #region Methods

  [Fact]
  public void Render_Administrator_ShouldShowTheSettingsTab()
  {
    A.CallTo(() => _sender.Send(A<GetRepositoryPermissionQuery>._, A<CancellationToken>._)).Returns(new ValueTask<bool>(true));

    var cut = Render<RepositoryHeader>(Address());

    cut.WaitForAssertion(() => cut.Find(".cc-pill-tabs").TextContent.ShouldContain(L("Tab_RepoSettings")));
  }

  [Fact]
  public void Render_Reader_ShouldNotShowTheSettingsTab()
  {
    A.CallTo(() => _sender.Send(A<GetRepositoryPermissionQuery>._, A<CancellationToken>._)).Returns(new ValueTask<bool>(false));

    var cut = Render<RepositoryHeader>(Address());

    cut.Find(".cc-pill-tabs").TextContent.ShouldNotContain(L("Tab_RepoSettings"));
    cut.FindAll(".cc-pill-tabs a").Count.ShouldBe(3);
  }

  // A refusal or a failure shows no tab: the header never guesses what the user may do.
  [Fact]
  public void Render_RefusedPermission_ShouldNotShowTheSettingsTab()
  {
    A.CallTo(() => _sender.Send(A<GetRepositoryPermissionQuery>._, A<CancellationToken>._))
     .ThrowsAsync(new NotFoundException(RepositoryErrors.NotFound, "Missing."));

    var cut = Render<RepositoryHeader>(Address());

    cut.Find(".cc-pill-tabs").TextContent.ShouldNotContain(L("Tab_RepoSettings"));
  }

  // A re-render of the same repository asks the permission once, not once per render.
  [Fact]
  public void Render_Again_ShouldAskThePermissionOnce()
  {
    A.CallTo(() => _sender.Send(A<GetRepositoryPermissionQuery>._, A<CancellationToken>._)).Returns(new ValueTask<bool>(true));
    var cut = Render<RepositoryHeader>(Address());

    cut.Render(static p => p.Add(static c => c.Title, "Another title"));

    A.CallTo(() => _sender.Send(A<GetRepositoryPermissionQuery>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  // Another repository is another question: its own answer says what its header shows.
  [Fact]
  public void Render_AnotherRepository_ShouldAskAgain()
  {
    A.CallTo(() => _sender.Send(A<GetRepositoryPermissionQuery>.That.Matches(q => q.Name == "app"), A<CancellationToken>._)).Returns(new ValueTask<bool>(true));
    A.CallTo(() => _sender.Send(A<GetRepositoryPermissionQuery>.That.Matches(q => q.Name == "other"), A<CancellationToken>._)).Returns(new ValueTask<bool>(false));
    var cut = Render<RepositoryHeader>(Address());

    cut.Render(static p => p.Add(static c => c.Name, "other"));

    cut.WaitForAssertion(() => cut.Find(".cc-pill-tabs").TextContent.ShouldNotContain(L("Tab_RepoSettings")));
  }

  private static Action<ComponentParameterCollectionBuilder<RepositoryHeader>> Address() =>
    p => p.Add(static c => c.Forge, "github").Add(static c => c.Owner, "octo-org").Add(static c => c.Name, "app");

  private string L(string key) => Services.GetRequiredService<IStringLocalizer<Common>>()[key];

  #endregion
}
