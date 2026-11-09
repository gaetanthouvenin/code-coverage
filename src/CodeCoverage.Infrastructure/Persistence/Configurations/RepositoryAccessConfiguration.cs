// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryAccessConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Users;
using CodeCoverage.Infrastructure.Persistence.Organizations;

namespace CodeCoverage.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="RepositoryAccess" /> to the <c>UserRepositoryAccess</c> table.</summary>
/// <remarks>
///   A table owned by an organization, whose unique index starts with the organization and serves the foreign key
///   (ADR-013). <c>RepositoryExternalId</c> has no foreign key to <c>Repositories</c>: the cache can cite a repository
///   that is not tracked yet.
/// </remarks>
internal sealed class RepositoryAccessConfiguration : IEntityTypeConfiguration<RepositoryAccess>
{
  #region Fields

  /// <summary>The unique index of the accesses of an organization by user and repository.</summary>
  public const string AccessIndexName = "UX_UserRepositoryAccess_OrganizationId_UserId_RepositoryExternalId";

  #endregion

  #region Implementation of IEntityTypeConfiguration<RepositoryAccess>

  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<RepositoryAccess> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("UserRepositoryAccess");
    builder.HasKey(static a => a.Id);
    builder.IsOrganizationOwned();

    builder.Property(static a => a.UserId).IsRequired();
    builder.Property(static a => a.RepositoryExternalId).IsRequired().HasMaxLength(TrackedRepository.ExternalIdMaxLength);
    builder.Property(static a => a.Permission).IsRequired();
    builder.Property(static a => a.RefreshedAt).IsRequired();

    builder.HasIndex(OrganizationOwnedExtensions.ColumnName, nameof(RepositoryAccess.UserId), nameof(RepositoryAccess.RepositoryExternalId))
           .IsUnique()
           .HasDatabaseName(AccessIndexName);

    builder.HasOrganizationForeignKey();
    builder.HasOne<AppUser>().WithMany().HasForeignKey(static a => a.UserId).OnDelete(DeleteBehavior.Restrict);
  }

  #endregion
}
