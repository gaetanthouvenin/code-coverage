// ------------------------------------------------------------------------------------------------
// <copyright file="AdminAuditTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Bunit;

using Mediator;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Time.Testing;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Operations.Queries.ListAuditEntries;
using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Audit;
using CodeCoverage.Web.Components.Pages;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Pages;

// /admin/audit (M3.41) against a fake sender.
public sealed class AdminAuditTests : UiTestContext
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  private readonly ISender _sender = A.Fake<ISender>();

  private readonly List<ListAuditEntriesQuery> _queries = [];

  private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 12, 8, 0, 0, TimeSpan.Zero));

  private readonly List<AuditEntrySummary> _entries;

  #endregion

  #region Ctors

  public AdminAuditTests()
  {
    Services.AddFakeSenderRunner(_sender);
    Services.AddSingleton<TimeProvider>(_time);

    _entries =
    [
      new AuditEntrySummary(3, Now, "sam-ops", "Settings.Reset", "GitHub:WebhookSecret", AuditCategory.Secrets),
      new AuditEntrySummary(2, Now.AddMinutes(-5), "alex-ops", "Organization.Admit", "GitHub/acme-labs", AuditCategory.Organizations),
      new AuditEntrySummary(1, Now.AddMinutes(-9), "[system]", "Repositories.Activate", "7", null),
    ];
    A.CallTo(() => _sender.Send(A<ListAuditEntriesQuery>._, A<CancellationToken>._))
     .ReturnsLazily(call =>
      {
        var query = call.GetArgument<ListAuditEntriesQuery>(0)!;
        _queries.Add(query);

        return new ValueTask<AuditEntryPage>(new AuditEntryPage(_entries, query.Page, query.PageSize, 60));
      });
  }

  #endregion

  #region Methods

  [Fact]
  public void Render_ShouldListTheEntriesWithTheirLabel()
  {
    var cut = Render<AdminAudit>();

    _queries.ShouldHaveSingleItem().Category.ShouldBeNull();
    var rows = cut.FindAll("tbody tr");
    rows[0].TextContent.ShouldContain(L("AuditAction_Settings_ClearSecret"));
    rows[0].TextContent.ShouldContain("GitHub:WebhookSecret");
    rows[1].TextContent.ShouldContain(L("AuditAction_Organization_Admit"));
    rows[1].TextContent.ShouldContain("alex-ops");
    rows[2].QuerySelector(".cc-badge")!.TextContent.Trim().ShouldBe("Repositories.Activate");
    cut.Find(".cc-panel__foot span").TextContent.ShouldContain("60");
  }

  [Fact]
  public void Filter_ShouldReload()
  {
    var cut = Render<AdminAudit>();

    cut.Find("select").Change("Secrets");

    _queries.Count.ShouldBe(2);
    _queries[^1].Category.ShouldBe(AuditCategory.Secrets);
    _queries[^1].Page.ShouldBe(1);
  }

  [Fact]
  public async Task Search_ShouldReloadFromTheFirstPage()
  {
    var cut = Render<AdminAudit>();
    cut.FindAll(".cc-panel__foot button")[1].Click();
    _queries[^1].Page.ShouldBe(2);

    await TypeAsync(cut, "octo");
    _time.Advance(TimeSpan.FromMilliseconds(300));

    cut.WaitForAssertion(() => _queries[^1].Search.ShouldBe("octo"));
    _queries[^1].Page.ShouldBe(1);
  }

  // The search waits for the typing to pause: one request for a word, not one per key.
  [Fact]
  public async Task Search_WhileTyping_ShouldWaitForAPause()
  {
    var cut = Render<AdminAudit>();

    foreach (var text in new[] { "o", "oc", "oct", "octo" })
    {
      await TypeAsync(cut, text);
      _time.Advance(TimeSpan.FromMilliseconds(100));
    }

    _queries.ShouldHaveSingleItem();
    _time.Advance(TimeSpan.FromMilliseconds(199));
    _queries.ShouldHaveSingleItem();
    _time.Advance(TimeSpan.FromMilliseconds(1));

    cut.WaitForAssertion(() => _queries.Count.ShouldBe(2));
    _queries[^1].Search.ShouldBe("octo");
  }

  // A search typed just before leaving the page never sends its request.
  [Fact]
  public async Task Search_LeavingDuringThePause_ShouldSendNothing()
  {
    var cut = Render<AdminAudit>();

    await TypeAsync(cut, "octo");
    await cut.InvokeAsync(Renderer.DisposeComponents);
    _time.Advance(TimeSpan.FromMilliseconds(300));

    _queries.ShouldHaveSingleItem();
  }

  // A page past the last one (the log shrank since) shows the last real page, with its pager.
  [Fact]
  public void ChangePage_PastTheLast_ShouldShowTheLastPage()
  {
    A.CallTo(() => _sender.Send(A<ListAuditEntriesQuery>._, A<CancellationToken>._))
     .ReturnsLazily(call =>
      {
        var query = call.GetArgument<ListAuditEntriesQuery>(0)!;
        _queries.Add(query);

        return new ValueTask<AuditEntryPage>(new AuditEntryPage(query.Page <= 2 ? _entries : [], query.Page, query.PageSize, query.Page <= 2 ? 60 : 50));
      });
    var cut = Render<AdminAudit>();
    cut.FindAll(".cc-panel__foot button")[1].Click();

    cut.FindAll(".cc-panel__foot button")[1].Click();

    cut.WaitForAssertion(() => _queries[^1].Page.ShouldBe(2));
    cut.FindAll("tbody tr").Count.ShouldBe(3);
    cut.FindAll(".cc-panel__foot").ShouldHaveSingleItem();
  }

  // A change of family while the first load is pending: the late answer of the first is dropped.
  [Fact]
  public void Filter_FirstLoadAnsweringLast_ShouldShowTheSecond()
  {
    var first = new TaskCompletionSource<AuditEntryPage>();
    A.CallTo(() => _sender.Send(A<ListAuditEntriesQuery>.That.Matches(q => q.Category == null), A<CancellationToken>._))
     .ReturnsLazily(() => new ValueTask<AuditEntryPage>(first.Task));
    var cut = Render<AdminAudit>();

    cut.Find("select").Change("Secrets");
    cut.WaitForAssertion(() => cut.FindAll("tbody tr").Count.ShouldBe(3));
    var renders = cut.RenderCount;
    cut.InvokeAsync(() => first.SetResult(new AuditEntryPage([], 1, 25, 0)));

    cut.WaitForState(() => cut.RenderCount > renders);
    cut.FindAll("tbody tr").Count.ShouldBe(3);
  }

  [Fact]
  public void Render_NonInstanceAdmin_ShouldBeDenied()
  {
    A.CallTo(() => _sender.Send(A<ListAuditEntriesQuery>._, A<CancellationToken>._)).ThrowsAsync(new ForbiddenException(SecurityErrors.Forbidden, "Forbidden."));

    var cut = Render<AdminAudit>();

    cut.Find("[data-state=denied]").TextContent.ShouldContain(L("AdminAudit_Denied"));
    cut.FindAll("table").ShouldBeEmpty();
  }

  private string L(string key) => Services.GetRequiredService<IStringLocalizer<Common>>()[key];

  // Types in the search box and waits until the renderer has run the key, so its pause is timed from the current fake
  // time. When an earlier pause ends during the previous Advance, its continuation holds the renderer dispatcher on
  // another thread: the key is then queued behind it, Input returns before the key starts its delay, and an Advance
  // made at once would move the clock before that delay exists. The dispatcher runs its work in order, so an empty
  // InvokeAsync completes only once the key has started its delay.
  private static async Task TypeAsync(IRenderedComponent<AdminAudit> cut, string text)
  {
    cut.Find(".cc-search input").Input(text);
    await cut.InvokeAsync(static () => { });
  }

  #endregion
}
