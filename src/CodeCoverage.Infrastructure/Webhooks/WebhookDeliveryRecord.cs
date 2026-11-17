// ------------------------------------------------------------------------------------------------
// <copyright file="WebhookDeliveryRecord.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Infrastructure.Webhooks;

/// <summary>A webhook delivery of any forge: one row of the <c>WebhookDeliveries</c> table.</summary>
/// <remarks>
///   A delivery is known by its forge and its identifier, since two forges may send the same identifier. The connection
///   and the repository are kept in text, as their forge names them: on GitHub, the installation and the numeric
///   identifier of the repository.
/// </remarks>
internal sealed class WebhookDeliveryRecord
{
  #region Fields

  /// <summary>The maximum length of <see cref="DeliveryId" />.</summary>
  public const int DeliveryIdMaxLength = 64;

  /// <summary>The maximum length of <see cref="Event" />.</summary>
  public const int EventMaxLength = 64;

  /// <summary>The maximum length of <see cref="Action" />.</summary>
  public const int ActionMaxLength = 64;

  /// <summary>The maximum length of <see cref="ConnectionId" />: the one of the identifier of a connection.</summary>
  public const int ConnectionIdMaxLength = ForgeConnection.IdMaxLength;

  /// <summary>The maximum length of <see cref="RepositoryExternalId" />.</summary>
  public const int RepositoryExternalIdMaxLength = 64;

  /// <summary>The maximum length of <see cref="Error" />.</summary>
  public const int ErrorMaxLength = 2000;

  #endregion

  #region Properties

  /// <summary>Gets or sets the forge that sent the delivery, stored as text.</summary>
  public Forge Forge { get; set; }

  /// <summary>Gets or sets the identifier of the delivery, unique on its forge.</summary>
  public string DeliveryId { get; set; } = string.Empty;

  /// <summary>Gets or sets the organization of the delivery, or <see langword="null" /> on the lane of the instance.</summary>
  public long? OrganizationId { get; set; }

  /// <summary>Gets or sets the name of the event.</summary>
  public string Event { get; set; } = string.Empty;

  /// <summary>Gets or sets the action of the event, or <see langword="null" />.</summary>
  public string? Action { get; set; }

  /// <summary>Gets or sets the connection that the delivery names, in text, or <see langword="null" />.</summary>
  /// <remarks>On GitHub, the installation of the GitHub App.</remarks>
  public string? ConnectionId { get; set; }

  /// <summary>Gets or sets the identifier of the repository on its forge, in text, or <see langword="null" />.</summary>
  public string? RepositoryExternalId { get; set; }

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
