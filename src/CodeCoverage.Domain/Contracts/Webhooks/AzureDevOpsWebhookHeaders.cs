// ------------------------------------------------------------------------------------------------
// <copyright file="AzureDevOpsWebhookHeaders.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Contracts.Webhooks;

/// <summary>The headers of an Azure DevOps service hook that the reception reads (ADR-014).</summary>
/// <remarks>
///   Azure DevOps does not sign its service hooks with an HMAC, a named difference of ADR-014: each subscription sends
///   the webhook secret of its connection in <see cref="Secret" />, which the route reads and the validator of the forge
///   compares in constant time.
/// </remarks>
public static class AzureDevOpsWebhookHeaders
{
  #region Fields

  /// <summary>The header that carries the webhook secret of the connection, set on each subscription.</summary>
  public const string Secret = "X-CodeCoverage-Webhook-Secret";

  #endregion
}
