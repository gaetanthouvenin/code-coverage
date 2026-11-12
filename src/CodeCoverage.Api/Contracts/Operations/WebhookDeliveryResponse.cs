// ------------------------------------------------------------------------------------------------
// <copyright file="WebhookDeliveryResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Contracts.Operations;

/// <summary>A webhook delivery: metadata only, never its body nor the text of its error.</summary>
/// <param name="DeliveryId">The identifier GitHub gives the delivery.</param>
/// <param name="Event">The name of the event.</param>
/// <param name="Action">The action of the event, or <see langword="null" />.</param>
/// <param name="OrganizationId">The organization of the delivery, or <see langword="null" />.</param>
/// <param name="InstallationId">The installation, or <see langword="null" />.</param>
/// <param name="RepositoryGitHubId">The identifier of the repository on GitHub, or <see langword="null" />.</param>
/// <param name="ReceivedAt">When it was received, in UTC.</param>
/// <param name="ProcessedAt">When it was handled, or <see langword="null" />.</param>
/// <param name="HasError">Whether it could not be handled.</param>
public sealed record WebhookDeliveryResponse(
  string DeliveryId,
  string Event,
  string? Action,
  long? OrganizationId,
  long? InstallationId,
  long? RepositoryGitHubId,
  DateTimeOffset ReceivedAt,
  DateTimeOffset? ProcessedAt,
  bool HasError);
