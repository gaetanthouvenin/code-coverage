// ------------------------------------------------------------------------------------------------
// <copyright file="RecurringJobCatalog.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Jobs;

namespace CodeCoverage.Infrastructure.Jobs;

/// <summary>The recurring jobs of the application, queued by the <see cref="RecurringJobScheduler" />.</summary>
/// <remarks>
///   <para>
///     Each feature that runs a job at a fixed period adds its definition here, next to its
///     constant in <c>JobTypes</c> and its case in the <see cref="JobDispatcher" /> (ARCHITECTURE §6.1):
///   </para>
///   <list type="bullet">
///     <item><description><c>SyncInstallations</c>, daily (lock and dedup <c>installations</c>);</description></item>
///     <item><description><c>SweepPendingCommits</c>, every 5 minutes (lock and dedup <c>sweep</c>);</description></item>
///     <item><description><c>PurgeExpiredData</c>, daily (lock and dedup <c>purge</c>).</description></item>
///   </list>
///   <para>
///     Two definitions never share a <see cref="RecurringJobDefinition.DedupKey" />: the scheduler refuses
///     such a catalog when it is created.
///   </para>
/// </remarks>
internal static class RecurringJobCatalog
{
  #region Properties

  /// <summary>Gets every recurring job of the application.</summary>
  public static IReadOnlyList<RecurringJobDefinition> All { get; } =
  [
    new(JobTypes.SyncInstallations, TimeSpan.FromDays(1), SyncInstallationsJobs.Key, SyncInstallationsJobs.Key),
    new(JobTypes.SweepPendingCommits, TimeSpan.FromMinutes(5), SweepPendingCommitsJobs.Key, SweepPendingCommitsJobs.Key),
    new(JobTypes.PurgeExpiredData, TimeSpan.FromDays(1), PurgeExpiredDataJobs.Key, PurgeExpiredDataJobs.Key),
  ];

  #endregion
}
