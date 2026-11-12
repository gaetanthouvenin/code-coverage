// ------------------------------------------------------------------------------------------------
// <copyright file="AdminOperationsTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Bunit;

using Mediator;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Operations.Commands.RetryJob;
using CodeCoverage.Application.Features.Operations.Queries.GetSystemStatus;
using CodeCoverage.Application.Features.Operations.Queries.ListJobs;
using CodeCoverage.Application.Features.Operations.Queries.ListWebhookDeliveries;
using CodeCoverage.Application.Features.Organizations.Queries.ListOrganizations;
using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Operations;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Web.Components.Pages;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Pages;

// /admin/operations (M3.40) against a fake sender.
public sealed class AdminOperationsTests : UiTestContext
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  private readonly ISender _sender = A.Fake<ISender>();

  #endregion

  #region Ctors

  public AdminOperationsTests()
  {
    Services.AddFakeSenderRunner(_sender);
    Services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));

    var organization = Organization.CreatePending(Forge.GitHub, "1001", "acme-labs", OrganizationAccountType.Organization, Now);
    organization.Admit("octocat", Now);
    typeof(Organization).GetProperty(nameof(Organization.Id))!.SetValue(organization, 7L);

    A.CallTo(() => _sender.Send(A<GetSystemStatusQuery>._, A<CancellationToken>._))
     .Returns(new ValueTask<SystemStatus>(Status(6, GitHubQuota.Create("42", 11204, 15000, Now.AddMinutes(38), Now), GitHubQuota.Create(GitHubQuota.AppInstallation, 4999, 5000, null, Now))));
    A.CallTo(() => _sender.Send(A<ListJobsQuery>.That.Matches(q => q.State == JobState.Dead), A<CancellationToken>._))
     .Returns(new ValueTask<JobPage>(new JobPage([Job(12, "ProcessUpload", 7, JobState.Dead)], 1, 25, 1)));
    A.CallTo(() => _sender.Send(A<ListJobsQuery>.That.Matches(q => q.State == JobState.Failed), A<CancellationToken>._))
     .Returns(new ValueTask<JobPage>(new JobPage([Job(11, "PurgeExpiredData", null, JobState.Failed)], 1, 25, 1)));
    A.CallTo(() => _sender.Send(A<ListWebhookDeliveriesQuery>._, A<CancellationToken>._))
     .Returns(new ValueTask<IReadOnlyList<WebhookDeliverySummary>>(
     [
       Delivery("d3", "push", Now.AddMinutes(-12), processed: true, error: false),
       Delivery("d2", "push", Now.AddDays(-1), processed: true, error: true),
       Delivery("d1", "installation", Now.AddDays(-4), processed: false, error: false),
     ]));
    A.CallTo(() => _sender.Send(A<ListOrganizationsQuery>._, A<CancellationToken>._))
     .Returns(new ValueTask<IReadOnlyList<OrganizationOverview>>([new OrganizationOverview(organization, 1)]));
  }

  #endregion

  #region Methods

  [Fact]
  public void Render_ShouldShowTheMetadataOfTheOperations()
  {
    var cut = Render<AdminOperations>();

    var cards = cut.FindAll(".cc-kpi");
    cards[0].QuerySelector(".cc-kpi__value")!.TextContent.ShouldBe("6");
    cards[3].QuerySelector(".cc-kpi__value")!.TextContent.ShouldBe("1 / 2");
    cut.FindAll("[data-panel=jobs] tbody tr").Select(static r => r.GetAttribute("data-job")).ShouldBe(["12", "11"]);
    cut.Find("tr[data-job='12']").TextContent.ShouldContain("acme-labs");
    cut.Find("tr[data-job='11']").TextContent.ShouldContain(L("Nav_Instance"));
    cut.FindAll("[data-panel=webhooks] tbody tr").Select(static r => r.GetAttribute("data-event")).ShouldBe(["push", "installation"]);
    cut.Find("tr[data-event=push]").TextContent.ShouldContain(L("Status_Success"));
    cut.Find("[data-panel=quota] svg.cc-gauge").GetAttribute("aria-label").ShouldStartWith(L("Operations_RemainingRequests"));
    cut.FindAll("[data-panel=workers] .cc-thumb--warn").ShouldHaveSingleItem();
  }

  [Fact]
  public void Click_RetryDeadJob_ShouldEnqueue()
  {
    var cut = Render<AdminOperations>();

    cut.Find("tr[data-job='12'] button").Click();

    A.CallTo(() => _sender.Send(A<RetryJobCommand>.That.Matches(c => c.Id == 12), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _sender.Send(A<GetSystemStatusQuery>._, A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
  }

  [Fact]
  public void Render_NonInstanceAdmin_ShouldBeDenied()
  {
    A.CallTo(() => _sender.Send(A<GetSystemStatusQuery>._, A<CancellationToken>._)).ThrowsAsync(new ForbiddenException(SecurityErrors.Forbidden, "Forbidden."));

    var cut = Render<AdminOperations>();

    cut.Find("[data-state=denied]").TextContent.ShouldContain(L("AdminOperations_Denied"));
  }

  // The reset of the quota is told as the time left, in the words of the culture, not as a time of the server.
  [Fact]
  public void Render_ShouldTellWhenTheQuotaResets()
  {
    var previous = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
    CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");

    try
    {
      var cut = Render<AdminOperations>();

      var footer = cut.FindAll(".cc-kpi")[2].QuerySelector(".cc-kpi__footer")!;
      footer.TextContent.ShouldContain(L("Operations_ResetsIn"));
      footer.QuerySelector("b")!.TextContent.ShouldBe("38 min");
    }
    finally
    {
      (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = previous;
    }
  }

  // A quota whose window already started again is refilled since: it never drives the gauge as the most consumed.
  [Fact]
  public void Render_ExpiredQuota_ShouldNotDriveTheGauge()
  {
    A.CallTo(() => _sender.Send(A<GetSystemStatusQuery>._, A<CancellationToken>._))
     .Returns(new ValueTask<SystemStatus>(Status(6, GitHubQuota.Create("42", 100, 15000, Now.AddMinutes(-5), Now.AddHours(-1)), GitHubQuota.Create("7", 9000, 15000, Now.AddMinutes(30), Now))));

    var cut = Render<AdminOperations>();

    cut.Find("[data-panel=quota] svg.cc-gauge").GetAttribute("aria-label")!.ShouldContain("60");
    cut.FindAll(".cc-kpi")[2].QuerySelector(".cc-kpi__value")!.TextContent.ShouldBe(9000.ToString("N0", CultureInfo.CurrentCulture));
  }

  // A second refresh while the first is pending: the late answer of the first is dropped.
  [Fact]
  public void Refresh_FirstLoadAnsweringLast_ShouldShowTheSecond()
  {
    var calls = 0;
    var first = new TaskCompletionSource<SystemStatus>();
    A.CallTo(() => _sender.Send(A<GetSystemStatusQuery>._, A<CancellationToken>._))
     .ReturnsLazily(() => ++calls switch
      {
        2 => new ValueTask<SystemStatus>(first.Task),
        3 => new ValueTask<SystemStatus>(Status(9)),
        var _ => new ValueTask<SystemStatus>(Status(6)),
      });
    var cut = Render<AdminOperations>();

    cut.Find(".cc-header button").Click();
    cut.Find(".cc-header button").Click();
    cut.WaitForAssertion(() => cut.FindAll(".cc-kpi")[0].QuerySelector(".cc-kpi__value")!.TextContent.ShouldBe("9"));
    var renders = cut.RenderCount;
    cut.InvokeAsync(() => first.SetResult(Status(2)));

    cut.WaitForState(() => cut.RenderCount > renders);
    cut.FindAll(".cc-kpi")[0].QuerySelector(".cc-kpi__value")!.TextContent.ShouldBe("9");
  }

  private static SystemStatus Status(int queued, params GitHubQuota[] quotas) =>
    new(
      queued,
      1,
      1,
      1,
      TimeSpan.FromSeconds(14),
      Now.AddSeconds(-2),
      quotas.ToDictionary(static q => q.Installation, static q => q.Remaining),
      [WorkerHeartbeat.Create("api-1:42", Now.AddHours(-1), Now.AddSeconds(-2)), WorkerHeartbeat.Create("api-2:7", Now.AddHours(-3), Now.AddMinutes(-10))],
      quotas);

  private static JobSummary Job(long id, string type, long? organizationId, JobState state) =>
    new()
    {
      Id = id,
      OrganizationId = organizationId,
      Type = type,
      State = state,
      Attempts = 8,
      MaxAttempts = 8,
      RunAfter = Now,
      FirstQueuedAt = Now.AddHours(-3),
      HasError = true,
      CorrelationId = "c",
      CreatedAt = Now.AddHours(-3),
      CompletedAt = Now.AddHours(-2),
    };

  private static WebhookDeliverySummary Delivery(string id, string @event, DateTimeOffset at, bool processed, bool error) =>
    new(id, @event, null, null, null, null, at, processed ? at : null, error);

  private string L(string key) => Services.GetRequiredService<IStringLocalizer<Common>>()[key];

  #endregion
}
