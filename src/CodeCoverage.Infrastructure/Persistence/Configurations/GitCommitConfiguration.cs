// ------------------------------------------------------------------------------------------------
// <copyright file="GitCommitConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Infrastructure.Persistence.Conventions;
using CodeCoverage.Infrastructure.Persistence.Organizations;

namespace CodeCoverage.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="GitCommit" /> to the <c>GitCommits</c> table.</summary>
/// <remarks>
///   A table owned by an organization, with no foreign key to <c>Organizations</c> (ADR-013). The primary key
///   does not hold the organization: a repository has one, so the repository of a new row is checked by
///   <see cref="Repositories.GitCommitRepository" />.
/// </remarks>
internal sealed class GitCommitConfiguration : IEntityTypeConfiguration<GitCommit>
{
  #region Fields

  /// <summary>The primary key of the cached commits.</summary>
  public const string PrimaryKeyName = "PK_GitCommits";

  /// <summary>The name of the <c>GitCommits</c> table.</summary>
  private const string TableName = "GitCommits";

  #endregion

  #region Implementation of IEntityTypeConfiguration<GitCommit>

  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<GitCommit> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable(TableName);
    builder.HasKey(static g => new { g.RepositoryId, g.Sha }).HasName(PrimaryKeyName);
    builder.IsOrganizationOwned();

    builder.Property(static g => g.Sha).IsRequired().HasMaxLength(GitSha.Length).HasConversion<GitShaConverter>();

    builder.Property(static g => g.ParentShas)
           .IsRequired()
           .HasConversion(new GitShaListConverter(), JsonListConverter<GitSha>.Comparer);

    builder.Property(static g => g.Message).IsRequired().HasMaxLength(GitCommit.MessageMaxLength);
    builder.Property(static g => g.AuthorLogin).HasMaxLength(Organization.LoginMaxLength);
    builder.Property(static g => g.CommittedAt).IsRequired();
    builder.Property(static g => g.FetchedAt).IsRequired();

    builder.HasOne<TrackedRepository>().WithMany().HasForeignKey(static g => g.RepositoryId).OnDelete(DeleteBehavior.Restrict);
  }

  #endregion
}
