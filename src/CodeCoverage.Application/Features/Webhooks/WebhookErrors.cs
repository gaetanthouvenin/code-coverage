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

  #endregion
}
