// ------------------------------------------------------------------------------------------------
// <copyright file="CommitFlagTotalsConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Git;
using CodeCoverage.Infrastructure.Persistence.Conventions;
using CodeCoverage.Infrastructure.Persistence.Organizations;

namespace CodeCoverage.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="CommitFlagTotals" /> to the <c>CommitFlagTotals</c> table.</summary>
/// <remarks>A table owned by an organization, with no foreign key to <c>Organizations</c> (ADR-013): its commit has one.</remarks>
internal sealed class CommitFlagTotalsConfiguration : IEntityTypeConfiguration<CommitFlagTotals>
{
  #region Fields

  /// <summary>The index of the history of a flag on a branch.</summary>
  private const string HistoryIndexName = "IX_CommitFlagTotals_RepositoryId_Branch_Flag_CompletedAt";

  /// <summary>The comparison of the flags by the change tracker: by code point, whatever the engine.</summary>
  private static readonly ValueComparer<string> OrdinalComparer = new(
    static (left, right) => string.Equals(left, right, StringComparison.Ordinal),
    static value => StringComparer.Ordinal.GetHashCode(value),
    static value => value
  );

  #endregion

  #region Implementation of IEntityTypeConfiguration<CommitFlagTotals>

  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<CommitFlagTotals> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("CommitFlagTotals");
    builder.HasKey(static t => new { t.CommitId, t.Flag });
    builder.IsOrganizationOwned();

    // A flag is case-sensitive, as on Codecov. With the SQL Server provider, the key of the change tracker does not
    // tell Backend from backend without this comparer, binary collation or not (SaveFinalized_FlagsDifferingByCase
    // fails in the identity map); CodeCoverageDbContext gives the column that collation for the database itself.
    builder.Property(static t => t.Flag)
           .IsRequired()
           .HasMaxLength(CommitFlagTotals.FlagMaxLength)
           .Metadata.SetValueComparer(OrdinalComparer);
    builder.Property(static t => t.RepositoryId).IsRequired();
    builder.Property(static t => t.Branch).HasMaxLength(Commit.BranchMaxLength);
    builder.Property(static t => t.CompletedAt).IsRequired();
    builder.Property(static t => t.Lines).IsRequired();
    builder.Property(static t => t.Hits).IsRequired();
    builder.Property(static t => t.Misses).IsRequired();
    builder.Property(static t => t.Partials).IsRequired();
    builder.Property(static t => t.Coverage).HasPrecision(CommitConfiguration.CoveragePrecision, CommitConfiguration.CoverageScale);
    builder.Property(static t => t.IsCarriedForward).IsRequired();
    builder.Property(static t => t.CarriedForwardFromSha).HasMaxLength(GitSha.Length).HasConversion<GitShaConverter>();

    builder.HasIndex(static t => new { t.RepositoryId, t.Branch, t.Flag, t.CompletedAt }).HasDatabaseName(HistoryIndexName);

    builder.HasOne<Commit>().WithMany().HasForeignKey(static t => t.CommitId).OnDelete(DeleteBehavior.Restrict);
  }

  #endregion
}
