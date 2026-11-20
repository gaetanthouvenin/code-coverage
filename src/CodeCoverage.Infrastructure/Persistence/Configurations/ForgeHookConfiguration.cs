// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeHookConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CodeCoverage.Domain.Connections;
using CodeCoverage.Infrastructure.Persistence.Organizations;

namespace CodeCoverage.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="ForgeHook" /> to the <c>ForgeHooks</c> table.</summary>
/// <remarks>
///   <para>
///     A table owned by an organization (ADR-013), whose unique index on the connection and the scope starts with the
///     organization and serves its foreign key. The kind of scope and the state are stored as text, so a new value
///     needs no migration.
///   </para>
///   <para>
///     A hook refers to its connection, a row of the global <c>ForgeConnections</c> table, by the organization and the
///     identifier of the connection: a hook can only point to a connection of its own organization. The unique index on
///     the connection and the scope starts with these two columns and serves this foreign key too. A connection cannot be
///     deleted while it has hooks: a connection that ends is suspended.
///   </para>
/// </remarks>
internal sealed class ForgeHookConfiguration : IEntityTypeConfiguration<ForgeHook>
{
  #region Fields

  /// <summary>The unique index of the hooks of a connection by scope, named within the 63 characters of PostgreSQL.</summary>
  public const string ScopeIndexName = "UX_ForgeHooks_OrganizationId_ConnectionId_Scope";

  /// <summary>The name of the <c>ForgeHooks</c> table.</summary>
  private const string TableName = "ForgeHooks";

  #endregion

  #region Implementation of IEntityTypeConfiguration<ForgeHook>

  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<ForgeHook> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable(TableName);
    builder.HasKey(static h => h.Id);
    builder.IsOrganizationOwned();

    builder.Property(static h => h.ConnectionId).IsRequired().HasMaxLength(ForgeConnection.IdMaxLength);
    builder.Property(static h => h.ScopeKind).IsRequired();
    builder.Property(static h => h.ScopeExternalId).IsRequired().HasMaxLength(ForgeHook.ScopeExternalIdMaxLength);
    builder.Property(static h => h.ExternalId).HasMaxLength(ForgeHook.ExternalIdMaxLength);
    builder.Property(static h => h.State).IsRequired();
    builder.Property(static h => h.LastCheckedAt).IsRequired();
    builder.Property(static h => h.LastError).HasMaxLength(ForgeHook.LastErrorMaxLength);

    builder.HasIndex(
             OrganizationOwnedExtensions.ColumnName,
             nameof(ForgeHook.ConnectionId),
             nameof(ForgeHook.ScopeKind),
             nameof(ForgeHook.ScopeExternalId))
           .IsUnique()
           .HasDatabaseName(ScopeIndexName);

    builder.HasOrganizationForeignKey();
    builder.HasOne<ForgeConnection>()
           .WithMany()
           .HasForeignKey(OrganizationOwnedExtensions.ColumnName, nameof(ForgeHook.ConnectionId))
           .HasPrincipalKey(static c => new { c.OrganizationId, c.Id })
           .OnDelete(DeleteBehavior.Restrict);
  }

  #endregion
}
