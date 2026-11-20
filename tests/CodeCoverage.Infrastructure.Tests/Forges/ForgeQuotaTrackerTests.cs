// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeQuotaTrackerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Organizations;
using CodeCoverage.Infrastructure.Forges;

namespace CodeCoverage.Infrastructure.Tests.Forges;

// The quotas of every forge kept in memory between two passes of the publisher (M3.13a, M7.28).
public sealed class ForgeQuotaTrackerTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  #endregion

  #region Methods

  [Fact]
  public void Record_TwoConnections_ShouldKeepOneQuotaEach()
  {
    var tracker = new ForgeQuotaTracker();

    tracker.Report(Forge.Bitbucket, "connection-1", 900, 1000, Now.AddHours(1), Now);
    tracker.Report(Forge.Bitbucket, "connection-2", 400, 1000, Now.AddHours(1), Now);
    tracker.Report(Forge.Bitbucket, "connection-1", 850, 1000, Now.AddHours(1), Now.AddSeconds(1));

    tracker.ChangedSince(0)
           .Changed
           .Select(static q => (q.Forge, q.Connection, q.Remaining))
           .OrderBy(static q => q.Connection, StringComparer.Ordinal)
           .ShouldBe([(Forge.Bitbucket, "connection-1", 850L), (Forge.Bitbucket, "connection-2", 400L)]);
  }

  // The same key on two forges designates two connections.
  [Fact]
  public void Report_SameConnectionOnTwoForges_ShouldKeepOneQuotaEach()
  {
    var tracker = new ForgeQuotaTracker();

    tracker.Report(Forge.GitHub, "42", 4321, 5000, null, Now);
    tracker.Report(Forge.GitLab, "42", 1900, 2000, null, Now);

    tracker.ChangedSince(0).Changed.Select(static q => q.Forge).Order().ShouldBe([Forge.GitHub, Forge.GitLab]);
  }

  // Two answers of the same connection may end in any order: the older one, ending last, never replaces the newer.
  [Fact]
  public void Report_OlderAfterNewer_ShouldKeepTheNewer()
  {
    var tracker = new ForgeQuotaTracker();

    tracker.Report(Forge.GitHub, "42", 4300, 5000, Now.AddMinutes(30), Now.AddSeconds(2));
    tracker.Report(Forge.GitHub, "42", 4321, 5000, Now.AddMinutes(30), Now);

    var quota = tracker.ChangedSince(0).Changed.ShouldHaveSingleItem();
    (quota.Remaining, quota.ObservedAt).ShouldBe((4300L, Now.AddSeconds(2)));
  }

  // Each report is listed once: by the first call after it, never by a later one.
  [Fact]
  public void ChangedSince_ShouldListEachReportOnce()
  {
    var tracker = new ForgeQuotaTracker();
    tracker.Report(Forge.GitHub, "42", 4321, 5000, null, Now);
    var (first, version) = tracker.ChangedSince(0);

    tracker.Report(Forge.AzureDevOps, "7", 100, null, null, Now);
    var (second, next) = tracker.ChangedSince(version);

    first.Select(static q => q.Connection).ShouldBe(["42"]);
    second.Select(static q => (q.Forge, q.Connection)).ShouldBe([(Forge.AzureDevOps, "7")]);
    tracker.ChangedSince(next).Changed.ShouldBeEmpty();
  }

  [Fact]
  public void Report_EmptyConnection_ShouldThrow()
  {
    var tracker = new ForgeQuotaTracker();

    Should.Throw<ArgumentException>(() => tracker.Report(Forge.GitHub, " ", 1, null, null, Now));
    tracker.ChangedSince(0).Changed.ShouldBeEmpty();
  }

  #endregion
}
