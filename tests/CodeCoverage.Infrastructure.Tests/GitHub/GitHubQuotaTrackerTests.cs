// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubQuotaTrackerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Operations;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Forges;
using CodeCoverage.Infrastructure.GitHub;

namespace CodeCoverage.Infrastructure.Tests.GitHub;

// The rate limit headers of GitHub, handed to the tracker of every forge under the forge GitHub (M7.28).
public sealed class GitHubQuotaTrackerTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  private readonly ForgeQuotaTracker _quotas = new();

  private readonly GitHubQuotaTracker _tracker;

  #endregion

  #region Ctors

  public GitHubQuotaTrackerTests()
  {
    var clock = A.Fake<IClock>();
    A.CallTo(() => clock.UtcNow).Returns(Now);
    _tracker = new GitHubQuotaTracker(_quotas, clock);
  }

  #endregion

  #region Methods

  [Fact]
  public void Report_AllHeaders_ShouldReportTheQuotaUnderGitHub()
  {
    using var response = Response(("x-ratelimit-remaining", "4321"), ("x-ratelimit-limit", "5000"), ("x-ratelimit-reset", "1893456000"));

    var remaining = _tracker.Report("42", response.Headers);

    remaining.ShouldBe(4321);
    var quota = _quotas.ChangedSince(0).Changed.ShouldHaveSingleItem();
    (quota.Forge, quota.Connection, quota.Remaining, quota.Limit).ShouldBe((Forge.GitHub, "42", 4321L, (long?)5000));
    quota.ResetAt.ShouldBe(DateTimeOffset.FromUnixTimeSeconds(1893456000));
    quota.ObservedAt.ShouldBe(Now);
  }

  [Fact]
  public void Report_RemainingOnly_ShouldReportNoLimitNorReset()
  {
    using var response = Response(("x-ratelimit-remaining", "4999"));

    _tracker.Report(ForgeQuota.AppConnection, response.Headers).ShouldBe(4999);

    var quota = _quotas.ChangedSince(0).Changed.ShouldHaveSingleItem();
    (quota.Connection, quota.Limit, quota.ResetAt).ShouldBe((ForgeQuota.AppConnection, (long?)null, (DateTimeOffset?)null));
  }

  [Fact]
  public void Report_WithoutRemaining_ShouldReportNothing()
  {
    using var response = Response(("x-ratelimit-remaining", "many"), ("x-ratelimit-limit", "5000"));

    _tracker.Report("42", response.Headers).ShouldBeNull();

    _quotas.ChangedSince(0).Changed.ShouldBeEmpty();
  }

  [Fact]
  public void Report_NullHeaders_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => _tracker.Report("42", null!));
  }

  [Fact]
  public void ReadNumber_NullHeaders_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => GitHubQuotaTracker.ReadNumber(null!, GitHubQuotaTracker.RemainingHeader));
  }

  private static HttpResponseMessage Response(params (string Name, string Value)[] headers)
  {
    var response = new HttpResponseMessage();

    foreach (var (name, value) in headers)
    {
      response.Headers.TryAddWithoutValidation(name, value);
    }

    return response;
  }

  #endregion
}
