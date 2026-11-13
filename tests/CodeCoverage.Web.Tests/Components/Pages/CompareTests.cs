// ------------------------------------------------------------------------------------------------
// <copyright file="CompareTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Bunit;

using Mediator;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Commits;
using CodeCoverage.Application.Features.Comparisons.Queries.GetCommitRange;
using CodeCoverage.Application.Features.Repositories.Queries.GetEffectiveConfig;
using CodeCoverage.Domain.Comparisons;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Diffs;
using CodeCoverage.Domain.Git;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.Tests.Support;

using ComparePage = CodeCoverage.Web.Components.Pages.Compare;

namespace CodeCoverage.Web.Tests.Components.Pages;

public sealed class CompareTests : UiTestContext
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 13, 8, 0, 0, TimeSpan.Zero);

  private static readonly GitSha Base = GitSha.Parse("a1b2c3d" + new string('2', GitSha.Length - 7));

  private static readonly GitSha Head = GitSha.Parse("e4f5a6b" + new string('1', GitSha.Length - 7));

  private readonly ISender _sender = A.Fake<ISender>();

  #endregion

  #region Ctors

  public CompareTests()
  {
    Services.AddFakeSenderRunner(_sender);

    A.CallTo(() => _sender.Send(A<GetEffectiveConfigQuery>._, A<CancellationToken>._))
     .Returns(new ValueTask<EffectiveConfig>(new EffectiveConfig(CoverageConfig.Create(ConfigNode.FromMap()), ConfigSource.Builtin, null, null, null, [], [])));
  }

  #endregion

  #region Methods

  // Decision 1: a short SHA or a malformed range is refused before any request, the wrong side named.
  [Theory]
  [InlineData("a1b2c3d...e4f5a6b")]
  [InlineData("not-a-range")]
  public void Render_InvalidRange_ShouldShowValidationError(string range)
  {
    var cut = Render<ComparePage>(Address(range));

    cut.Find("[data-state=invalid]").TextContent.ShouldContain(L("Compare_FullShaExpected"));
    A.CallTo(() => _sender.Send(A<GetCommitRangeQuery>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public void Render_NotComputed_ShouldShowEmptyState()
  {
    Returns(new CommitRange(Base, Head, null));

    var cut = Render<ComparePage>(Address($"{Base.Value}...{Head.Value}"));

    cut.Find("[data-state=notcomputed]").TextContent.ShouldContain(L("Compare_NotComputed"));
    cut.Find(".cc-header__title").TextContent.ShouldContain("a1b2c3d … e4f5a6b");
    cut.FindAll("[data-state=missing]").ShouldBeEmpty();
  }

  // An unknown full SHA is the not-found page, as any other inaccessible resource.
  [Fact]
  public void Render_UnknownCommit_ShouldShowInaccessibleState()
  {
    A.CallTo(() => _sender.Send(A<GetCommitRangeQuery>._, A<CancellationToken>._)).Throws(new NotFoundException(CommitErrors.NotFound, "none"));

    var cut = Render<ComparePage>(Address($"{Base.Value}...{Head.Value}"));

    cut.Find("[data-state=missing]").ShouldNotBeNull();
  }

  // The content of a pull request page, without its comment link nor Re-notify; links to the source carry no pull.
  [Fact]
  public void Render_StoredComparison_ShouldShowItWithoutPull()
  {
    var result = new Comparison(
      new CoverageTotals(9, 1, 0, 0, 0),
      new CoverageTotals(8, 2, 0, 0, 0),
      PatchCoverage.Empty,
      [new ImpactedFile("src/a.cs", null, FileDiffStatus.Modified, new CoverageTotals(1, 1, 0, 0, 0), new CoverageTotals(2, 0, 0, 0, 0), new CoverageTotals(1, 1, 0, 0, 0))],
      [],
      [],
      false);
    var view = new ComparisonView(Head, new CommitComparison(9, null, null, Base, 1, 1, DiffSource.Compare, result, Now), null, false, 3, 3);
    Returns(new CommitRange(Base, Head, view));

    var cut = Render<ComparePage>(Address($"{Base.Value}...{Head.Value}"));

    cut.Find("[data-panel=impacted] a").GetAttribute("href").ShouldBe($"github/octo-org/app/commit/{Head.Value}/blob/src/a.cs");
    cut.FindAll("a[data-link=comment]").ShouldBeEmpty();
    cut.FindAll(".cc-action-renotify").ShouldBeEmpty();
    cut.Find(".cc-pill-tabs__tab--active").TextContent.Trim().ShouldBe(L("Tab_Commits"));
    A.CallTo(() => _sender.Send(A<GetCommitRangeQuery>.That.Matches(static q => q.Base == Base && q.Head == Head), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  // V5: a push comparison counts its hops from the first parent: the page never says « before the merge base ».
  [Fact]
  public void Render_PushComparisonWithHops_ShouldNotMentionTheMergeBase()
  {
    var result = new Comparison(new CoverageTotals(9, 1, 0, 0, 0), new CoverageTotals(8, 2, 0, 0, 0), PatchCoverage.Empty, [], [], [], false);
    var view = new ComparisonView(Head, new CommitComparison(9, null, null, Base, 1, 1, DiffSource.Compare, result, Now, null, 2), null, false, 3, 3);
    Returns(new CommitRange(Base, Head, view));

    var cut = Render<ComparePage>(Address($"{Base.Value}...{Head.Value}"));

    cut.Find("[data-base=compared] code").TextContent.ShouldBe("a1b2c3d");
    cut.FindAll("[data-base=hops]").ShouldBeEmpty();
  }

  // The first range answering after the second never replaces it.
  [Fact]
  public void Render_FirstLoadAnsweringLast_ShouldShowTheSecond()
  {
    var first = new TaskCompletionSource<CommitRange>();
    A.CallTo(() => _sender.Send(A<GetCommitRangeQuery>.That.Matches(static q => q.Head == Head), A<CancellationToken>._))
     .ReturnsLazily(() => new ValueTask<CommitRange>(first.Task));
    A.CallTo(() => _sender.Send(A<GetCommitRangeQuery>.That.Matches(static q => q.Head == Base), A<CancellationToken>._))
     .Returns(new ValueTask<CommitRange>(new CommitRange(Head, Base, null)));
    var cut = Render<ComparePage>(Address($"{Base.Value}...{Head.Value}"));

    cut.Render(p => p.Add(static c => c.Range, $"{Head.Value}...{Base.Value}"));
    cut.WaitForAssertion(() => cut.Find(".cc-header__title").TextContent.ShouldContain("e4f5a6b … a1b2c3d"));
    var renders = cut.RenderCount;
    cut.InvokeAsync(() => first.SetResult(new CommitRange(Base, Head, null)));

    cut.WaitForState(() => cut.RenderCount > renders);
    cut.Find(".cc-header__title").TextContent.ShouldContain("e4f5a6b … a1b2c3d");
  }

  private static Action<ComponentParameterCollectionBuilder<ComparePage>> Address(string range) =>
    p => p.Add(static c => c.Forge, "github").Add(static c => c.Owner, "octo-org").Add(static c => c.Name, "app").Add(static c => c.Range, range);

  private void Returns(CommitRange range) =>
    A.CallTo(() => _sender.Send(A<GetCommitRangeQuery>._, A<CancellationToken>._)).Returns(new ValueTask<CommitRange>(range));

  private string L(string key, params object[] arguments) => Services.GetRequiredService<IStringLocalizer<Common>>()[key, arguments];

  #endregion
}
