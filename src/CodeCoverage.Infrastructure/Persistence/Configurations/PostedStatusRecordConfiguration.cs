// ------------------------------------------------------------------------------------------------
// <copyright file="PostedStatusRecordConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Notifications;
using CodeCoverage.Infrastructure.Notifications;
using CodeCoverage.Infrastructure.Persistence.Organizations;

namespace CodeCoverage.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="PostedStatusRecord" /> to the <c>PostedStatuses</c> table.</summary>
/// <remarks>
///   A table owned by an organization, with no foreign key to <c>Organizations</c> (ADR-013). The primary key does not
///   hold the organization: a commit has one, so the commit of a new row is checked by
///   <see cref="Repositories.PostedStatusRepository" />.
/// </remarks>
internal sealed class PostedStatusRecordConfiguration : IEntityTypeConfiguration<PostedStatusRecord>
{
  #region Fields

  /// <summary>The primary key of the published statuses.</summary>
  public const string PrimaryKeyName = "PK_PostedStatuses";

  /// <summary>The maximum length of the name of a state.</summary>
  private const int StateMaxLength = 16;

  private static readonly ValueComparer<string> OrdinalComparer = new(
    static (left, right) => string.Equals(left, right, StringComparison.Ordinal),
    static value => StringComparer.Ordinal.GetHashCode(value),
    static value => value
  );

  #endregion

  #region Implementation of IEntityTypeConfiguration<PostedStatusRecord>

  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<PostedStatusRecord> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("PostedStatuses");
    builder.HasKey(static s => new { s.CommitId, s.Context }).HasName(PrimaryKeyName);
    builder.IsOrganizationOwned();

    // A context can end with a flag, which is case-sensitive: the same model as CommitFlagTotals.Flag. The comparer
    // keeps two contexts that differ by their case apart in the change tracker; CodeCoverageDbContext gives the
    // column its collation on SQL Server.
    builder.Property(static s => s.Context)
           .IsRequired()
           .HasMaxLength(PostedStatus.ContextMaxLength)
           .Metadata.SetValueComparer(OrdinalComparer);
    builder.Property(static s => s.State).IsRequired().HasMaxLength(StateMaxLength);
    builder.Property(static s => s.Description).IsRequired().HasMaxLength(CommitStatus.DescriptionMaxLength);
    builder.Property(static s => s.TargetUrl).HasMaxLength(PostedStatus.TargetUrlMaxLength);
    builder.Property(static s => s.PostedAt).IsRequired();
    builder.Property(static s => s.CheckRunId);

    builder.HasOne<Commit>().WithMany().HasForeignKey(static s => s.CommitId).OnDelete(DeleteBehavior.Restrict);
  }

  #endregion
}