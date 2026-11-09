// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryTreeRecordConfiguration.cs" company="Gaëtan Thouvenin">
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

/// <summary>Maps <see cref="RepositoryTreeRecord" /> to the <c>RepositoryTrees</c> table.</summary>
/// <remarks>
///   A table owned by an organization, with no foreign key to <c>Organizations</c> (ADR-013). The primary key does not
///   hold the organization: a repository has one, so the repository of a new row is checked by
///   <see cref="Repositories.RepositoryTreeRepository" />.
/// </remarks>
internal sealed class RepositoryTreeRecordConfiguration : IEntityTypeConfiguration<RepositoryTreeRecord>
{
  #region Fields

  /// <summary>The primary key of the cached trees.</summary>
  public const string PrimaryKeyName = "PK_RepositoryTrees";

  #endregion

  #region Implementation of IEntityTypeConfiguration<RepositoryTreeRecord>

  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<RepositoryTreeRecord> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("RepositoryTrees");
    builder.HasKey(static t => new { t.RepositoryId, t.Sha }).HasName(PrimaryKeyName);
    builder.IsOrganizationOwned();

    builder.Property(static t => t.Sha).IsRequired().HasMaxLength(GitSha.Length).HasConversion<GitShaConverter>();
    builder.Property(static t => t.Data).IsRequired();
    builder.Property(static t => t.Truncated).IsRequired();
    builder.Property(static t => t.FetchedAt).IsRequired();

    builder.HasOne<TrackedRepository>().WithMany().HasForeignKey(static t => t.RepositoryId).OnDelete(DeleteBehavior.Restrict);
  }

  #endregion
}
