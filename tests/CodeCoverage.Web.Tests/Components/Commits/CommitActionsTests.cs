// ------------------------------------------------------------------------------------------------
// <copyright file="CommitActionsTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Bunit;

using Mediator;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Commits;
using CodeCoverage.Application.Features.Commits.Commands.ReprocessCommit;
using CodeCoverage.Application.Features.Notifications.Commands.TriggerNotify;
using CodeCoverage.Domain.Git;
using CodeCoverage.Web.Components.Commits;
using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Components.Ui.Services;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Commits;

public sealed class CommitActionsTests : UiTestContext
{
  #region Fields

  private static readonly string Sha = "e4f5a6b" + new string('1', GitSha.Length - 7);

  private readonly ISender _sender = A.Fake<ISender>();

  #endregion

  #region Ctors

  public CommitActionsTests()
  {
    Services.AddFakeSenderRunner(_sender);
  }

  #endregion

  #region Methods

  [Fact]
  public void Render_NonRepoAdmin_ShouldHideActions()
  {
    var cut = Render(canAdminister: false);

    cut.FindAll("button").ShouldBeEmpty();
  }

  [Fact]
  public void Click_Notify_ShouldShowToast()
  {
    var cut = Render(canAdminister: true);

    cut.Find(".cc-action-notify").Click();

    cut.WaitForAssertion(() => Toasts().ShouldContain(t => t.Title == L("Toast_NotifyQueued") && t.Tone == CcTone.Success));
    A.CallTo(() => _sender.Send(A<TriggerNotifyCommand>.That.Matches(static c => c.Sha.Value == Sha && c.Owner == "octo-org"), A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  [Fact]
  public void Click_Reprocess_ShouldShowToastAndReload()
  {
    var reloaded = 0;
    var cut = Render(canAdminister: true, () => reloaded++);

    cut.Find(".cc-action-reprocess").Click();

    cut.WaitForAssertion(() => reloaded.ShouldBe(1));
    Toasts().ShouldContain(t => t.Title == L("Toast_ReprocessQueued") && t.Message == "e4f5a6b");
    A.CallTo(() => _sender.Send(A<ReprocessCommitCommand>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  // A refusal the runner keeps silent for a page (missing) is told by a toast: the page stays where it is.
  [Fact]
  public void Click_RefusedAsMissing_ShouldToastWithoutReload()
  {
    A.CallTo(() => _sender.Send(A<ReprocessCommitCommand>._, A<CancellationToken>._)).Throws(new NotFoundException(CommitErrors.NotFound, "gone"));
    var reloaded = 0;
    var cut = Render(canAdminister: true, () => reloaded++);

    cut.Find(".cc-action-reprocess").Click();

    cut.WaitForAssertion(() => Toasts().ShouldContain(t => t.Tone == CcTone.Danger && t.Message == L("Toast_ActionRefused")));
    reloaded.ShouldBe(0);
  }

  private IRenderedComponent<CommitActions> Render(bool canAdminister, Action? reloaded = null) =>
    Render<CommitActions>(p => p.Add(static c => c.Forge, "github")
                                .Add(static c => c.Owner, "octo-org")
                                .Add(static c => c.Name, "app")
                                .Add(static c => c.Sha, Sha)
                                .Add(static c => c.ShortSha, "e4f5a6b")
                                .Add(static c => c.CanAdminister, canAdminister)
                                .Add(static c => c.OnReprocessed, reloaded ?? (static () => { })));

  private IReadOnlyList<CcToastMessage> Toasts() => Services.GetRequiredService<IToastService>().Toasts;

  private string L(string key, params object[] arguments) => Services.GetRequiredService<IStringLocalizer<Common>>()[key, arguments];

  #endregion
}
