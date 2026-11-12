// ------------------------------------------------------------------------------------------------
// <copyright file="ListJobsQueryValidator.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using FluentValidation;

namespace CodeCoverage.Application.Features.Operations.Queries.ListJobs;

/// <summary>Validator for <see cref="ListJobsQuery" />.</summary>
internal sealed class ListJobsQueryValidator : AbstractValidator<ListJobsQuery>
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="ListJobsQueryValidator" /> class.</summary>
  public ListJobsQueryValidator()
  {
    RuleFor(static q => q.Page).InclusiveBetween(1, ListJobsQuery.MaxPage).WithErrorCode(OperationsErrors.InvalidPage);
    RuleFor(static q => q.PageSize).InclusiveBetween(1, ListJobsQuery.MaxPageSize).WithErrorCode(OperationsErrors.InvalidPage);
  }

  #endregion
}
