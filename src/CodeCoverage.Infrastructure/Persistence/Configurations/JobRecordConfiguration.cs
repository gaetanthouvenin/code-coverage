// ------------------------------------------------------------------------------------------------
// <copyright file="JobRecordConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Infrastructure.Jobs;

namespace CodeCoverage.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="JobRecord" /> to the <c>Jobs</c> table.</summary>
/// <remarks>
///   <para>
///     The index <c>IX(State, RunAfter, Id)</c> serves the claim of the next job. The two unique
///     indexes filtered by state, <see cref="DedupIndexName" /> (<c>DedupKey</c> where
///     <c>State = 'Queued'</c>) and <see cref="LockIndexName" /> (<c>LockKey</c> where
///     <c>State = 'Running'</c>), are <b>not</b> in the model: the quoting of their filter differs
///     between the engines, so each migrations assembly writes them in its own SQL (migration
///     <c>AddJobs</c>). The model stays free of engine-specific SQL.
///   </para>
///   <para>
///     <c>PayloadJson</c> and <c>LastError</c> are unbounded text columns, never queried.
///   </para>
/// </remarks>
internal sealed class JobRecordConfiguration : IEntityTypeConfiguration<JobRecord>
{
  #region Fields

  /// <summary>The name of the <c>Jobs</c> table.</summary>
  public const string TableName = "Jobs";

  /// <summary>The unique index of the queued jobs by <c>DedupKey</c>, written by the migrations.</summary>
  public const string DedupIndexName = "UX_Jobs_DedupKey_Queued";

  /// <summary>The unique index of the running jobs by <c>LockKey</c>, written by the migrations.</summary>
  public const string LockIndexName = "UX_Jobs_LockKey_Running";

  #endregion

  #region Implementation of IEntityTypeConfiguration<JobRecord>

  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<JobRecord> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable(TableName);
    builder.HasKey(static j => j.Id);

    builder.Property(static j => j.Type).IsRequired().HasMaxLength(JobRequest.TypeMaxLength);
    builder.Property(static j => j.PayloadJson).IsRequired();
    builder.Property(static j => j.LockKey).HasMaxLength(JobRequest.KeyMaxLength);
    builder.Property(static j => j.DedupKey).HasMaxLength(JobRequest.KeyMaxLength);
    builder.Property(static j => j.State).IsRequired();
    builder.Property(static j => j.Attempts).IsRequired();
    builder.Property(static j => j.MaxAttempts).IsRequired();
    builder.Property(static j => j.RunAfter).IsRequired();
    builder.Property(static j => j.FirstQueuedAt).IsRequired();
    builder.Property(static j => j.LockedBy).HasMaxLength(JobRecord.LockedByMaxLength);
    builder.Property(static j => j.LockedUntil);
    builder.Property(static j => j.LastError);
    builder.Property(static j => j.CorrelationId).IsRequired().HasMaxLength(JobRecord.CorrelationIdMaxLength);
    builder.Property(static j => j.CreatedAt).IsRequired();
    builder.Property(static j => j.CompletedAt);

    builder.HasIndex(static j => new { j.State, j.RunAfter, j.Id });
  }

  #endregion
}
