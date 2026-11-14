// ------------------------------------------------------------------------------------------------
// <copyright file="PurgeExpiredDataCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using CodeCoverage.Application.Configuration;
using CodeCoverage.Application.Features.Operations.Commands.PurgeExpiredData;
using CodeCoverage.Application.Tests.Support;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Tests.Features.Operations.Commands.PurgeExpiredData;

public sealed class PurgeExpiredDataCommandHandlerTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 3, 0, 0, TimeSpan.Zero);

  private readonly ICurrentOrganization _currentOrganization = A.Fake<ICurrentOrganization>();

  private readonly InMemoryOrganizationDirectory _directory = new(Now);

  private readonly IJobQueue _jobQueue = A.Fake<IJobQueue>();

  private readonly IRetentionRepository _retention = A.Fake<IRetentionRepository>();

  private readonly ICodecovImportRepository _codecovImports = A.Fake<ICodecovImportRepository>();

  private readonly RetentionOptions _options = new() { RawDays = 30, BranchReportDays = 180, JobDays = 14, WebhookDays = 7, DiffCacheDays = 30 };

  private readonly List<JobRequest> _queued = [];

  private readonly PurgeExpiredDataCommandHandler _handler;

  #endregion

  #region Ctors

  public PurgeExpiredDataCommandHandlerTests()
  {
    var options = A.Fake<IOptionsMonitor<RetentionOptions>>();
    var clock = A.Fake<IClock>();

    A.CallTo(() => clock.UtcNow).Returns(Now);
    A.CallTo(() => options.CurrentValue).Returns(_options);
    A.CallTo(() => _jobQueue.EnqueueAsync(A<JobRequest>._, A<CancellationToken>._))
     .Invokes((JobRequest job, CancellationToken _) => _queued.Add(job))
     .Returns(1L);

    _handler = new PurgeExpiredDataCommandHandler(
      _currentOrganization,
      _directory,
      _jobQueue,
      _retention,
      _codecovImports,
      options,
      clock,
      NullLogger<PurgeExpiredDataCommandHandler>.Instance
    );
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  // The coordinator (ADR-013): it purges the operational tables itself, reads the organizations only, and queues one
  // purge per active organization, by its identifier.
  [Fact]
  public async Task Handle_OnTheLaneOfTheInstance_ShouldPurgeOperationalTablesAndQueueOnePurgePerActiveOrganization()
  {
    A.CallTo(() => _currentOrganization.IsKnown).Returns(false);
    var first = _directory.Add("1001", "first", admitted: true);
    var second = _directory.Add("2002", "second", admitted: true);
    _directory.Add("3003", "pending");
    _directory.Add("4004", "suspended", admitted: true).Suspend(SuspensionReason.Administrator, Now);

    await _handler.Handle(new PurgeExpiredDataCommand(), Ct);

    A.CallTo(() => _retention.PurgeJobsAsync(Now.AddDays(-14), PurgeExpiredDataJobs.BatchSize, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _retention.PurgeWebhookDeliveriesAsync(Now.AddDays(-7), PurgeExpiredDataJobs.BatchSize, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _retention.PurgeRawUploadsAsync(A<DateTimeOffset>._, A<int>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => _retention.CompactCommitsAsync(A<DateTimeOffset>._, A<int>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => _retention.PurgeForgeCachesAsync(A<DateTimeOffset>._, A<int>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(_codecovImports).MustNotHaveHappened();

    _queued.Select(static j => j.OrganizationId).ShouldBe([first.Id, second.Id]);
    _queued.ShouldAllBe(static j => j.Type == JobTypes.PurgeExpiredData
                                    && j.PayloadJson == "{}"
                                    && j.LockKey == "purge"
                                    && j.DedupKey == "purge"
                                    && j.Coalesce
                                    && !j.IsInstance);
  }

  // In an organization: the raw files whose time is over, the commits off the default branch, the forge caches. The
  // operational tables and the queue are left to the coordinator.
  [Fact]
  public async Task Handle_InAnOrganization_ShouldPurgeItsTablesOnly()
  {
    A.CallTo(() => _currentOrganization.IsKnown).Returns(true);
    A.CallTo(() => _currentOrganization.Id).Returns(7);

    await _handler.Handle(new PurgeExpiredDataCommand(), Ct);

    A.CallTo(() => _retention.PurgeRawUploadsAsync(Now, PurgeExpiredDataJobs.BatchSize, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _retention.CompactCommitsAsync(Now.AddDays(-180), PurgeExpiredDataJobs.BatchSize, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _retention.PurgeForgeCachesAsync(Now.AddDays(-30), PurgeExpiredDataJobs.BatchSize, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    // The credentials of the imports of the history of Codecov never outlive seven days.
    A.CallTo(() => _codecovImports.PurgeAsync(Now.AddDays(-7), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _retention.PurgeJobsAsync(A<DateTimeOffset>._, A<int>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => _retention.PurgeWebhookDeliveriesAsync(A<DateTimeOffset>._, A<int>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(_jobQueue).MustNotHaveHappened();
  }

  // A setting at zero never purges what was written today: one day at least.
  [Fact]
  public async Task Handle_ZeroDays_ShouldKeepOneDay()
  {
    A.CallTo(() => _currentOrganization.IsKnown).Returns(true);
    _options.BranchReportDays = 0;

    await _handler.Handle(new PurgeExpiredDataCommand(), Ct);

    A.CallTo(() => _retention.CompactCommitsAsync(Now.AddDays(-1), A<int>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_Null_ShouldThrow()
  {
    await Should.ThrowAsync<ArgumentNullException>(async () => await _handler.Handle(null!, Ct));
  }

  #endregion
}
