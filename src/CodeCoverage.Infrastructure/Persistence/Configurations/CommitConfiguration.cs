// ------------------------------------------------------------------------------------------------
// <copyright file="CommitConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Infrastructure.Persistence.Conventions;
using CodeCoverage.Infrastructure.Persistence.Organizations;

namespace CodeCoverage.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="Commit" /> to the <c>Commits</c> table.</summary>
/// <remarks>
///   A table owned by an organization, with no foreign key to <c>Organizations</c> (ADR-013). The unique index
///   does not hold the organization: a repository has one, so the repository of a new row is checked by
///   <see cref="Repositories.CommitRepository" />.
/// </remarks>
internal sealed class CommitConfiguration : IEntityTypeConfiguration<Commit>
{
  #region Fields

  /// <summary>The unique index of the commits of a repository by SHA.</summary>
  public const string ShaIndexName = "UX_Commits_RepositoryId_Sha";

  /// <summary>The name of the <c>Commits</c> table.</summary>
  private const string TableName = "Commits";

  /// <summary>The index that lists the finalized commits of a branch.</summary>
  private const string BranchIndexName = "IX_Commits_RepositoryId_Branch_CompletedAt";

  /// <summary>The total number of digits of a coverage.</summary>
  private const int CoveragePrecision = 9;

  /// <summary>The number of decimals of a coverage.</summary>
  private const int CoverageScale = 5;

  #endregion

  #region Implementation of IEntityTypeConfiguration<Commit>

  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<Commit> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable(TableName);
    builder.HasKey(static c => c.Id);
    builder.IsOrganizationOwned();

    builder.Property(static c => c.RepositoryId).IsRequired();
    builder.Property(static c => c.Sha).IsRequired().HasMaxLength(GitSha.Length).HasConversion<GitShaConverter>();
    builder.Property(static c => c.Branch).HasMaxLength(Commit.BranchMaxLength);
    builder.Property(static c => c.PullNumber);
    builder.Property(static c => c.State).IsRequired();
    builder.Property(static c => c.UploadSequence).IsRequired();
    builder.Property(static c => c.Files).IsRequired();
    builder.Property(static c => c.Lines).IsRequired();
    builder.Property(static c => c.Hits).IsRequired();
    builder.Property(static c => c.Misses).IsRequired();
    builder.Property(static c => c.Partials).IsRequired();
    builder.Property(static c => c.Branches).IsRequired();
    builder.Property(static c => c.Methods).IsRequired();
    builder.Property(static c => c.Coverage).HasPrecision(CoveragePrecision, CoverageScale);
    builder.Property(static c => c.SessionCount).IsRequired();
    builder.Property(static c => c.UploadedSessionCount).IsRequired();
    builder.Property(static c => c.ReportRevision).IsRequired();
    builder.Property(static c => c.CarryforwardStatus).IsRequired();
    builder.Property(static c => c.ConfigSource).IsRequired().HasMaxLength(Commit.ConfigSourceMaxLength);
    builder.Property(static c => c.LastUploadAt);
    builder.Property(static c => c.CompletedAt);
    builder.Property(static c => c.NotifiedAt);
    builder.Property(static c => c.CreatedAt).IsRequired();
    builder.Property(static c => c.UpdatedAt).IsRequired();
    builder.Property(static c => c.Version).IsRequired().IsConcurrencyToken();

    builder.HasIndex(static c => new { c.RepositoryId, c.Sha }).IsUnique().HasDatabaseName(ShaIndexName);
    builder.HasIndex(static c => new { c.RepositoryId, c.Branch, c.CompletedAt }).HasDatabaseName(BranchIndexName);

    builder.HasOne<TrackedRepository>().WithMany().HasForeignKey(static c => c.RepositoryId).OnDelete(DeleteBehavior.Restrict);
  }

  #endregion
}
