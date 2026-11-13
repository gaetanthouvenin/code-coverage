// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationRepositoriesTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Bunit;

using Mediator;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.GitHubEvents;
using CodeCoverage.Application.Features.GitHubEvents.Commands.SyncOrganizationInstallation;
using CodeCoverage.Application.Features.Organizations;
using CodeCoverage.Application.Features.Repositories.Commands.ActivateRepository;
using CodeCoverage.Application.Features.Repositories.Commands.DeactivateRepository;
using CodeCoverage.Application.Features.Repositories.Queries.ListOrganizationRepositories;
using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Web.Components.Pages;
using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Components.Ui.Services;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Pages;

// /orgs/{forge}/{org}/repositories (M3.39) against a fake sender.
public sealed class OrganizationRepositoriesTests : UiTestContext
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 13, 8, 0, 0, TimeSpan.Zero);

  private readonly ISender _sender = A.Fake<ISender>();

  private readonly List<OrganizationRepository> _repositories = [];

  #endregion

  #region Ctors

  public OrganizationRepositoriesTests()
  {
    Services.AddFakeSenderRunner(_sender);
    Services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));

    _repositories.Add(new OrganizationRepository(Repository(5, "web", disabled: false), Now.AddHours(-2)));
    _repositories.Add(new OrganizationRepository(Repository(6, "api", disabled: true), null));
    A.CallTo(() => _sender.Send(A<ListOrganizationRepositoriesQuery>._, A<CancellationToken>._))
     .ReturnsLazily(() => new ValueTask<IReadOnlyList<OrganizationRepository>>(_repositories));
  }

  #endregion

  #region Methods

  [Fact]
  public void Render_ShouldShowEachRepositoryWithItsStateAndLastUpload()
  {
    var cut = Render<OrganizationRepositories>(Address());

    var rows = cut.FindAll("[data-panel=repositories] tbody tr");
    rows.Count.ShouldBe(2);
    rows[0].TextContent.ShouldContain("web");
    rows[0].TextContent.ShouldContain(L("Repository_Active"));
    rows[1].TextContent.ShouldContain(L("Repository_Inactive"));
    cut.Find("tr[data-repository=api]").QuerySelector("[role=switch]")!.GetAttribute("aria-checked").ShouldBe("false");
  }

  [Fact]
  public void Render_WithoutRepository_ShouldShowTheEmptyState()
  {
    _repositories.Clear();

    var cut = Render<OrganizationRepositories>(Address());

    cut.Find("[data-state=empty]").TextContent.ShouldContain(L("OrganizationRepositories_EmptyTitle"));
  }

  [Fact]
  public void Search_ShouldKeepTheMatchingRepositories()
  {
    var cut = Render<OrganizationRepositories>(Address());

    cut.Find(".cc-search input").Input("ap");

    cut.FindAll("[data-panel=repositories] tbody tr").ShouldHaveSingleItem().TextContent.ShouldContain("api");
  }

  [Fact]
  public void Search_WithoutMatch_ShouldShowTheNoResultState()
  {
    var cut = Render<OrganizationRepositories>(Address());

    cut.Find(".cc-search input").Input("nothing");

    cut.Find("[data-state=noresult]").TextContent.ShouldContain(L("Home_NoMatchTitle"));
  }

  [Fact]
  public void Click_Sync_ShouldEnqueue()
  {
    var cut = Render<OrganizationRepositories>(Address());

    cut.FindAll(".cc-header button").Last().Click();

    A.CallTo(() => _sender.Send(A<SyncOrganizationInstallationCommand>.That.Matches(c => c.Login == "acme-labs"), A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
    Toasts().ShouldContain(t => t.Title == L("OrganizationRepositories_SyncQueued"));
  }

  // Without an installation, the refusal is explicit, and the page shows the message of its code.
  [Fact]
  public void Click_Sync_WithoutInstallation_ShouldShowTheRefusal()
  {
    A.CallTo(() => _sender.Send(A<SyncOrganizationInstallationCommand>._, A<CancellationToken>._))
     .ThrowsAsync(new UnprocessableEntityException(GitHubEventErrors.NoInstallation, "No installation."));
    var cut = Render<OrganizationRepositories>(Address());

    cut.FindAll(".cc-header button").Last().Click();

    var toast = Toasts().ShouldHaveSingleItem();
    toast.Tone.ShouldBe(CcTone.Danger);
    toast.Message.ShouldBe(L("GitHubEvent.NoInstallation"));
  }

  [Fact]
  public void Toggle_ShouldDeactivateThatRepositoryAndReadTheListAgain()
  {
    var cut = Render<OrganizationRepositories>(Address());

    cut.Find("tr[data-repository=web] [role=switch]").Click();

    A.CallTo(() => _sender.Send(A<DeactivateRepositoryCommand>.That.Matches(c => c.Name == "web" && c.Owner == "acme-labs"), A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
    A.CallTo(() => _sender.Send(A<ListOrganizationRepositoriesQuery>._, A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
  }

  [Fact]
  public void Toggle_Disabled_ShouldActivateItAgain()
  {
    var cut = Render<OrganizationRepositories>(Address());

    cut.Find("tr[data-repository=api] [role=switch]").Click();

    A.CallTo(() => _sender.Send(A<ActivateRepositoryCommand>.That.Matches(c => c.Name == "api"), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public void Render_Participant_ShouldBeDenied()
  {
    A.CallTo(() => _sender.Send(A<ListOrganizationRepositoriesQuery>._, A<CancellationToken>._))
     .ThrowsAsync(new ForbiddenException(SecurityErrors.Forbidden, "Forbidden."));

    var cut = Render<OrganizationRepositories>(Address());

    cut.Find("[data-state=denied]").TextContent.ShouldContain(L("OrganizationRepositories_Denied"));
  }

  [Fact]
  public void Render_UnknownOrganization_ShouldShowTheInaccessibleState()
  {
    A.CallTo(() => _sender.Send(A<ListOrganizationRepositoriesQuery>._, A<CancellationToken>._))
     .ThrowsAsync(new NotFoundException(OrganizationErrors.NotFound, "Missing."));

    var cut = Render<OrganizationRepositories>(Address());

    cut.Find("[data-state=missing]").TextContent.ShouldContain(L("Inaccessible_Title"));
  }

  // A late answer for the first organization never shows its repositories on the second.
  [Fact]
  public void Navigate_FirstLoadAnsweringLast_ShouldShowTheSecond()
  {
    var first = new TaskCompletionSource<IReadOnlyList<OrganizationRepository>>();
    A.CallTo(() => _sender.Send(A<ListOrganizationRepositoriesQuery>.That.Matches(q => q.Login == "acme-labs"), A<CancellationToken>._))
     .ReturnsLazily(() => new ValueTask<IReadOnlyList<OrganizationRepository>>(first.Task));
    A.CallTo(() => _sender.Send(A<ListOrganizationRepositoriesQuery>.That.Matches(q => q.Login == "blue-harbor"), A<CancellationToken>._))
     .Returns(new ValueTask<IReadOnlyList<OrganizationRepository>>([new OrganizationRepository(Repository(9, "blue", disabled: false), null)]));
    var cut = Render<OrganizationRepositories>(Address());

    cut.Render(static p => p.Add(static c => c.Login, "blue-harbor"));
    cut.WaitForAssertion(() => cut.Find("[data-panel=repositories] tbody tr").TextContent.ShouldContain("blue"));
    var renders = cut.RenderCount;
    cut.InvokeAsync(() => first.SetResult(_repositories));

    cut.WaitForState(() => cut.RenderCount > renders);
    cut.FindAll("[data-panel=repositories] tbody tr").ShouldHaveSingleItem().TextContent.ShouldContain("blue");
  }

  private static TrackedRepository Repository(long id, string name, bool disabled)
  {
    var repository = TrackedRepository.Create(1, id.ToString(System.Globalization.CultureInfo.InvariantCulture), RepositorySlug.Parse($"acme-labs/{name}"), "main", false, "protected", Now);
    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(repository, id);

    if (disabled)
    {
      repository.Disable(Now);
    }

    return repository;
  }

  private IReadOnlyList<CcToastMessage> Toasts() => Services.GetRequiredService<IToastService>().Toasts;

  private static Action<ComponentParameterCollectionBuilder<OrganizationRepositories>> Address(string forge = "github") =>
    p => p.Add(static c => c.Forge, forge).Add(static c => c.Login, "acme-labs");

  private string L(string key) => Services.GetRequiredService<IStringLocalizer<Common>>()[key];

  #endregion
}
