// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryFlagRecordConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Infrastructure.Notifications;
using CodeCoverage.Infrastructure.Persistence.Organizations;

namespace CodeCoverage.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="RepositoryFlagRecord" /> to the <c>RepositoryFlags</c> table.</summary>
/// <remarks>
///   A table owned by an organization, with no foreign key to <c>Organizations</c> (ADR-013). The primary key does not
///   hold the organization: a repository has one, so the repository of a new row is checked by
///   <see cref="Repositories.RepositoryFlagRepository" />.
/// </remarks>
internal sealed class RepositoryFlagRecordConfiguration : IEntityTypeConfiguration<RepositoryFlagRecord>
{
  #region Fields

  /// <summary>The primary key of the flags of the repositories.</summary>
  public const string PrimaryKeyName = "PK_RepositoryFlags";

  private static readonly ValueComparer<string> OrdinalComparer = new(
    static (left, right) => string.Equals(left, right, StringComparison.Ordinal),
    static value => StringComparer.Ordinal.GetHashCode(value),
    static value => value
  );

  #endregion

  #region Implementation of IEntityTypeConfiguration<RepositoryFlagRecord>

  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<RepositoryFlagRecord> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("RepositoryFlags");
    builder.HasKey(static f => new { f.RepositoryId, f.Flag }).HasName(PrimaryKeyName);
    builder.IsOrganizationOwned();

    // A flag is case-sensitive: the same model as CommitFlagTotals.Flag. The comparer keeps two flags that differ by
    // their case apart in the change tracker; CodeCoverageDbContext gives the column its collation on SQL Server.
    builder.Property(static f => f.Flag)
           .IsRequired()
           .HasMaxLength(CommitFlagTotals.FlagMaxLength)
           .Metadata.SetValueComparer(OrdinalComparer);
    builder.Property(static f => f.CreatedAt).IsRequired();

    builder.HasOne<TrackedRepository>().WithMany().HasForeignKey(static f => f.RepositoryId).OnDelete(DeleteBehavior.Restrict);
  }

  #endregion
}
