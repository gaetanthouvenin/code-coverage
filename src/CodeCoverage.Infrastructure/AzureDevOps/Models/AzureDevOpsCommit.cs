// ------------------------------------------------------------------------------------------------
// <copyright file="AzureDevOpsCommit.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.AzureDevOps.Models;

/// <summary>The answer of <c>GET _apis/git/repositories/{repositoryId}/commits/{commitId}</c>.</summary>
/// <param name="CommitId">The SHA of the commit.</param>
/// <param name="Parents">The SHAs of its parents, the first parent first, or <see langword="null" /> when not reported.</param>
/// <param name="Comment">The message of the commit, or <see langword="null" />.</param>
/// <param name="Author">The author of the commit, or <see langword="null" />.</param>
/// <param name="Committer">The committer of the commit, or <see langword="null" />.</param>
/// <param name="TreeId">The SHA of the root tree of the commit, or <see langword="null" /> when not reported.</param>
/// <remarks>The listing of the commits leaves out the parents and cuts the message: only this reading carries both.</remarks>
internal sealed record AzureDevOpsCommit(
  string CommitId,
  IReadOnlyList<string>? Parents = null,
  string? Comment = null,
  AzureDevOpsGitUser? Author = null,
  AzureDevOpsGitUser? Committer = null,
  string? TreeId = null);
