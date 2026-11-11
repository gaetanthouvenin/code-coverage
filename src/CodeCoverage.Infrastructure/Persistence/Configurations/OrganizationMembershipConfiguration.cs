// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationMembershipConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="OrganizationMembership" /> to the <c>OrganizationMemberships</c> table.</summary>
/// <remarks>
///   A global table (ADR-013): it tells which organizations a user may enter, so it is read before a scope enters
///   one. Its <c>OrganizationId</c> is an ordinary column, without the query filter of the organization tables.
/// </remarks>
internal sealed class OrganizationMembershipConfiguration : IEntityTypeConfiguration<OrganizationMembership>
{
  #region Fields

  /// <summary>
  ///   The name of the unique index: one membership per user and per organization. It starts with the organization,
  ///   so that it also serves the foreign key to it.
  /// </summary>
  public const string MembershipIndexName = "UX_OrganizationMemberships_OrganizationId_UserId";

  private const string TableName = "OrganizationMemberships";

  #endregion

  #region Implementation of IEntityTypeConfiguration<OrganizationMembership>

  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<OrganizationMembership> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable(TableName);
    builder.HasKey(static m => m.Id);

    builder.Property(static m => m.UserId).IsRequired();
    builder.Property(static m => m.OrganizationId).IsRequired();
    builder.Property(static m => m.Role).IsRequired();
    builder.Property(static m => m.RefreshedAt).IsRequired();

    builder.HasIndex(static m => new { m.OrganizationId, m.UserId }).IsUnique().HasDatabaseName(MembershipIndexName);

    builder.HasOne<AppUser>().WithMany().HasForeignKey(static m => m.UserId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<Organization>().WithMany().HasForeignKey(static m => m.OrganizationId).OnDelete(DeleteBehavior.Restrict);
  }

  #endregion
}
