// ------------------------------------------------------------------------------------------------
// <copyright file="CodecovImportConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CodeCoverage.Domain.Codecov;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Infrastructure.Persistence.Organizations;

namespace CodeCoverage.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="CodecovImport" /> to the <c>CodecovImports</c> table.</summary>
/// <remarks>
///   A table owned by an organization, with no foreign key to <c>Organizations</c> (ADR-013): its rows are those of a
///   repository, which has one organization. A row holds the protected token of Codecov and lives as long as its
///   import, seven days at most.
/// </remarks>
internal sealed class CodecovImportConfiguration : IEntityTypeConfiguration<CodecovImport>
{
  #region Implementation of IEntityTypeConfiguration<CodecovImport>

  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<CodecovImport> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("CodecovImports");
    builder.HasKey(static i => i.Id);
    builder.IsOrganizationOwned();

    builder.Property(static i => i.RepositoryId).IsRequired();
    builder.Property(static i => i.ProtectedToken).IsRequired();
    builder.Property(static i => i.CreatedAt).IsRequired();
    builder.Property(static i => i.NextPage).IsRequired();
    builder.Property(static i => i.ImportedCommits).IsRequired();

    // The purge reads the oldest rows first.
    builder.HasIndex(static i => i.CreatedAt);

    builder.HasOne<TrackedRepository>().WithMany().HasForeignKey(static i => i.RepositoryId).OnDelete(DeleteBehavior.Restrict);
  }

  #endregion
}
