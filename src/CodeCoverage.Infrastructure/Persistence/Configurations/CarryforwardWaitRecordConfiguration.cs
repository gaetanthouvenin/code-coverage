// ------------------------------------------------------------------------------------------------
// <copyright file="CarryforwardWaitRecordConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CodeCoverage.Domain.Commits;
using CodeCoverage.Infrastructure.Commits;
using CodeCoverage.Infrastructure.Persistence.Organizations;

namespace CodeCoverage.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="CarryforwardWaitRecord" /> to the <c>CarryforwardWaits</c> table (#165).</summary>
/// <remarks>
///   A table owned by an organization, with no foreign key to <c>Organizations</c> (ADR-013): both commits belong to one
///   repository, so to one organization. The index on the ancestor finds the commits a completed commit wakes.
/// </remarks>
internal sealed class CarryforwardWaitRecordConfiguration : IEntityTypeConfiguration<CarryforwardWaitRecord>
{
  #region Fields

  /// <summary>The primary key of the waits.</summary>
  public const string PrimaryKeyName = "PK_CarryforwardWaits";

  #endregion

  #region Implementation of IEntityTypeConfiguration<CarryforwardWaitRecord>

  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<CarryforwardWaitRecord> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("CarryforwardWaits");
    builder.HasKey(static w => new { w.CommitId, w.AncestorId }).HasName(PrimaryKeyName);
    builder.IsOrganizationOwned();

    builder.HasIndex(static w => w.AncestorId);

    builder.HasOne<Commit>().WithMany().HasForeignKey(static w => w.CommitId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<Commit>().WithMany().HasForeignKey(static w => w.AncestorId).OnDelete(DeleteBehavior.Restrict);
  }

  #endregion
}
