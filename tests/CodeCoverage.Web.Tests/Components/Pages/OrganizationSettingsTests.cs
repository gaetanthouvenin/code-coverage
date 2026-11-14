// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationSettingsTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Bunit;

using Mediator;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Organizations;
using CodeCoverage.Application.Features.Organizations.Commands.ResetOrganizationSetting;
using CodeCoverage.Application.Features.Organizations.Commands.UpdateOrganizationSetting;
using CodeCoverage.Application.Features.Organizations.Queries.GetOrganizationSettings;
using CodeCoverage.Application.Features.Settings.Commands.ResetSetting;
using CodeCoverage.Application.Features.Settings.Commands.UpdateSetting;
using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Settings;
using CodeCoverage.Web.Components.Pages;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Pages;

// /orgs/{forge}/{org}/settings (M3.37a) against a fake sender.
public sealed class OrganizationSettingsTests : UiTestContext
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  private readonly ISender _sender = A.Fake<ISender>();

  #endregion

  #region Ctors

  public OrganizationSettingsTests()
  {
    Services.AddFakeSenderRunner(_sender);

    A.CallTo(() => _sender.Send(A<GetOrganizationSettingsQuery>.That.Matches(q => q.Forge == Forge.GitHub && q.Login == "acme-labs"), A<CancellationToken>._))
     .Returns(new ValueTask<IReadOnlyList<SettingValue>>(
     [
       Value(SettingDefinitions.CoverageOrganizationYaml, "coverage:\n  precision: 1\n", SettingSource.Store),
       Value(SettingDefinitions.NotificationsStatusContextPrefix, null, SettingSource.Default),
       Value(SettingDefinitions.NotificationsStatusesEnabled, null, SettingSource.Default),
       Value(SettingDefinitions.NotificationsCommentsEnabled, "false", SettingSource.Store),
     ]));
  }

  #endregion

  #region Methods

  [Fact]
  public void Render_ShouldShowTheKeysOfTheOrganizationInOnePanel()
  {
    var cut = Render<OrganizationSettings>(Address());

    cut.FindAll("section.cc-panel").ShouldHaveSingleItem();
    cut.FindAll("[data-key]").Count.ShouldBe(4);
    Row(cut, SettingDefinitions.CoverageOrganizationYaml).TextContent.ShouldContain(L("Setting_SourceOrganization"));
    Row(cut, SettingDefinitions.NotificationsStatusContextPrefix).TextContent.ShouldContain(L("Setting_SourceDefault"));
    cut.FindAll("input[type=password]").ShouldBeEmpty();
    cut.Find("[role=tablist], nav").TextContent.ShouldContain(L("Tab_OrgSettings"));
  }

  [Fact]
  public void Save_ShouldCallUpdateForThatOrganization()
  {
    var cut = Render<OrganizationSettings>(Address());

    Row(cut, SettingDefinitions.NotificationsStatusContextPrefix).QuerySelector("input")!.Input("codecov");
    Row(cut, SettingDefinitions.NotificationsStatusContextPrefix).QuerySelector(".cc-setting__btn--save")!.Click();

    A.CallTo(() => _sender.Send(
               A<UpdateOrganizationSettingCommand>.That.Matches(c => c.Forge == Forge.GitHub && c.Login == "acme-labs" && c.Key == SettingDefinitions.NotificationsStatusContextPrefix && c.Value == "codecov"),
               A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
    A.CallTo(() => _sender.Send(A<UpdateSettingCommand>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => _sender.Send(A<GetOrganizationSettingsQuery>._, A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
  }

  [Fact]
  public void Reset_ShouldResetForThatOrganization()
  {
    var cut = Render<OrganizationSettings>(Address());

    Row(cut, SettingDefinitions.CoverageOrganizationYaml).QuerySelectorAll("button").Single(b => b.TextContent == L("Action_Reset")).Click();

    A.CallTo(() => _sender.Send(
               A<ResetOrganizationSettingCommand>.That.Matches(c => c.Login == "acme-labs" && c.Key == SettingDefinitions.CoverageOrganizationYaml),
               A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
    A.CallTo(() => _sender.Send(A<ResetSettingCommand>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public void Render_Participant_ShouldBeDenied()
  {
    A.CallTo(() => _sender.Send(A<GetOrganizationSettingsQuery>._, A<CancellationToken>._)).ThrowsAsync(new ForbiddenException(SecurityErrors.Forbidden, "Forbidden."));

    var cut = Render<OrganizationSettings>(Address());

    cut.Find("[data-state=denied]").TextContent.ShouldContain(L("OrganizationSettings_Denied"));
    cut.FindAll("[data-key]").ShouldBeEmpty();
  }

  // A non-member, an unknown organization and a forge the console does not write all look alike.
  [Theory]
  [InlineData("github", true)]
  [InlineData("gh", false)]
  public void Render_NotFound_ShouldShowInaccessibleState(string forge, bool sent)
  {
    A.CallTo(() => _sender.Send(A<GetOrganizationSettingsQuery>._, A<CancellationToken>._)).ThrowsAsync(new NotFoundException(OrganizationErrors.NotFound, "Missing."));

    var cut = Render<OrganizationSettings>(Address(forge));

    cut.Find("[data-state=missing]").TextContent.ShouldContain(L("Inaccessible_Title"));
    A.CallTo(() => _sender.Send(A<GetOrganizationSettingsQuery>._, A<CancellationToken>._)).MustHaveHappened(sent ? 1 : 0, Times.Exactly);
  }

  // From one organization to another while the second is loading: no editor of the first is left to write to the second.
  [Fact]
  public void Navigate_ToAnotherOrganization_ShouldDropTheEditorsOfTheFirst()
  {
    var pending = new TaskCompletionSource<IReadOnlyList<SettingValue>>();
    A.CallTo(() => _sender.Send(A<GetOrganizationSettingsQuery>.That.Matches(q => q.Login == "blue-harbor"), A<CancellationToken>._))
     .ReturnsLazily(() => new ValueTask<IReadOnlyList<SettingValue>>(pending.Task));
    var cut = Render<OrganizationSettings>(Address());

    cut.Render(static p => p.Add(static c => c.Login, "blue-harbor"));

    cut.FindAll("[data-key]").ShouldBeEmpty();
    cut.FindAll(".cc-spinner, [role=status]").ShouldNotBeEmpty();
  }

  // A late answer for the first organization never replaces the settings of the second.
  [Fact]
  public void Navigate_FirstLoadAnsweringLast_ShouldShowTheSecond()
  {
    var first = new TaskCompletionSource<IReadOnlyList<SettingValue>>();
    A.CallTo(() => _sender.Send(A<GetOrganizationSettingsQuery>.That.Matches(q => q.Login == "acme-labs"), A<CancellationToken>._))
     .ReturnsLazily(() => new ValueTask<IReadOnlyList<SettingValue>>(first.Task));
    A.CallTo(() => _sender.Send(A<GetOrganizationSettingsQuery>.That.Matches(q => q.Login == "blue-harbor"), A<CancellationToken>._))
     .Returns(new ValueTask<IReadOnlyList<SettingValue>>([Value(SettingDefinitions.NotificationsCommentsEnabled, "false", SettingSource.Store)]));
    var cut = Render<OrganizationSettings>(Address());

    cut.Render(static p => p.Add(static c => c.Login, "blue-harbor"));
    cut.WaitForAssertion(() => cut.FindAll("[data-key]").Count.ShouldBe(1));
    var renders = cut.RenderCount;
    cut.InvokeAsync(() => first.SetResult([Value(SettingDefinitions.CoverageOrganizationYaml, null, SettingSource.Default), Value(SettingDefinitions.NotificationsStatusesEnabled, null, SettingSource.Default)]));

    cut.WaitForState(() => cut.RenderCount > renders);
    cut.FindAll("[data-key]").Count.ShouldBe(1);
  }

  // A save refused as not found (organization suspended, membership lost) moves the page to its inaccessible state.
  [Fact]
  public void Save_NotFound_ShouldShowInaccessibleState()
  {
    A.CallTo(() => _sender.Send(A<UpdateOrganizationSettingCommand>._, A<CancellationToken>._)).ThrowsAsync(new NotFoundException(OrganizationErrors.NotFound, "Missing."));
    var cut = Render<OrganizationSettings>(Address());

    Row(cut, SettingDefinitions.NotificationsStatusContextPrefix).QuerySelector("input")!.Input("codecov");
    Row(cut, SettingDefinitions.NotificationsStatusContextPrefix).QuerySelector(".cc-setting__btn--save")!.Click();

    cut.Find("[data-state=missing]").TextContent.ShouldContain(L("Inaccessible_Title"));
    cut.FindAll("[data-key]").ShouldBeEmpty();
  }

  // A refusal answering after the move to another organization never touches the state of the page it no longer shows.
  [Fact]
  public async Task Save_NotFoundAnsweringAfterNavigation_ShouldLeaveTheSecondOrganizationAlone()
  {
    var first = new TaskCompletionSource<Unit>();
    A.CallTo(() => _sender.Send(A<UpdateOrganizationSettingCommand>._, A<CancellationToken>._)).ReturnsLazily(_ => new ValueTask<Unit>(first.Task));
    A.CallTo(() => _sender.Send(A<GetOrganizationSettingsQuery>.That.Matches(q => q.Login == "blue-harbor"), A<CancellationToken>._))
     .Returns(new ValueTask<IReadOnlyList<SettingValue>>([Value(SettingDefinitions.NotificationsCommentsEnabled, "false", SettingSource.Store)]));
    var cut = Render<OrganizationSettings>(Address());

    Row(cut, SettingDefinitions.NotificationsStatusContextPrefix).QuerySelector("input")!.Input("codecov");
    Row(cut, SettingDefinitions.NotificationsStatusContextPrefix).QuerySelector(".cc-setting__btn--save")!.Click();
    cut.Render(static p => p.Add(static c => c.Login, "blue-harbor"));
    cut.WaitForAssertion(() => cut.FindAll("[data-key]").Count.ShouldBe(1));
    await cut.InvokeAsync(() => first.SetException(new NotFoundException(OrganizationErrors.NotFound, "Missing.")));
    await cut.InvokeAsync(static () => { });

    cut.FindAll("[data-state=missing]").ShouldBeEmpty();
    cut.FindAll("[data-key]").Count.ShouldBe(1);
  }

  private static Action<ComponentParameterCollectionBuilder<OrganizationSettings>> Address(string forge = "github") =>
    p => p.Add(static c => c.Forge, forge).Add(static c => c.Login, "acme-labs");

  private static SettingValue Value(string key, string? value, SettingSource source) =>
    SettingValue.Create(SettingDefinitions.Find(key)!, value, source, source is SettingSource.Store ? Now : null, null);

  private static AngleSharp.Dom.IElement Row(IRenderedComponent<OrganizationSettings> cut, string key) => cut.Find($"[data-key='{key.ToLowerInvariant()}']");

  private string L(string key) => Services.GetRequiredService<IStringLocalizer<Common>>()[key];

  #endregion
}
