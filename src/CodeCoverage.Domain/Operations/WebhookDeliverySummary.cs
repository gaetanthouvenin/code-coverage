// ------------------------------------------------------------------------------------------------
// <copyright file="WebhookDeliverySummary.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Operations;

/// <summary>A webhook delivery as the operations console shows it, without its body.</summary>
/// <param name="DeliveryId">The identifier of the delivery.</param>
/// <param name="Event">The name of the event.</param>
/// <param name="Action">The action of the event, or <see langword="null" />.</param>
/// <param name="OrganizationId">The organization of the delivery, or <see langword="null" /> on the lane of the instance.</param>
/// <param name="InstallationId">The installation, or <see langword="null" />.</param>
/// <param name="RepositoryGitHubId">The identifier of the repository on GitHub, or <see langword="null" />.</param>
/// <param name="ReceivedAt">When it was received, in UTC.</param>
/// <param name="ProcessedAt">When it was handled, in UTC, or <see langword="null" />.</param>
/// <param name="Error">Why it could not be handled, or <see langword="null" />.</param>
public sealed record WebhookDeliverySummary(
  string DeliveryId,
  string Event,
  string? Action,
  long? OrganizationId,
  long? InstallationId,
  long? RepositoryGitHubId,
  DateTimeOffset ReceivedAt,
  DateTimeOffset? ProcessedAt,
  string? Error);
