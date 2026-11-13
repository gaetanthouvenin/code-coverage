// ------------------------------------------------------------------------------------------------
// <copyright file="AnnotatedSourceTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Bunit;

using Mediator;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Commits;
using CodeCoverage.Application.Features.Commits.Queries.GetAnnotatedSource;
using CodeCoverage.Application.Features.Repositories.Queries.GetEffectiveConfig;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Source;
using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Components.Ui.Services;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.Tests.Support;

using AnnotatedSourcePage = CodeCoverage.Web.Components.Pages.AnnotatedSource;

namespace CodeCoverage.Web.Tests.Components.Pages;

public sealed class AnnotatedSourceTests : UiTestContext
{
  #region Fields

  private static readonly GitSha Sha = GitSha.Parse("e4f5a6b" + new string('1', GitSha.Length - 7));

  private readonly ISender _sender = A.Fake<ISender>();

  private readonly BunitJSModuleInterop _module;

  #endregion

  #region Ctors

  public AnnotatedSourceTests()
  {
    Services.AddFakeSenderRunner(_sender);
    _module = JSInterop.SetupModule("./js/annotated-source.js");
    _module.SetupVoid("highlight", _ => true).SetVoidResult();

    A.CallTo(() => _sender.Send(A<GetEffectiveConfigQuery>._, A<CancellationToken>._))
     .Returns(new ValueTask<EffectiveConfig>(new EffectiveConfig(CoverageConfig.Create(ConfigNode.FromMap()), ConfigSource.Builtin, null, null, null, [], [])));
  }

  #endregion

  #region Methods

  [Fact]
  public void Render_ShouldShowTheTotalsThenEveryLine()
  {
    Returns(Source());

    var cut = Render<AnnotatedSourcePage>(Address());

    cut.Find(".cc-header__title").TextContent.ShouldContain("src/Alerts/Send.cs");
    cut.FindAll(".cc-panel__stats .cc-mini").Count.ShouldBe(5);
    var rows = cut.FindAll(".cc-source tbody tr");
    rows.Select(static r => r.GetAttribute("data-state")).ShouldBe(["hit", "miss", null, "partial"]);
    rows[0].QuerySelector(".cc-source__hits")!.TextContent.ShouldBe("3");
    rows[3].QuerySelector(".cc-source__hits")!.TextContent.ShouldBe("1/2");
    rows[3].GetAttribute("title").ShouldBe($"{L("Source_Partial")} · 1/2");
    rows[1].QuerySelector(".cc-source__code")!.TextContent.ShouldBe("<script>alert(1)</script>");
    rows[1].QuerySelector("script").ShouldBeNull();
    cut.FindAll(".cc-legend").ShouldBeEmpty();
    cut.Find(".cc-pill-tabs__tab--active").TextContent.Trim().ShouldBe(L("Tab_Commits"));
  }

  // ?pull= marks the lines of the patch, names the pull request and links it.
  [Fact]
  public void Render_PatchLines_ShouldHighlight()
  {
    Returns(Source() with { PatchPull = 423, Lines = [.. Source().Lines.Select(static l => l.Number == 2 ? l with { InPatch = true } : l)] });

    Navigation.NavigateTo($"github/octo-org/app/commit/{Sha.Value}/blob/src/Alerts/Send.cs?pull=423");

    var cut = Render<AnnotatedSourcePage>(Address());

    cut.FindAll(".cc-source tbody tr.cc-source__patch").ShouldHaveSingleItem().GetAttribute("data-line").ShouldBe("2");
    cut.Find(".cc-legend").TextContent.ShouldContain(L("Source_ChangedInPull"));
    cut.Find(".cc-header__subtitle a[data-link=pull]").GetAttribute("href").ShouldBe("github/octo-org/app/pull/423");
    A.CallTo(() => _sender.Send(A<GetAnnotatedSourceQuery>.That.Matches(static q => q.Pull == 423 && q.Path == "src/Alerts/Send.cs"), A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  // The whole file is coloured once, in the browser: the server never writes markup from the forge's text.
  [Fact]
  public void Render_ShouldColourTheFileOnce()
  {
    Returns(Source());

    _module.Setup<int>("nextUncovered", _ => true).SetResult(2);
    var cut = Render<AnnotatedSourcePage>(Address());
    cut.WaitForAssertion(() => _module.VerifyInvoke("highlight").Arguments[1].ShouldBe("src/Alerts/Send.cs"));

    // A render of the same table (here after Next uncovered) colours nothing again.
    cut.Find(".cc-action-next").Click();

    cut.WaitForAssertion(() => _module.VerifyInvoke("nextUncovered"));
    _module.Invocations["highlight"].Count.ShouldBe(1);
  }

  // V1: a flag change reloads the same file behind the spinner; the new table is coloured again, not left plain.
  [Fact]
  public void ChangeFlag_SameFile_ShouldColourAgain()
  {
    Returns(Source());
    var cut = Render<AnnotatedSourcePage>(Address());
    cut.WaitForAssertion(() => _module.Invocations["highlight"].Count.ShouldBe(1));

    Navigation.NavigateTo($"github/octo-org/app/commit/{Sha.Value}/blob/src/Alerts/Send.cs?flag=backend");

    cut.WaitForAssertion(() => _module.Invocations["highlight"].Count.ShouldBe(2));
    A.CallTo(() => _sender.Send(A<GetAnnotatedSourceQuery>.That.Matches(static q => q.Flag == "backend"), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  // V2: the page left while its module was being imported: the module is released and nothing is coloured.
  [Fact]
  public async Task Dispose_DuringImport_ShouldReleaseTheModuleWithoutColouring()
  {
    Returns(Source());
    var import = new TaskCompletionSource<IJSObjectReference>();
    var module = A.Fake<IJSObjectReference>();
    var js = A.Fake<IJSRuntime>();
    A.CallTo(() => js.InvokeAsync<IJSObjectReference>("import", A<object?[]?>._)).Returns(new ValueTask<IJSObjectReference>(import.Task));
    Services.AddSingleton(js);
    var cut = Render<AnnotatedSourcePage>(Address());
    cut.WaitForAssertion(() => A.CallTo(() => js.InvokeAsync<IJSObjectReference>("import", A<object?[]?>._)).MustHaveHappenedOnceExactly());

    await cut.Instance.DisposeAsync();
    await cut.InvokeAsync(() => import.SetResult(module));

    A.CallTo(() => module.DisposeAsync()).MustHaveHappenedOnceExactly();
    A.CallTo(module).Where(static c => c.Method.Name.StartsWith("Invoke", StringComparison.Ordinal)).MustNotHaveHappened();
  }

  [Fact]
  public void NextUncovered_ShouldScroll()
  {
    Returns(Source());
    _module.Setup<int>("nextUncovered", _ => true).SetResult(2);
    var cut = Render<AnnotatedSourcePage>(Address());

    cut.Find(".cc-action-next").Click();

    _module.VerifyInvoke("nextUncovered");
    Toasts().ShouldBeEmpty();
  }

  [Fact]
  public void NextUncovered_None_ShouldToast()
  {
    Returns(Source());
    _module.Setup<int>("nextUncovered", _ => true).SetResult(0);
    var cut = Render<AnnotatedSourcePage>(Address());

    cut.Find(".cc-action-next").Click();

    cut.WaitForAssertion(() => Toasts().ShouldContain(t => t.Title == L("Source_NoUncovered")));
  }

  // The totals remain, and the page leads back to the commit.
  [Fact]
  public void Render_SourceUnavailable_ShouldOfferBackToCommit()
  {
    Returns(Source() with { IsSourceAvailable = false, Lines = [] });

    var cut = Render<AnnotatedSourcePage>(Address());

    cut.FindAll(".cc-panel__stats .cc-mini").Count.ShouldBe(5);
    var unavailable = cut.Find("[data-state=unavailable]");
    unavailable.TextContent.ShouldContain(L("Source_UnavailableTitle"));
    unavailable.QuerySelector("a")!.GetAttribute("href").ShouldBe($"github/octo-org/app/commit/{Sha.Value}");
    cut.FindAll(".cc-source").ShouldBeEmpty();
    cut.FindAll(".cc-action-next").ShouldBeEmpty();
    _module.Invocations["highlight"].ShouldBeEmpty();
  }

  // A compacted commit says so, without the toast of a refused request.
  [Fact]
  public void Render_Compacted_ShouldShowItsState()
  {
    A.CallTo(() => _sender.Send(A<GetAnnotatedSourceQuery>._, A<CancellationToken>._)).Throws(new UnprocessableEntityException(CommitErrors.Compacted, "compacted"));

    var cut = Render<AnnotatedSourcePage>(Address());

    cut.Find("[data-state=compacted]").TextContent.ShouldContain(L("Commit_CompactedHint"));
    Toasts().ShouldBeEmpty();
  }

  [Fact]
  public void Render_FileNotInReport_ShouldShowInaccessibleState()
  {
    A.CallTo(() => _sender.Send(A<GetAnnotatedSourceQuery>._, A<CancellationToken>._)).Throws(new NotFoundException(CommitErrors.FileNotFound, "none"));

    var cut = Render<AnnotatedSourcePage>(Address());

    cut.Find("[data-state=missing]").ShouldNotBeNull();
  }

  // A short SHA is refused before any send, as on the commit page.
  [Fact]
  public void Render_ShortSha_ShouldShowInaccessibleStateWithoutReading()
  {
    var cut = Render<AnnotatedSourcePage>(p => p.Add(static c => c.Forge, "github").Add(static c => c.Owner, "octo-org").Add(static c => c.Name, "app").Add(static c => c.Sha, "e4f5a6b").Add(static c => c.Path, "src/A.cs"));

    cut.Find("[data-state=missing]").ShouldNotBeNull();
    A.CallTo(() => _sender.Send(A<GetAnnotatedSourceQuery>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  // The first file answering after the second never replaces it.
  [Fact]
  public void Render_FirstLoadAnsweringLast_ShouldShowTheSecond()
  {
    var first = new TaskCompletionSource<AnnotatedSource>();
    A.CallTo(() => _sender.Send(A<GetAnnotatedSourceQuery>.That.Matches(static q => q.Path == "src/Alerts/Send.cs"), A<CancellationToken>._))
     .ReturnsLazily(() => new ValueTask<AnnotatedSource>(first.Task));
    A.CallTo(() => _sender.Send(A<GetAnnotatedSourceQuery>.That.Matches(static q => q.Path == "src/Other.cs"), A<CancellationToken>._))
     .Returns(new ValueTask<AnnotatedSource>(Source() with { Path = "src/Other.cs" }));
    var cut = Render<AnnotatedSourcePage>(Address());

    cut.Render(static p => p.Add(static c => c.Path, "src/Other.cs"));
    cut.WaitForAssertion(() => cut.Find(".cc-source table").GetAttribute("aria-label").ShouldBe("src/Other.cs"));
    var renders = cut.RenderCount;
    cut.InvokeAsync(() => first.SetResult(Source()));

    cut.WaitForState(() => cut.RenderCount > renders);
    cut.Find(".cc-source table").GetAttribute("aria-label").ShouldBe("src/Other.cs");
  }

  private static AnnotatedSource Source() =>
    new(
      "src/Alerts/Send.cs",
      Sha,
      null,
      new CoverageTotals(2, 1, 1, 0, 0),
      true,
      [
        new AnnotatedLine(1, "covered();", LineState.Hit, 3, 0, 0),
        new AnnotatedLine(2, "<script>alert(1)</script>", LineState.Miss, 0, 0, 0),
        new AnnotatedLine(3, "// comment", null, null, 0, 0),
        new AnnotatedLine(4, "if (a) {}", LineState.Partial, 1, 1, 2),
      ],
      null,
      ["backend"]);

  private static Action<ComponentParameterCollectionBuilder<AnnotatedSourcePage>> Address() =>
    p => p.Add(static c => c.Forge, "github")
          .Add(static c => c.Owner, "octo-org")
          .Add(static c => c.Name, "app")
          .Add(static c => c.Sha, Sha.Value)
          .Add(static c => c.Path, "src/Alerts/Send.cs");

  private void Returns(AnnotatedSource source) =>
    A.CallTo(() => _sender.Send(A<GetAnnotatedSourceQuery>._, A<CancellationToken>._)).Returns(new ValueTask<AnnotatedSource>(source));

  private IReadOnlyList<CcToastMessage> Toasts() => Services.GetRequiredService<IToastService>().Toasts;

  private string L(string key, params object[] arguments) => Services.GetRequiredService<IStringLocalizer<Common>>()[key, arguments];

  #endregion
}
