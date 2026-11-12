// ------------------------------------------------------------------------------------------------
// <copyright file="ListAuditEntriesQueryValidator.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using FluentValidation;

namespace CodeCoverage.Application.Features.Operations.Queries.ListAuditEntries;

/// <summary>Validates the <see cref="ListAuditEntriesQuery" /> query: the page, its size and the search are bounded.</summary>
internal sealed class ListAuditEntriesQueryValidator : AbstractValidator<ListAuditEntriesQuery>
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="ListAuditEntriesQueryValidator" /> class.</summary>
  public ListAuditEntriesQueryValidator()
  {
    RuleFor(static q => q.Page).InclusiveBetween(1, ListAuditEntriesQuery.MaxPage).WithErrorCode(OperationsErrors.InvalidAuditPage);
    RuleFor(static q => q.PageSize).InclusiveBetween(1, ListAuditEntriesQuery.MaxPageSize).WithErrorCode(OperationsErrors.InvalidAuditPage);
    RuleFor(static q => q.Search).MaximumLength(ListAuditEntriesQuery.SearchMaxLength).WithErrorCode(OperationsErrors.InvalidAuditSearch);
    RuleFor(static q => q.Category).IsInEnum().WithErrorCode(OperationsErrors.InvalidAuditSearch);
  }

  #endregion
}
