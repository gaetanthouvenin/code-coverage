// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubQuotaTrackerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Infrastructure.GitHub;

namespace CodeCoverage.Infrastructure.Tests.GitHub;

// The quotas kept in memory between two passes of the publisher (M3.13a).
public sealed class GitHubQuotaTrackerTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  #endregion

  #region Methods

  // Two answers of the same installation may end in any order: the older one, ending last, never replaces the newer.
  [Fact]
  public void Report_OlderAfterNewer_ShouldKeepTheNewer()
  {
    var tracker = new GitHubQuotaTracker();

    tracker.Report("42", 4300, 5000, Now.AddMinutes(30), Now.AddSeconds(2));
    tracker.Report("42", 4321, 5000, Now.AddMinutes(30), Now);

    var quota = tracker.ChangedSince(0).Changed.ShouldHaveSingleItem();
    (quota.Remaining, quota.ObservedAt).ShouldBe((4300L, Now.AddSeconds(2)));
  }

  // Each report is listed once: by the first call after it, never by a later one.
  [Fact]
  public void ChangedSince_ShouldListEachReportOnce()
  {
    var tracker = new GitHubQuotaTracker();
    tracker.Report("42", 4321, 5000, null, Now);
    var (first, version) = tracker.ChangedSince(0);

    tracker.Report("7", 100, 5000, null, Now);
    var (second, next) = tracker.ChangedSince(version);

    first.Select(static q => q.Installation).ShouldBe(["42"]);
    second.Select(static q => q.Installation).ShouldBe(["7"]);
    tracker.ChangedSince(next).Changed.ShouldBeEmpty();
  }

  #endregion
}
