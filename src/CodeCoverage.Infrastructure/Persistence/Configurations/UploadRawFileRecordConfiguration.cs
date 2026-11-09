// ------------------------------------------------------------------------------------------------
// <copyright file="UploadRawFileRecordConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CodeCoverage.Domain.Contracts.Uploads;
using CodeCoverage.Infrastructure.Persistence.Organizations;
using CodeCoverage.Infrastructure.Uploads;

namespace CodeCoverage.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="UploadRawFileRecord" /> to the <c>UploadRawFiles</c> table.</summary>
/// <remarks>
///   A table owned by an organization, with no foreign key to <c>Organizations</c> (ADR-013). <c>UploadPublicId</c> has no
///   foreign key to <c>Uploads</c>: the parts are written before the upload exists, and those of an upload never recorded
///   leave with <c>ExpiresAt</c>.
/// </remarks>
internal sealed class UploadRawFileRecordConfiguration : IEntityTypeConfiguration<UploadRawFileRecord>
{
  #region Implementation of IEntityTypeConfiguration<UploadRawFileRecord>

  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<UploadRawFileRecord> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("UploadRawFiles");
    builder.HasKey(static f => f.Id);
    builder.IsOrganizationOwned();

    builder.Property(static f => f.UploadPublicId).IsRequired();
    builder.Property(static f => f.Kind).IsRequired();
    builder.Property(static f => f.FileName).IsRequired().HasMaxLength(UploadPart.FileNameMaxLength);
    builder.Property(static f => f.DetectedFormat);
    builder.Property(static f => f.Sha256).IsRequired().HasMaxLength(UploadRawFileRecord.Sha256Length).IsFixedLength();
    builder.Property(static f => f.CompressedSize).IsRequired();
    builder.Property(static f => f.UncompressedSize).IsRequired();
    builder.Property(static f => f.ExpiresAt).IsRequired();

    builder.HasIndex(static f => f.UploadPublicId).HasDatabaseName("IX_UploadRawFiles_UploadPublicId");
    builder.HasIndex(static f => f.ExpiresAt).HasDatabaseName("IX_UploadRawFiles_ExpiresAt");
  }

  #endregion
}
