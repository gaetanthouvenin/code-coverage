// ------------------------------------------------------------------------------------------------
// <copyright file="ListCommitsQueryValidator.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using FluentValidation;

namespace CodeCoverage.Application.Features.Commits.Queries.ListCommits;

/// <summary>Validator for <see cref="ListCommitsQuery" />.</summary>
internal sealed class ListCommitsQueryValidator : AbstractValidator<ListCommitsQuery>
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="ListCommitsQueryValidator" /> class.</summary>
  public ListCommitsQueryValidator()
  {
    RuleFor(static q => q.Page).InclusiveBetween(1, ListCommitsQuery.MaxPage).WithErrorCode(CommitErrors.InvalidPage);
    RuleFor(static q => q.PageSize).InclusiveBetween(1, ListCommitsQuery.MaxPageSize).WithErrorCode(CommitErrors.InvalidPage);
  }

  #endregion
}
