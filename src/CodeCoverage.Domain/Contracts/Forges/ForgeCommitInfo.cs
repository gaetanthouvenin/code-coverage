// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeCommitInfo.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Git;

namespace CodeCoverage.Domain.Contracts.Forges;

/// <summary>What a forge reports about a commit.</summary>
/// <param name="Sha">The SHA of the commit.</param>
/// <param name="ParentShas">The SHAs of its parents, the first parent first.</param>
/// <param name="Message">The message of the commit.</param>
/// <param name="AuthorLogin">
///   The login of its author, or <see langword="null" /> when the author matches no account of the forge.
/// </param>
/// <param name="CommittedAt">When it was committed, according to its committer.</param>
public sealed record ForgeCommitInfo(
  GitSha Sha,
  IReadOnlyList<GitSha> ParentShas,
  string Message,
  string? AuthorLogin,
  DateTimeOffset CommittedAt);
