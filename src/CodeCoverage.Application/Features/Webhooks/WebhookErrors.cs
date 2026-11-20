// ------------------------------------------------------------------------------------------------
// <copyright file="WebhookErrors.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Application.Features.Webhooks;

/// <summary>The error codes of the webhooks of the forges with connections.</summary>
public static class WebhookErrors
{
  #region Fields

  /// <summary>The delivery to handle is not stored, names no connection, or belongs to another organization.</summary>
  public const string DeliveryNotFound = "Webhook.DeliveryNotFound";

  /// <summary>
  ///   The connection of the route is unknown, or no secret of it authenticates the delivery: both get this code, so
  ///   the route tells nothing of the connections that exist (ADR-017).
  /// </summary>
  public const string InvalidSignature = "Webhook.InvalidSignature";

  /// <summary>The body of the delivery is missing or is not a JSON object.</summary>
  public const string InvalidPayload = "Webhook.InvalidPayload";

  /// <summary>The identifier, the event or the headers of the delivery are missing, or a text is too long.</summary>
  public const string InvalidDelivery = "Webhook.InvalidDelivery";

  #endregion
}
