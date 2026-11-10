// ------------------------------------------------------------------------------------------------
// <copyright file="ComparisonRecordConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Git;
using CodeCoverage.Infrastructure.Comparisons;
using CodeCoverage.Infrastructure.Persistence.Conventions;
using CodeCoverage.Infrastructure.Persistence.Organizations;

namespace CodeCoverage.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="ComparisonRecord" /> to the <c>Comparisons</c> table.</summary>
/// <remarks>
///   A table owned by an organization, with no foreign key to <c>Organizations</c> (ADR-013). The unique index
///   does not hold the organization: a commit has one, so the commit of a new row is checked by
///   <see cref="Repositories.ComparisonRepository" />.
/// </remarks>
internal sealed class ComparisonRecordConfiguration : IEntityTypeConfiguration<ComparisonRecord>
{
  #region Fields

  /// <summary>The unique index on the commit and the pull request.</summary>
  public const string CommitPullIndexName = "UX_Comparisons_CommitId_PullNumber";

  /// <summary>The maximum length of the name of the source of a diff.</summary>
  private const int DiffSourceMaxLength = 16;

  #endregion

  #region Implementation of IEntityTypeConfiguration<ComparisonRecord>

  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<ComparisonRecord> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("Comparisons");
    builder.HasKey(static c => c.Id);
    builder.IsOrganizationOwned();

    builder.Property(static c => c.PullNumber).IsRequired();
    builder.Property(static c => c.ForkPointSha).HasMaxLength(GitSha.Length).HasConversion<GitShaConverter>();
    builder.Property(static c => c.ComparedToSha).HasMaxLength(GitSha.Length).HasConversion<GitShaConverter>();
    builder.Property(static c => c.HeadRevision).IsRequired();
    builder.Property(static c => c.HeadCoverage).HasPrecision(CommitConfiguration.CoveragePrecision, CommitConfiguration.CoverageScale);
    builder.Property(static c => c.BaseCoverage).HasPrecision(CommitConfiguration.CoveragePrecision, CommitConfiguration.CoverageScale);
    builder.Property(static c => c.DiffSource).IsRequired().HasMaxLength(DiffSourceMaxLength);
    builder.Property(static c => c.DiffIncomplete).IsRequired();
    builder.Property(static c => c.Data).IsRequired();
    builder.Property(static c => c.ComputedAt).IsRequired();

    builder.HasIndex(static c => new { c.CommitId, c.PullNumber }).IsUnique().HasDatabaseName(CommitPullIndexName);

    builder.HasOne<Commit>().WithMany().HasForeignKey(static c => c.CommitId).OnDelete(DeleteBehavior.Restrict);
  }

  #endregion
}