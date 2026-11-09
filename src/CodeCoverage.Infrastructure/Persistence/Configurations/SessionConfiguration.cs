// ------------------------------------------------------------------------------------------------
// <copyright file="SessionConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Git;
using CodeCoverage.Infrastructure.Persistence.Conventions;
using CodeCoverage.Infrastructure.Persistence.Organizations;

namespace CodeCoverage.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="Session" /> to the <c>CommitSessions</c> table.</summary>
/// <remarks>
///   A table owned by an organization, with no foreign key to <c>Organizations</c> (ADR-013): its commit has one.
///   <c>SourceSessionId</c> has no foreign key: the sessions of the ancestor it names are written again by its next
///   finalization.
/// </remarks>
internal sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
  #region Fields

  /// <summary>The index that lists the sessions of a commit.</summary>
  private const string CommitIndexName = "IX_CommitSessions_CommitId";

  #endregion

  #region Implementation of IEntityTypeConfiguration<Session>

  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<Session> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("CommitSessions");
    builder.HasKey(static s => s.Id);
    builder.IsOrganizationOwned();

    builder.Property(static s => s.CommitId).IsRequired();
    builder.Property(static s => s.Kind).IsRequired();
    builder.Property(static s => s.Number).IsRequired();
    builder.Property(static s => s.UploadId);
    builder.Property(static s => s.CarriedForwardFromSha).HasMaxLength(GitSha.Length).HasConversion<GitShaConverter>();
    builder.Property(static s => s.SourceSessionId);
    builder.Property(static s => s.DataUploadId);
    builder.Property(static s => s.Data);

    builder.Property(static s => s.Flags)
           .IsRequired()
           .HasConversion(new JsonListConverter<string>(), JsonListConverter<string>.Comparer);

    builder.Property(static s => s.Name).HasMaxLength(Session.NameMaxLength);
    builder.Property(static s => s.CiService).HasMaxLength(Upload.CiServiceMaxLength);
    builder.Property(static s => s.BuildUrl).HasMaxLength(Upload.BuildUrlMaxLength);
    builder.Property(static s => s.Lines).IsRequired();
    builder.Property(static s => s.Hits).IsRequired();
    builder.Property(static s => s.Misses).IsRequired();
    builder.Property(static s => s.Partials).IsRequired();
    builder.Property(static s => s.Coverage).HasPrecision(CommitConfiguration.CoveragePrecision, CommitConfiguration.CoverageScale);

    builder.HasIndex(static s => s.CommitId).HasDatabaseName(CommitIndexName);

    builder.HasOne<Commit>().WithMany().HasForeignKey(static s => s.CommitId).OnDelete(DeleteBehavior.Restrict);
  }

  #endregion
}
