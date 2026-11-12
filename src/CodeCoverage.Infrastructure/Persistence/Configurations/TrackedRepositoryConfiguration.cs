// ------------------------------------------------------------------------------------------------
// <copyright file="TrackedRepositoryConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Infrastructure.Persistence.Conventions;
using CodeCoverage.Infrastructure.Persistence.Organizations;

namespace CodeCoverage.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="TrackedRepository" /> to the <c>Repositories</c> table.</summary>
/// <remarks>
///   <para>
///     A table owned by an organization. Both unique indexes start with <c>OrganizationId</c>: the same name or
///     the same external identifier can exist in two organizations, and the first index serves the foreign key.
///   </para>
///   <para>
///     The namespace of a forge that has none is an empty text, so that the unique index needs no filter of
///     its own on either engine. Its two text columns take at most 1,534 bytes on SQL Server, whose limit is
///     1,700.
///   </para>
/// </remarks>
internal sealed class TrackedRepositoryConfiguration : IEntityTypeConfiguration<TrackedRepository>
{
  #region Fields

  /// <summary>The unique index of the repositories of an organization by namespace and name.</summary>
  public const string NameIndexName = "UX_Repositories_OrganizationId_Namespace_Name";

  /// <summary>The unique index of the repositories of an organization by external identifier.</summary>
  public const string ExternalIdIndexName = "UX_Repositories_OrganizationId_ExternalId";

  /// <summary>The name of the <c>Repositories</c> table.</summary>
  private const string TableName = "Repositories";

  #endregion

  #region Implementation of IEntityTypeConfiguration<TrackedRepository>

  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<TrackedRepository> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable(TableName);
    builder.HasKey(static r => r.Id);
    builder.IsOrganizationOwned();

    builder.Property(static r => r.InstallationId).IsRequired();
    builder.Property(static r => r.ExternalId).IsRequired().HasMaxLength(TrackedRepository.ExternalIdMaxLength);
    builder.Property(static r => r.Owner).IsRequired().HasMaxLength(Organization.LoginMaxLength);
    builder.Property(static r => r.Namespace).IsRequired().HasMaxLength(TrackedRepository.NamespaceMaxLength);
    builder.Property(static r => r.NormalizedNamespace).IsRequired().HasMaxLength(TrackedRepository.NamespaceMaxLength);
    builder.Property(static r => r.Name).IsRequired().HasMaxLength(TrackedRepository.NameMaxLength);
    builder.Property(static r => r.NormalizedName).IsRequired().HasMaxLength(TrackedRepository.NameMaxLength);
    builder.Property(static r => r.DefaultBranch).IsRequired().HasMaxLength(Branch.NameMaxLength);
    builder.Property(static r => r.IsPrivate).IsRequired();
    builder.Property(static r => r.IsActive).IsRequired();
    builder.Property(static r => r.TransferredAt);
    builder.Property(static r => r.DisabledAt);
    builder.Ignore(static r => r.IsTracked);
    builder.Property(static r => r.BadgeTokenProtected).IsRequired();
    builder.Property(static r => r.DefaultBranchYaml);
    builder.Property(static r => r.DefaultBranchYamlSha).HasMaxLength(GitSha.Length).HasConversion<GitShaConverter>();
    builder.Property(static r => r.CreatedAt).IsRequired();
    builder.Property(static r => r.UpdatedAt).IsRequired();
    builder.Property(static r => r.Version).IsRequired().IsConcurrencyToken();

    builder.HasIndex(
             OrganizationOwnedExtensions.ColumnName,
             nameof(TrackedRepository.NormalizedNamespace),
             nameof(TrackedRepository.NormalizedName)
           )
           .IsUnique()
           .HasDatabaseName(NameIndexName);

    builder.HasIndex(OrganizationOwnedExtensions.ColumnName, nameof(TrackedRepository.ExternalId))
           .IsUnique()
           .HasDatabaseName(ExternalIdIndexName);

    builder.HasOrganizationForeignKey();
    builder.HasOne<Installation>().WithMany().HasForeignKey(static r => r.InstallationId).OnDelete(DeleteBehavior.Restrict);
  }

  #endregion
}
