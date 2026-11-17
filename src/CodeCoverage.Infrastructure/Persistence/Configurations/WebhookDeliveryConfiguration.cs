// ------------------------------------------------------------------------------------------------
// <copyright file="WebhookDeliveryConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CodeCoverage.Infrastructure.Webhooks;

namespace CodeCoverage.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="WebhookDeliveryRecord" /> to the <c>WebhookDeliveries</c> table.</summary>
/// <remarks>
///   The table is operational (ADR-013): a delivery is written before any scope enters an organization, so its
///   <c>OrganizationId</c> is an ordinary nullable column, with neither query filter nor foreign key. The primary key,
///   on the forge and the identifier of the delivery, makes a delivery idempotent on its forge; the forge is stored as
///   text, the connection and the repository as their forge names them.
/// </remarks>
internal sealed class WebhookDeliveryConfiguration : IEntityTypeConfiguration<WebhookDeliveryRecord>
{
  #region Fields

  /// <summary>The name of the primary key, which refuses a delivery already known on its forge.</summary>
  public const string PrimaryKeyName = "PK_WebhookDeliveries";

  #endregion

  #region Implementation of IEntityTypeConfiguration<WebhookDeliveryRecord>

  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<WebhookDeliveryRecord> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("WebhookDeliveries");
    builder.HasKey(static d => new { d.Forge, d.DeliveryId }).HasName(PrimaryKeyName);

    builder.Property(static d => d.Forge).IsRequired();
    builder.Property(static d => d.DeliveryId).ValueGeneratedNever().HasMaxLength(WebhookDeliveryRecord.DeliveryIdMaxLength);
    builder.Property(static d => d.Event).IsRequired().HasMaxLength(WebhookDeliveryRecord.EventMaxLength);
    builder.Property(static d => d.Action).HasMaxLength(WebhookDeliveryRecord.ActionMaxLength);
    builder.Property(static d => d.ConnectionId).HasMaxLength(WebhookDeliveryRecord.ConnectionIdMaxLength);
    builder.Property(static d => d.RepositoryExternalId).HasMaxLength(WebhookDeliveryRecord.RepositoryExternalIdMaxLength);
    builder.Property(static d => d.Payload).IsRequired();
    builder.Property(static d => d.ReceivedAt).IsRequired();
    builder.Property(static d => d.Error).HasMaxLength(WebhookDeliveryRecord.ErrorMaxLength);

    builder.HasIndex(static d => d.ReceivedAt);
  }

  #endregion
}
