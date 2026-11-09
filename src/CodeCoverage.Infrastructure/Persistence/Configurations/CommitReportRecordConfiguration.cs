// ------------------------------------------------------------------------------------------------
// <copyright file="CommitReportRecordConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CodeCoverage.Domain.Commits;
using CodeCoverage.Infrastructure.Commits;
using CodeCoverage.Infrastructure.Persistence.Organizations;

namespace CodeCoverage.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="CommitReportRecord" /> to the <c>CommitReports</c> table.</summary>
/// <remarks>A table owned by an organization, with no foreign key to <c>Organizations</c> (ADR-013): its commit has one.</remarks>
internal sealed class CommitReportRecordConfiguration : IEntityTypeConfiguration<CommitReportRecord>
{
  #region Implementation of IEntityTypeConfiguration<CommitReportRecord>

  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<CommitReportRecord> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("CommitReports");
    builder.HasKey(static r => r.CommitId);
    builder.IsOrganizationOwned();

    builder.Property(static r => r.CommitId).ValueGeneratedNever();
    builder.Property(static r => r.Revision).IsRequired();
    builder.Property(static r => r.Codec).IsRequired();
    builder.Property(static r => r.Data).IsRequired();
    builder.Property(static r => r.FileIndex).IsRequired();

    builder.HasOne<Commit>().WithMany().HasForeignKey(static r => r.CommitId).OnDelete(DeleteBehavior.Restrict);
  }

  #endregion
}
