// ------------------------------------------------------------------------------------------------
// <copyright file="CodecovCommitPage.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Contracts.Codecov;

/// <summary>A page of the commits of a repository on Codecov.</summary>
/// <param name="Commits">
///   The commits of the page that have a complete report, a branch and totals; the others are left out.
/// </param>
/// <param name="HasNext">Whether another page follows.</param>
public sealed record CodecovCommitPage(IReadOnlyList<CodecovCommit> Commits, bool HasNext);
