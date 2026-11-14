// ------------------------------------------------------------------------------------------------
// <copyright file="CodeCoverageMeterTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Infrastructure.Diagnostics;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.Diagnostics;

// The instruments of the meter, observed through a MeterListener scoped to the meter factory of each test.
public sealed class CodeCoverageMeterTests : IDisposable
{
  #region Fields

  private readonly TestMeterFactory _factory = new();

  private readonly CodeCoverageMeter _meter;

  private readonly MeasurementRecorder _recorder;

  #endregion

  #region Ctors

  public CodeCoverageMeterTests()
  {
    _meter = new CodeCoverageMeter(_factory);
    _recorder = new MeasurementRecorder(_factory);
  }

  #endregion

  #region Methods

  [Fact]
  public void JobDead_ShouldIncrementCounter()
  {
    _meter.JobDead();
    _meter.JobDead();

    _recorder.Of(CodeCoverageMeter.JobsDeadTotal).Count.ShouldBe(2);
    _recorder.Sum(CodeCoverageMeter.JobsDeadTotal).ShouldBe(2);
  }

  [Fact]
  public void ReportJobQueue_ShouldPublishTheJobGauges()
  {
    _meter.ReportJobQueue(
      new JobQueueSnapshot(new Dictionary<string, long>(StringComparer.Ordinal) { ["FinalizeCommit"] = 3, ["NotifyCommit"] = 1 }, 42.5)
    );

    _recorder.Collect();

    var queued = _recorder.Of(CodeCoverageMeter.JobsQueued);
    queued.Count.ShouldBe(2);
    queued.Single(m => Equals(m.Tags["type"], "FinalizeCommit")).Value.ShouldBe(3);
    queued.Single(m => Equals(m.Tags["type"], "NotifyCommit")).Value.ShouldBe(1);
    _recorder.Of(CodeCoverageMeter.JobOldestQueuedAgeSeconds).ShouldHaveSingleItem().Value.ShouldBe(42.5);
  }

  // M4.04: the gauges the alerts read, from the operational tables, through the snapshot of the sampler.
  [Fact]
  public void ReportJobQueue_ShouldPublishTheDeadJobsTheHeartbeatAndTheQuotaSizes()
  {
    _meter.ReportJobQueue(
      new JobQueueSnapshot(
        new Dictionary<string, long>(StringComparer.Ordinal),
        0,
        Dead: 3,
        HeartbeatAgeSeconds: 12.5,
        GitHubLimits: new Dictionary<string, long>(StringComparer.Ordinal) { ["101"] = 5000, ["202"] = 15000 },
        GitHubRemaining: new Dictionary<string, long>(StringComparer.Ordinal) { ["101"] = 450, ["202"] = 15000 }
      )
    );

    _recorder.Collect();

    _recorder.Of(CodeCoverageMeter.JobsDead).ShouldHaveSingleItem().Value.ShouldBe(3);
    _recorder.Of(CodeCoverageMeter.WorkerHeartbeatAgeSeconds).ShouldHaveSingleItem().Value.ShouldBe(12.5);
    var limits = _recorder.Of(CodeCoverageMeter.GitHubRateLimitLimit);
    limits.Count.ShouldBe(2);
    limits.Single(m => Equals(m.Tags["installation"], "101")).Value.ShouldBe(5000);
    limits.Single(m => Equals(m.Tags["installation"], "202")).Value.ShouldBe(15000);
    var remaining = _recorder.Of(CodeCoverageMeter.GitHubRateLimitRecordedRemaining);
    remaining.Count.ShouldBe(2);
    remaining.Single(m => Equals(m.Tags["installation"], "101")).Value.ShouldBe(450);
  }

  // No worker ever beat: no age is published, so that absent_over_time and the stale rule tell the cases apart.
  [Fact]
  public void ReportJobQueue_NoHeartbeat_ShouldPublishNoAge()
  {
    _meter.ReportJobQueue(new JobQueueSnapshot(new Dictionary<string, long>(StringComparer.Ordinal), 0));

    _recorder.Collect();

    _recorder.Of(CodeCoverageMeter.WorkerHeartbeatAgeSeconds).ShouldBeEmpty();
    _recorder.Of(CodeCoverageMeter.GitHubRateLimitLimit).ShouldBeEmpty();
    _recorder.Of(CodeCoverageMeter.GitHubRateLimitRecordedRemaining).ShouldBeEmpty();
    _recorder.Of(CodeCoverageMeter.JobsDead).ShouldHaveSingleItem().Value.ShouldBe(0);
  }

  [Fact]
  public void JobGauges_BeforeAnyReport_ShouldPublishNothing()
  {
    _recorder.Collect();

    _recorder.Of(CodeCoverageMeter.JobsDead).ShouldBeEmpty();
    _recorder.Of(CodeCoverageMeter.WorkerHeartbeatAgeSeconds).ShouldBeEmpty();

    _recorder.Of(CodeCoverageMeter.JobsQueued).ShouldBeEmpty();
    _recorder.Of(CodeCoverageMeter.JobOldestQueuedAgeSeconds).ShouldBeEmpty();
    _recorder.Of(CodeCoverageMeter.GitHubRateLimitRemaining).ShouldBeEmpty();
  }

  [Fact]
  public void ReportGitHubRateLimit_ShouldPublishTheLastValueOfEachInstallation()
  {
    _meter.ReportGitHubRateLimit("101", 5000);
    _meter.ReportGitHubRateLimit("101", 4990);
    _meter.ReportGitHubRateLimit("202", 12);

    _recorder.Collect();

    var remaining = _recorder.Of(CodeCoverageMeter.GitHubRateLimitRemaining);
    remaining.Count.ShouldBe(2);
    remaining.Single(m => Equals(m.Tags["installation"], "101")).Value.ShouldBe(4990);
    remaining.Single(m => Equals(m.Tags["installation"], "202")).Value.ShouldBe(12);
  }

  [Fact]
  public void Record_ShouldTagEachMeasurement()
  {
    _meter.UploadReceived("my-org/core", flag: null);
    _meter.UploadFailed("Uploads.TooLarge");
    _meter.ParseDuration("cobertura", TimeSpan.FromMilliseconds(250));
    _meter.GitHubRequest("check-runs", 201);
    _meter.UnresolvedPaths(0);
    _meter.UnresolvedPaths(4);
    _meter.NotifyLatency(TimeSpan.FromSeconds(3));
    _meter.WebhookSignatureFailure();

    var received = _recorder.Of(CodeCoverageMeter.UploadsReceivedTotal).ShouldHaveSingleItem();
    received.Tags["repo"].ShouldBe("my-org/core");
    received.Tags["flag"].ShouldBe(string.Empty);
    _recorder.Of(CodeCoverageMeter.UploadsFailedTotal).ShouldHaveSingleItem().Tags["reason"].ShouldBe("Uploads.TooLarge");
    var parse = _recorder.Of(CodeCoverageMeter.ParseDurationSeconds).ShouldHaveSingleItem();
    parse.Value.ShouldBe(0.25);
    parse.Tags["format"].ShouldBe("cobertura");
    var request = _recorder.Of(CodeCoverageMeter.GitHubRequestsTotal).ShouldHaveSingleItem();
    request.Tags["endpoint"].ShouldBe("check-runs");
    request.Tags["status"].ShouldBe(201);
    _recorder.Of(CodeCoverageMeter.UnresolvedPathsTotal).ShouldHaveSingleItem().Value.ShouldBe(4, "an upload without unresolved path counts nothing");
    _recorder.Of(CodeCoverageMeter.NotifyLatencySeconds).ShouldHaveSingleItem().Value.ShouldBe(3);
    _recorder.Sum(CodeCoverageMeter.WebhookSignatureFailuresTotal).ShouldBe(1);
  }

  [Fact]
  public void Record_WithBlankTag_ShouldThrowArgumentException()
  {
    Should.Throw<ArgumentException>(() => _meter.UploadReceived(" ", "unit"));
    Should.Throw<ArgumentException>(() => _meter.UploadFailed(string.Empty));
    Should.Throw<ArgumentException>(() => _meter.ParseDuration(" ", TimeSpan.Zero));
    Should.Throw<ArgumentException>(() => _meter.GitHubRequest(" ", 200));
    Should.Throw<ArgumentException>(() => _meter.ReportGitHubRateLimit(" ", 1));
    Should.Throw<ArgumentNullException>(() => _meter.ReportJobQueue(null!));
  }

  // Once its last queued job is claimed, a type drops to 0 instead of keeping its last value.
  [Fact]
  public void ReportJobQueue_WhenATypeHasNoQueuedJobLeft_ShouldPublishZeroForIt()
  {
    _meter.ReportJobQueue(
      new JobQueueSnapshot(new Dictionary<string, long>(StringComparer.Ordinal) { ["FinalizeCommit"] = 3, ["NotifyCommit"] = 1 }, 42.5)
    );
    _recorder.Collect();
    _meter.ReportJobQueue(new JobQueueSnapshot(new Dictionary<string, long>(StringComparer.Ordinal) { ["FinalizeCommit"] = 2 }, 0));

    _recorder.Collect();

    var notify = _recorder.Of(CodeCoverageMeter.JobsQueued).Where(m => Equals(m.Tags["type"], "NotifyCommit")).ToList();
    notify.Count.ShouldBe(2);
    notify[^1].Value.ShouldBe(0);
    _recorder.Of(CodeCoverageMeter.JobsQueued).Last(m => Equals(m.Tags["type"], "FinalizeCommit")).Value.ShouldBe(2);
  }

  [Fact]
  public void IsJobQueueObserved_ShouldTellWhetherAListenerCollectsTheJobGauges()
  {
    using var factory = new TestMeterFactory();
    var meter = new CodeCoverageMeter(factory);

    meter.IsJobQueueObserved.ShouldBeFalse("nobody listens without an OTLP endpoint");

    using (new MeasurementRecorder(factory))
    {
      meter.IsJobQueueObserved.ShouldBeTrue();
    }

    meter.IsJobQueueObserved.ShouldBeFalse("the listener is gone");
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
