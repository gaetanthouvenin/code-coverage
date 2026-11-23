// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeQuotaTrackerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Organizations;
using CodeCoverage.Infrastructure.Diagnostics;
using CodeCoverage.Infrastructure.Forges;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.Forges;

// The quotas of every forge kept in memory between two passes of the publisher (M3.13a, M7.28), and published by the
// quota gauge of every forge (M7.29).
public sealed class ForgeQuotaTrackerTests : IDisposable
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  private readonly TestMeterFactory _factory = new();

  private readonly MeasurementRecorder _recorder;

  private readonly ForgeQuotaTracker _tracker;

  #endregion

  #region Ctors

  public ForgeQuotaTrackerTests()
  {
    _tracker = new ForgeQuotaTracker(new CodeCoverageMeter(_factory));
    _recorder = new MeasurementRecorder(_factory);
  }

  #endregion

  #region Methods

  [Fact]
  public void Record_TwoConnections_ShouldKeepOneQuotaEach()
  {
    _tracker.Report(Forge.Bitbucket, "connection-1", 900, 1000, Now.AddHours(1), Now);
    _tracker.Report(Forge.Bitbucket, "connection-2", 400, 1000, Now.AddHours(1), Now);
    _tracker.Report(Forge.Bitbucket, "connection-1", 850, 1000, Now.AddHours(1), Now.AddSeconds(1));

    _tracker.ChangedSince(0)
            .Changed
            .Select(static q => (q.Forge, q.Connection, q.Remaining))
            .OrderBy(static q => q.Connection, StringComparer.Ordinal)
            .ShouldBe([(Forge.Bitbucket, "connection-1", 850L), (Forge.Bitbucket, "connection-2", 400L)]);
  }

  // The same key on two forges designates two connections.
  [Fact]
  public void Report_SameConnectionOnTwoForges_ShouldKeepOneQuotaEach()
  {
    _tracker.Report(Forge.GitHub, "42", 4321, 5000, null, Now);
    _tracker.Report(Forge.GitLab, "42", 1900, 2000, null, Now);

    _tracker.ChangedSince(0).Changed.Select(static q => q.Forge).Order().ShouldBe([Forge.GitHub, Forge.GitLab]);
  }

  // Two answers of the same connection may end in any order: the older one, ending last, never replaces the newer,
  // neither in memory nor on the gauge.
  [Fact]
  public void Report_OlderAfterNewer_ShouldKeepTheNewer()
  {
    _tracker.Report(Forge.GitHub, "42", 4300, 5000, Now.AddMinutes(30), Now.AddSeconds(2));
    _tracker.Report(Forge.GitHub, "42", 4321, 5000, Now.AddMinutes(30), Now);

    var quota = _tracker.ChangedSince(0).Changed.ShouldHaveSingleItem();
    (quota.Remaining, quota.ObservedAt).ShouldBe((4300L, Now.AddSeconds(2)));
    _recorder.Collect();
    _recorder.Of(CodeCoverageMeter.ForgeRateLimitRemaining).ShouldHaveSingleItem().Value.ShouldBe(4300);
  }

  // M7.29: every forge reporting here has its series of the quota gauge, under its segment and its connection.
  [Fact]
  public void Report_ShouldPublishTheQuotaLeftOfEachConnection()
  {
    _tracker.Report(Forge.GitHub, "42", 4321, 5000, null, Now);
    _tracker.Report(Forge.AzureDevOps, "azure-devops-1", 120, null, null, Now);

    _recorder.Collect();

    var remaining = _recorder.Of(CodeCoverageMeter.ForgeRateLimitRemaining);
    remaining.Count.ShouldBe(2);
    remaining.Single(m => Equals(m.Tags["forge"], "github") && Equals(m.Tags["connection"], "42")).Value.ShouldBe(4321);
    remaining.Single(m => Equals(m.Tags["forge"], "azure-devops") && Equals(m.Tags["connection"], "azure-devops-1")).Value.ShouldBe(120);
  }

  // Each report is listed once: by the first call after it, never by a later one.
  [Fact]
  public void ChangedSince_ShouldListEachReportOnce()
  {
    _tracker.Report(Forge.GitHub, "42", 4321, 5000, null, Now);
    var (first, version) = _tracker.ChangedSince(0);

    _tracker.Report(Forge.AzureDevOps, "7", 100, null, null, Now);
    var (second, next) = _tracker.ChangedSince(version);

    first.Select(static q => q.Connection).ShouldBe(["42"]);
    second.Select(static q => (q.Forge, q.Connection)).ShouldBe([(Forge.AzureDevOps, "7")]);
    _tracker.ChangedSince(next).Changed.ShouldBeEmpty();
  }

  [Fact]
  public void Report_EmptyConnection_ShouldThrow()
  {
    Should.Throw<ArgumentException>(() => _tracker.Report(Forge.GitHub, " ", 1, null, null, Now));
    _tracker.ChangedSince(0).Changed.ShouldBeEmpty();
    _recorder.Collect();
    _recorder.Of(CodeCoverageMeter.ForgeRateLimitRemaining).ShouldBeEmpty();
  }

  [Fact]
  public void Ctor_NullMeter_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => new ForgeQuotaTracker(null!));
  }

  #endregion

  #region Implementation of IDisposable

  public void Dispose()
  {
    _recorder.Dispose();
    _factory.Dispose();
  }

  #endregion
}
