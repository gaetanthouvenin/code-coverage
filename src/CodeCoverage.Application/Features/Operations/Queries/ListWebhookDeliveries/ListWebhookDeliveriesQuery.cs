// ------------------------------------------------------------------------------------------------
// <copyright file="ListWebhookDeliveriesQuery.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Operations;

namespace CodeCoverage.Application.Features.Operations.Queries.ListWebhookDeliveries;

/// <summary>Lists the latest webhook deliveries, without their body nor the text of their error.</summary>
/// <param name="Count">How many deliveries, 1 to <see cref="MaxCount" />.</param>
public sealed record ListWebhookDeliveriesQuery(int Count = ListWebhookDeliveriesQuery.DefaultCount) : IQuery<IReadOnlyList<WebhookDeliverySummary>>, IAdminOnly
{
  #region Fields

  /// <summary>The number of deliveries when none is asked.</summary>
  public const int DefaultCount = 50;

  /// <summary>The largest number that can be asked.</summary>
  public const int MaxCount = 200;

  #endregion
}
