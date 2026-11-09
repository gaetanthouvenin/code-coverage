// ------------------------------------------------------------------------------------------------
// <copyright file="UploadCoverageRecordConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CodeCoverage.Domain.Commits;
using CodeCoverage.Infrastructure.Commits;
using CodeCoverage.Infrastructure.Persistence.Organizations;

namespace CodeCoverage.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="UploadCoverageRecord" /> to the <c>UploadCoverages</c> table.</summary>
/// <remarks>A table owned by an organization, with no foreign key to <c>Organizations</c> (ADR-013): its upload has one.</remarks>
internal sealed class UploadCoverageRecordConfiguration : IEntityTypeConfiguration<UploadCoverageRecord>
{
  #region Implementation of IEntityTypeConfiguration<UploadCoverageRecord>

  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<UploadCoverageRecord> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("UploadCoverages");
    builder.HasKey(static c => c.UploadId);
    builder.IsOrganizationOwned();

    builder.Property(static c => c.UploadId).ValueGeneratedNever();
    builder.Property(static c => c.Codec).IsRequired();
    builder.Property(static c => c.Data).IsRequired();

    builder.HasOne<Upload>().WithMany().HasForeignKey(static c => c.UploadId).OnDelete(DeleteBehavior.Restrict);
  }

  #endregion
}
