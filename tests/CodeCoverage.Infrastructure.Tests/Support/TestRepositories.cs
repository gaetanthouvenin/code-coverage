// ------------------------------------------------------------------------------------------------
// <copyright file="TestRepositories.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Configuration;

using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Diagnostics;
using CodeCoverage.Infrastructure.Identity;
using CodeCoverage.Infrastructure.Jobs;
using CodeCoverage.Infrastructure.Persistence;

using CodeCoverage.Infrastructure.Persistence.Repositories;

namespace CodeCoverage.Infrastructure.Tests.Support;

// Builds the repositories that queue a job in the transaction of their change, with the real queue.
internal static class TestRepositories
{
  #region Fields

  public static readonly DateTimeOffset Now = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);

  // Shared: no test listens to the measurements of a queue built here.
  private static readonly TestMeterFactory MeterFactory = new();

  #endregion

  #region Methods

  // organizationId: the organization the scope entered, which is the one of the context; none on the lane of the instance.
  public static RepositoryRepository Repositories(CodeCoverageDbContext context, long? organizationId = null, JobWakeUpSignal? wakeUpSignal = null)
  {
    var signal = wakeUpSignal ?? new JobWakeUpSignal(TimeProvider.System);

    return new RepositoryRepository(context, Queue(context, organizationId, signal), signal);
  }

  public static SqlJobQueue Queue(CodeCoverageDbContext context, long? organizationId, JobWakeUpSignal wakeUpSignal)
  {
    var organization = new OrganizationContext();

    if (organizationId is { } id)
    {
      organization.Enter(id);
    }

    var clock = A.Fake<IClock>();
    A.CallTo(() => clock.UtcNow).Returns(Now);

    return new SqlJobQueue(
      context,
      organization,
      clock,
      new ConfigurationBuilder().Build(),
      JobClaimDialects.For(context),
      wakeUpSignal,
      new CodeCoverageMeter(MeterFactory)
    );
  }

  #endregion
}