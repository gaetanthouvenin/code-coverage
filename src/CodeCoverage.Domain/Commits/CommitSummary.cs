// ------------------------------------------------------------------------------------------------
// <copyright file="CommitSummary.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Commits;

/// <summary>A commit of a page of commits, with what the database knows of it besides its coverage.</summary>
/// <param name="Commit">The commit.</param>
/// <param name="Git">Its message, author and date from the cache of the forge, or <see langword="null" /> when it is not cached.</param>
/// <param name="Delta">
///   The change of its project coverage against its parent on the branch, from the stored push comparison (the one without
///   pull request), or <see langword="null" /> when none is stored or it has no base.
/// </param>
/// <remarks>A read model, read from the database only: nothing is asked of the forge.</remarks>
public sealed record CommitSummary(Commit Commit, GitCommit? Git, decimal? Delta);
