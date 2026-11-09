// ------------------------------------------------------------------------------------------------
// <copyright file="WebhookDeliveryConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Infrastructure.GitHub;

namespace CodeCoverage.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="WebhookDeliveryRecord" /> to the <c>WebhookDeliveries</c> table.</summary>
/// <remarks>
///   The table is operational (ADR-013): a delivery is written before any scope enters an organization, so its
///   <c>OrganizationId</c> is an ordinary nullable column, with neither query filter nor foreign key. The primary key
///   makes a delivery idempotent.
/// </remarks>
internal sealed class WebhookDeliveryConfiguration : IEntityTypeConfiguration<WebhookDeliveryRecord>
{
  #region Fields

  /// <summary>The name of the primary key, which refuses a delivery already known.</summary>
  public const string PrimaryKeyName = "PK_WebhookDeliveries";

  #endregion

  #region Implementation of IEntityTypeConfiguration<WebhookDeliveryRecord>

  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<WebhookDeliveryRecord> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("WebhookDeliveries");
    builder.HasKey(static d => d.DeliveryId).HasName(PrimaryKeyName);

    builder.Property(static d => d.DeliveryId).ValueGeneratedNever().HasMaxLength(GitHubWebhookEvent.DeliveryIdMaxLength);
    builder.Property(static d => d.Event).IsRequired().HasMaxLength(GitHubWebhookEvent.EventMaxLength);
    builder.Property(static d => d.Action).HasMaxLength(GitHubWebhookEvent.ActionMaxLength);
    builder.Property(static d => d.Payload).IsRequired();
    builder.Property(static d => d.ReceivedAt).IsRequired();
    builder.Property(static d => d.Error).HasMaxLength(WebhookDeliveryRecord.ErrorMaxLength);

    builder.HasIndex(static d => d.ReceivedAt);
  }

  #endregion
}
