// ------------------------------------------------------------------------------------------------
// <copyright file="WebhookDeliverySummary.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Domain.Operations;

/// <summary>A webhook delivery as the operations console shows it, without its body.</summary>
/// <param name="Forge">The forge that sent the delivery.</param>
/// <param name="DeliveryId">The identifier of the delivery, unique on its forge.</param>
/// <param name="Event">The name of the event.</param>
/// <param name="Action">The action of the event, or <see langword="null" />.</param>
/// <param name="OrganizationId">The organization of the delivery, or <see langword="null" /> on the lane of the instance.</param>
/// <param name="ConnectionId">
///   The connection that the delivery names, in text, or <see langword="null" />; on GitHub, the installation.
/// </param>
/// <param name="RepositoryExternalId">The identifier of the repository on its forge, in text, or <see langword="null" />.</param>
/// <param name="ReceivedAt">When it was received, in UTC.</param>
/// <param name="ProcessedAt">When it was handled, in UTC, or <see langword="null" />.</param>
/// <param name="HasError">Whether it could not be handled; the text of the error is not exposed, it may hold data.</param>
public sealed record WebhookDeliverySummary(
  Forge Forge,
  string DeliveryId,
  string Event,
  string? Action,
  long? OrganizationId,
  string? ConnectionId,
  string? RepositoryExternalId,
  DateTimeOffset ReceivedAt,
  DateTimeOffset? ProcessedAt,
  bool HasError);
