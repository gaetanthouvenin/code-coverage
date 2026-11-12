// ------------------------------------------------------------------------------------------------
// <copyright file="ListWebhookDeliveriesQueryValidator.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using FluentValidation;

namespace CodeCoverage.Application.Features.Operations.Queries.ListWebhookDeliveries;

/// <summary>Validator for <see cref="ListWebhookDeliveriesQuery" />.</summary>
internal sealed class ListWebhookDeliveriesQueryValidator : AbstractValidator<ListWebhookDeliveriesQuery>
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="ListWebhookDeliveriesQueryValidator" /> class.</summary>
  public ListWebhookDeliveriesQueryValidator()
  {
    RuleFor(static q => q.Count).InclusiveBetween(1, ListWebhookDeliveriesQuery.MaxCount).WithErrorCode(OperationsErrors.InvalidPage);
  }

  #endregion
}
