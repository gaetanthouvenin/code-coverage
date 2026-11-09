// ------------------------------------------------------------------------------------------------
// <copyright file="InstallationConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="Installation" /> to the <c>Installations</c> table.</summary>
/// <remarks>
///   <para>
///     The table is global and read before a scope enters an organization, so its <c>OrganizationId</c> is an
///     ordinary column under no query filter. The key is the identifier GitHub gives the installation.
///   </para>
///   <para>
///     An account can hold several rows over time, since a reinstallation gets a new identifier. The unique
///     index on the organization and the key serves the foreign key in place of the index of the convention.
///   </para>
/// </remarks>
internal sealed class InstallationConfiguration : IEntityTypeConfiguration<Installation>
{
  #region Fields

  /// <summary>The name of the <c>Installations</c> table.</summary>
  private const string TableName = "Installations";

  /// <summary>The unique index that serves the foreign key to the organization.</summary>
  private const string OrganizationIndexName = "UX_Installations_OrganizationId_Id";

  #endregion

  #region Implementation of IEntityTypeConfiguration<Installation>

  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<Installation> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable(TableName);
    builder.HasKey(static i => i.Id);

    builder.Property(static i => i.Id).ValueGeneratedNever();
    builder.Property(static i => i.OrganizationId).IsRequired();
    builder.Property(static i => i.SuspendedAt);
    builder.Property(static i => i.CreatedAt).IsRequired();
    builder.Property(static i => i.UpdatedAt).IsRequired();

    builder.HasIndex(static i => new { i.OrganizationId, i.Id }).IsUnique().HasDatabaseName(OrganizationIndexName);
    builder.HasOne<Organization>().WithMany().HasForeignKey(static i => i.OrganizationId).OnDelete(DeleteBehavior.Restrict);
  }

  #endregion
}
