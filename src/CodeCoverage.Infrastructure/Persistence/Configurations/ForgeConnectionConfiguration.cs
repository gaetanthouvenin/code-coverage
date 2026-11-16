// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeConnectionConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="ForgeConnection" /> to the <c>ForgeConnections</c> table.</summary>
/// <remarks>
///   <para>
///     The table is global, like <c>Installations</c> (ADR-017): a webhook reads it before a scope enters an
///     organization, so its <c>OrganizationId</c> is an ordinary column under no query filter. The key is the text
///     identifier the instance gives the connection.
///   </para>
///   <para>
///     The kind and the status are stored as text, so a new value needs no migration. The protected secrets have no
///     maximum length: the size of a protected value depends on the key ring. The unique index on the organization and
///     the key serves the foreign key in place of the index of the convention.
///   </para>
/// </remarks>
internal sealed class ForgeConnectionConfiguration : IEntityTypeConfiguration<ForgeConnection>
{
  #region Fields

  /// <summary>The name of the <c>ForgeConnections</c> table.</summary>
  private const string TableName = "ForgeConnections";

  /// <summary>The unique index that serves the foreign key to the organization.</summary>
  private const string OrganizationIndexName = "UX_ForgeConnections_OrganizationId_Id";

  #endregion

  #region Implementation of IEntityTypeConfiguration<ForgeConnection>

  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<ForgeConnection> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable(TableName);
    builder.HasKey(static c => c.Id);

    builder.Property(static c => c.Id).HasMaxLength(ForgeConnection.IdMaxLength).ValueGeneratedNever();
    builder.Property(static c => c.OrganizationId).IsRequired();
    builder.Property(static c => c.Forge).IsRequired();
    builder.Property(static c => c.Kind).IsRequired();
    builder.Property(static c => c.ExternalAccountId).IsRequired().HasMaxLength(Organization.ExternalAccountIdMaxLength);
    builder.Property(static c => c.BaseAddress).IsRequired().HasMaxLength(ForgeConnection.BaseAddressMaxLength);
    builder.Property(static c => c.TenantId);
    builder.Property(static c => c.ServiceAccountExternalId).HasMaxLength(ForgeConnection.ServiceAccountExternalIdMaxLength);
    builder.Property(static c => c.CredentialProtected).IsRequired();
    builder.Property(static c => c.WebhookSecretProtected).IsRequired();
    builder.Property(static c => c.WebhookSecretPreviousProtected);
    builder.Property(static c => c.ExpiresAt);
    builder.Property(static c => c.Status).IsRequired();
    builder.Property(static c => c.ConnectedAt).IsRequired();
    builder.Property(static c => c.SuspendedAt);
    builder.Property(static c => c.LastCheckedAt);
    builder.Property(static c => c.LastError).HasMaxLength(ForgeConnection.LastErrorMaxLength);

    builder.HasIndex(static c => new { c.OrganizationId, c.Id }).IsUnique().HasDatabaseName(OrganizationIndexName);
    builder.HasOne<Organization>().WithMany().HasForeignKey(static c => c.OrganizationId).OnDelete(DeleteBehavior.Restrict);
  }

  #endregion
}
