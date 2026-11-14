// ------------------------------------------------------------------------------------------------
// <copyright file="CodecovCommit.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Git;

namespace CodeCoverage.Domain.Contracts.Codecov;

/// <summary>A commit of the history of a repository, as the API v2 of Codecov reports it.</summary>
/// <param name="Sha">The SHA of the commit.</param>
/// <param name="Branch">The branch Codecov recorded for the commit.</param>
/// <param name="Timestamp">When the commit was made, as Codecov recorded it.</param>
/// <param name="Files">The number of files of its report.</param>
/// <param name="Totals">
///   The counters of its report: the coverage is computed again from them by the formula of Codecov, never copied.
/// </param>
public sealed record CodecovCommit(GitSha Sha, string Branch, DateTimeOffset Timestamp, int Files, CoverageTotals Totals);
