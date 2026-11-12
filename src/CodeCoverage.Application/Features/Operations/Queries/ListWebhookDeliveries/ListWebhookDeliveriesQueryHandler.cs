// ------------------------------------------------------------------------------------------------
// <copyright file="ListWebhookDeliveriesQueryHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Domain.Operations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Operations.Queries.ListWebhookDeliveries;

/// <summary>Handler for the <see cref="ListWebhookDeliveriesQuery" /> query.</summary>
/// <param name="deliveries">The webhook deliveries, an operational table read outside any organization.</param>
internal sealed class ListWebhookDeliveriesQueryHandler(IWebhookDeliveryRepository deliveries)
  : IQueryHandler<ListWebhookDeliveriesQuery, IReadOnlyList<WebhookDeliverySummary>>
{
  #region Implementation of IQueryHandler<ListWebhookDeliveriesQuery,IReadOnlyList<WebhookDeliverySummary>>

  /// <inheritdoc />
  public async ValueTask<IReadOnlyList<WebhookDeliverySummary>> Handle(ListWebhookDeliveriesQuery query, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    return await deliveries.ListRecentAsync(query.Count, cancellationToken).ConfigureAwait(false);
  }

  #endregion
}
