// ------------------------------------------------------------------------------------------------
// <copyright file="WebhookDeliveryRecord.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.GitHub;

/// <summary>A webhook delivery: one row of the <c>WebhookDeliveries</c> table.</summary>
internal sealed class WebhookDeliveryRecord
{
  #region Fields

  /// <summary>The maximum length of <see cref="Error" />.</summary>
  public const int ErrorMaxLength = 2000;

  #endregion

  #region Properties

  /// <summary>Gets or sets the identifier of the delivery.</summary>
  public string DeliveryId { get; set; } = string.Empty;

  /// <summary>Gets or sets the organization of the delivery, or <see langword="null" /> on the lane of the instance.</summary>
  public long? OrganizationId { get; set; }

  /// <summary>Gets or sets the name of the event.</summary>
  public string Event { get; set; } = string.Empty;

  /// <summary>Gets or sets the action of the event, or <see langword="null" />.</summary>
  public string? Action { get; set; }

  /// <summary>Gets or sets the installation, or <see langword="null" />.</summary>
  public long? InstallationId { get; set; }

  /// <summary>Gets or sets the identifier of the repository on GitHub, or <see langword="null" />.</summary>
  public long? RepositoryGitHubId { get; set; }

  /// <summary>Gets or sets the raw body, compressed with gzip.</summary>
  public byte[] Payload { get; set; } = [];

  /// <summary>Gets or sets when it was received, in UTC.</summary>
  public DateTimeOffset ReceivedAt { get; set; }

  /// <summary>Gets or sets when it was handled, in UTC, or <see langword="null" />.</summary>
  public DateTimeOffset? ProcessedAt { get; set; }

  /// <summary>Gets or sets why it could not be handled, or <see langword="null" />.</summary>
  public string? Error { get; set; }

  #endregion
}
