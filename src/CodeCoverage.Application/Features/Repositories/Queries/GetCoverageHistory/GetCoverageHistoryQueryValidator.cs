// ------------------------------------------------------------------------------------------------
// <copyright file="GetCoverageHistoryQueryValidator.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using FluentValidation;

using CodeCoverage.Domain.Commits;

namespace CodeCoverage.Application.Features.Repositories.Queries.GetCoverageHistory;

/// <summary>Validates the <see cref="GetCoverageHistoryQuery" /> query.</summary>
internal sealed class GetCoverageHistoryQueryValidator : AbstractValidator<GetCoverageHistoryQuery>
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="GetCoverageHistoryQueryValidator" /> class.</summary>
  public GetCoverageHistoryQueryValidator()
  {
    RuleFor(static q => q.Interval).Must(static i => GetCoverageHistoryQuery.TryParseInterval(i, out var _)).WithErrorCode(RepositoryErrors.InvalidHistory);
    RuleFor(static q => q.Branch).MaximumLength(Commit.BranchMaxLength).WithErrorCode(RepositoryErrors.InvalidHistory);
    RuleFor(static q => q.Flags).NotNull().Must(static f => f.Count <= GetCoverageHistoryQuery.MaxFlags).WithErrorCode(RepositoryErrors.InvalidHistory);
    RuleForEach(static q => q.Flags).NotNull().MaximumLength(CommitFlagTotals.FlagMaxLength).WithErrorCode(RepositoryErrors.InvalidHistory);
    RuleFor(static q => q)
      .Must(static q => q.From is not { } from || q.To is not { } to || from <= to)
      .WithErrorCode(RepositoryErrors.InvalidHistory)
      .WithName("From");
  }

  #endregion
}
