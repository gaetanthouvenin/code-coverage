// ------------------------------------------------------------------------------------------------
// <copyright file="ListPullRequestsQueryValidator.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using FluentValidation;

namespace CodeCoverage.Application.Features.PullRequests.Queries.ListPullRequests;

/// <summary>Validates the <see cref="ListPullRequestsQuery" /> query.</summary>
internal sealed class ListPullRequestsQueryValidator : AbstractValidator<ListPullRequestsQuery>
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="ListPullRequestsQueryValidator" /> class.</summary>
  public ListPullRequestsQueryValidator()
  {
    RuleFor(static q => q.State).Must(static s => ListPullRequestsQuery.TryParseState(s, out var _)).WithErrorCode(PullRequestErrors.InvalidState);
    RuleFor(static q => q.Page).InclusiveBetween(1, ListPullRequestsQuery.MaxPage).WithErrorCode(PullRequestErrors.InvalidPage);
    RuleFor(static q => q.PageSize).InclusiveBetween(1, ListPullRequestsQuery.MaxPageSize).WithErrorCode(PullRequestErrors.InvalidPage);
  }

  #endregion
}
