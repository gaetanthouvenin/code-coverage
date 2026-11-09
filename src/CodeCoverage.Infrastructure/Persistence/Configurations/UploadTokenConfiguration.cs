// ------------------------------------------------------------------------------------------------
// <copyright file="UploadTokenConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CodeCoverage.Domain.Operations;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Tokens;
using CodeCoverage.Infrastructure.Persistence.Organizations;

namespace CodeCoverage.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="UploadToken" /> to the <c>UploadTokens</c> table.</summary>
/// <remarks>
///   A table owned by an organization, whose unique index starts with the organization and serves the foreign key
///   (ADR-013): an imported value is chosen by the caller, so the same value can exist in two organizations.
/// </remarks>
internal sealed class UploadTokenConfiguration : IEntityTypeConfiguration<UploadToken>
{
  #region Fields

  /// <summary>The unique index of the tokens of an organization by hash.</summary>
  public const string HashIndexName = "UX_UploadTokens_OrganizationId_Hash";

  #endregion

  #region Implementation of IEntityTypeConfiguration<UploadToken>

  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<UploadToken> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("UploadTokens");
    builder.HasKey(static t => t.Id);
    builder.IsOrganizationOwned();

    builder.Property(static t => t.Scope).IsRequired();
    builder.Property(static t => t.RepositoryId);
    builder.Property(static t => t.Label).IsRequired().HasMaxLength(UploadToken.LabelMaxLength);
    builder.Property(static t => t.Hint).IsRequired().HasMaxLength(UploadToken.HintMaxLength);
    builder.Property(static t => t.Hash).IsRequired().HasMaxLength(UploadToken.HashLength).IsFixedLength();
    builder.Property(static t => t.Imported).IsRequired();
    builder.Property(static t => t.CreatedBy).IsRequired().HasMaxLength(AuditEntry.ActorMaxLength);
    builder.Property(static t => t.CreatedAt).IsRequired();
    builder.Property(static t => t.ExpiresAt);
    builder.Property(static t => t.LastUsedAt);
    builder.Property(static t => t.RevokedAt);
    builder.Property(static t => t.RotationAdvisedAt);

    builder.HasIndex(OrganizationOwnedExtensions.ColumnName, nameof(UploadToken.Hash)).IsUnique().HasDatabaseName(HashIndexName);

    builder.HasOrganizationForeignKey();
    builder.HasOne<TrackedRepository>().WithMany().HasForeignKey(static t => t.RepositoryId).OnDelete(DeleteBehavior.Restrict);
  }

  #endregion
}
