// ------------------------------------------------------------------------------------------------
// <copyright file="WorkerHeartbeatConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CodeCoverage.Domain.Operations;

namespace CodeCoverage.Infrastructure.Persistence.Configurations;

/// <summary>Maps the <see cref="WorkerHeartbeat" /> to the operational table <c>WorkerHeartbeats</c>, one row per worker process.</summary>
internal sealed class WorkerHeartbeatConfiguration : IEntityTypeConfiguration<WorkerHeartbeat>
{
  #region Fields

  /// <summary>The name of the table.</summary>
  public const string TableName = "WorkerHeartbeats";

  /// <summary>The name of the primary key.</summary>
  public const string PrimaryKeyName = "PK_WorkerHeartbeats";

  #endregion

  #region Implementation of IEntityTypeConfiguration<WorkerHeartbeat>

  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<WorkerHeartbeat> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable(TableName);
    builder.HasKey(static h => h.Host).HasName(PrimaryKeyName);
    builder.Property(static h => h.Host).HasMaxLength(WorkerHeartbeat.HostMaxLength).ValueGeneratedNever();
    builder.Property(static h => h.StartedAt).IsRequired();
    builder.Property(static h => h.LastBeatAt).IsRequired();
  }

  #endregion
}
