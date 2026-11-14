// ------------------------------------------------------------------------------------------------
// <copyright file="CodecovCommitModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.Codecov.Models;

/// <summary>A commit of the API v2 of Codecov.</summary>
/// <param name="Commitid">The SHA of the commit.</param>
/// <param name="Timestamp">When the commit was made.</param>
/// <param name="Branch">The branch Codecov recorded, or <see langword="null" />.</param>
/// <param name="State">The state of its report: <c>complete</c> once processed.</param>
/// <param name="Totals">The totals of its report, or <see langword="null" /> without one.</param>
internal sealed record CodecovCommitModel(
  string Commitid,
  DateTimeOffset Timestamp,
  string? Branch = null,
  string? State = null,
  CodecovTotalsModel? Totals = null);
