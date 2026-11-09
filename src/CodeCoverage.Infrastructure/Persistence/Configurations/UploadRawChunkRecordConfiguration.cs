// ------------------------------------------------------------------------------------------------
// <copyright file="UploadRawChunkRecordConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CodeCoverage.Infrastructure.Persistence.Organizations;
using CodeCoverage.Infrastructure.Uploads;

namespace CodeCoverage.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="UploadRawChunkRecord" /> to the <c>UploadRawChunks</c> table.</summary>
/// <remarks>A table owned by an organization, with no foreign key to <c>Organizations</c> (ADR-013): its file has one.</remarks>
internal sealed class UploadRawChunkRecordConfiguration : IEntityTypeConfiguration<UploadRawChunkRecord>
{
  #region Implementation of IEntityTypeConfiguration<UploadRawChunkRecord>

  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<UploadRawChunkRecord> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("UploadRawChunks");
    builder.HasKey(static c => new { c.RawFileId, c.ChunkIndex });
    builder.IsOrganizationOwned();

    builder.Property(static c => c.Data).IsRequired();

    builder.HasOne<UploadRawFileRecord>().WithMany().HasForeignKey(static c => c.RawFileId).OnDelete(DeleteBehavior.Restrict);
  }

  #endregion
}
