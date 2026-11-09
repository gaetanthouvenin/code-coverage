// ------------------------------------------------------------------------------------------------
// <copyright file="DiffCacheRecordConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Infrastructure.Commits;
using CodeCoverage.Infrastructure.Persistence.Conventions;
using CodeCoverage.Infrastructure.Persistence.Organizations;

namespace CodeCoverage.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="DiffCacheRecord" /> to the <c>DiffCache</c> table.</summary>
/// <remarks>
///   A table owned by an organization, with no foreign key to <c>Organizations</c> (ADR-013). The primary key does not
///   hold the organization: a repository has one, so the repository of a new row is checked by
///   <see cref="Repositories.DiffCacheRepository" />.
/// </remarks>
internal sealed class DiffCacheRecordConfiguration : IEntityTypeConfiguration<DiffCacheRecord>
{
  #region Fields

  /// <summary>The primary key of the cached diffs.</summary>
  public const string PrimaryKeyName = "PK_DiffCache";

  /// <summary>The maximum length of the name of a kind of diff.</summary>
  private const int KindMaxLength = 16;

  #endregion

  #region Implementation of IEntityTypeConfiguration<DiffCacheRecord>

  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<DiffCacheRecord> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("DiffCache");
    builder.HasKey(static d => new { d.RepositoryId, d.BaseSha, d.HeadSha, d.Kind }).HasName(PrimaryKeyName);
    builder.IsOrganizationOwned();

    builder.Property(static d => d.BaseSha).IsRequired().HasMaxLength(GitSha.Length).HasConversion<GitShaConverter>();
    builder.Property(static d => d.HeadSha).IsRequired().HasMaxLength(GitSha.Length).HasConversion<GitShaConverter>();
    builder.Property(static d => d.Kind).IsRequired().HasMaxLength(KindMaxLength);
    builder.Property(static d => d.Data).IsRequired();
    builder.Property(static d => d.Incomplete).IsRequired();
    builder.Property(static d => d.FetchedAt).IsRequired();

    builder.HasOne<TrackedRepository>().WithMany().HasForeignKey(static d => d.RepositoryId).OnDelete(DeleteBehavior.Restrict);
  }

  #endregion
}
