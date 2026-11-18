// ------------------------------------------------------------------------------------------------
// <copyright file="AzureDevOpsPullRequest.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.AzureDevOps.Models;

/// <summary>The answer of <c>GET _apis/git/repositories/{repositoryId}/pullrequests/{pullRequestId}</c>.</summary>
/// <param name="PullRequestId">The number of the pull request.</param>
/// <param name="Status">The status of the pull request: <c>active</c>, <c>completed</c> or <c>abandoned</c>.</param>
/// <param name="Title">The title of the pull request.</param>
/// <param name="SourceRefName">The full name of the branch of its head, <c>refs/heads/feature</c>.</param>
/// <param name="TargetRefName">The full name of the branch it targets.</param>
/// <param name="LastMergeSourceCommit">
///   The head of the source branch at the last merge Azure DevOps computed: the head of the pull request.
/// </param>
/// <param name="LastMergeTargetCommit">The head of the target branch at the last merge Azure DevOps computed.</param>
/// <param name="CreatedBy">The author of the pull request, or <see langword="null" />.</param>
/// <param name="LastMergeCommit">The synthetic merge commit, or <see langword="null" /> when Azure DevOps computed none.</param>
internal sealed record AzureDevOpsPullRequest(
  int PullRequestId,
  string Status,
  string Title,
  string SourceRefName,
  string TargetRefName,
  AzureDevOpsCommitRef LastMergeSourceCommit,
  AzureDevOpsCommitRef LastMergeTargetCommit,
  AzureDevOpsIdentity? CreatedBy = null,
  AzureDevOpsCommitRef? LastMergeCommit = null);
