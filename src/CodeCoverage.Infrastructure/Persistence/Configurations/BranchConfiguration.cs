// ------------------------------------------------------------------------------------------------
// <copyright file="BranchConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Infrastructure.Persistence.Conventions;
using CodeCoverage.Infrastructure.Persistence.Organizations;

namespace CodeCoverage.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="Branch" /> to the <c>Branches</c> table.</summary>
/// <remarks>
///   <para>
///     A table owned by an organization, with no foreign key to <c>Organizations</c> (ADR-013). The primary key
///     does not hold the organization: a repository has one, so the repository of a new row is checked by
///     <see cref="Repositories.BranchRepository" />.
///   </para>
///   <para><c>CoveredCommitId</c> has no constraint until the <c>Commits</c> table exists.</para>
/// </remarks>
internal sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
  #region Fields

  /// <summary>The primary key of the branches.</summary>
  public const string PrimaryKeyName = "PK_Branches";

  /// <summary>The name of the <c>Branches</c> table.</summary>
  private const string TableName = "Branches";

  #endregion

  #region Implementation of IEntityTypeConfiguration<Branch>

  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<Branch> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable(TableName);
    builder.HasKey(static b => new { b.RepositoryId, b.Name }).HasName(PrimaryKeyName);
    builder.IsOrganizationOwned();

    builder.Property(static b => b.Name).IsRequired().HasMaxLength(Branch.NameMaxLength);
    builder.Property(static b => b.HeadSha).IsRequired().HasMaxLength(GitSha.Length).HasConversion<GitShaConverter>();
    builder.Property(static b => b.HeadUpdatedAt).IsRequired();
    builder.Property(static b => b.CoveredCommitId);
    builder.Property(static b => b.CoveredAt);
    builder.Property(static b => b.Version).IsRequired().IsConcurrencyToken();

    builder.HasOne<TrackedRepository>().WithMany().HasForeignKey(static b => b.RepositoryId).OnDelete(DeleteBehavior.Restrict);
  }

  #endregion
}
