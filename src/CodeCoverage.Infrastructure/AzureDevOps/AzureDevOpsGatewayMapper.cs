// ------------------------------------------------------------------------------------------------
// <copyright file="AzureDevOpsGatewayMapper.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Text.Json;

using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Git;
using CodeCoverage.Infrastructure.AzureDevOps.Models;

namespace CodeCoverage.Infrastructure.AzureDevOps;

/// <summary>Converts the payloads of the Azure DevOps REST API into the contracts of the forge port.</summary>
/// <remarks>A value the port cannot carry raises a <see cref="JsonException" />: the payload is unreadable.</remarks>
internal static class AzureDevOpsGatewayMapper
{
  #region Fields

  /// <summary>The prefix of the full name of a branch.</summary>
  public const string BranchPrefix = "refs/heads/";

  /// <summary>
  ///   The branch an empty repository is given, which Azure DevOps reports without a default branch: the one Azure
  ///   DevOps creates by default, replaced by the real one at the next synchronization.
  /// </summary>
  public const string EmptyRepositoryBranch = "main";

  #endregion

  #region Methods

  /// <summary>Converts a repository.</summary>
  /// <param name="repository">The repository.</param>
  /// <param name="organization">The name of the Azure DevOps organization that holds it, the owner of the port.</param>
  /// <param name="organizationId">The identifier of the Azure DevOps organization, in lower case.</param>
  /// <returns>The repository of the port, its project in the namespace.</returns>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  /// <remarks>A repository is private unless its project is public, the visibility Azure DevOps sets on the project.</remarks>
  public static ForgeRepositoryInfo ToForgeRepository(this AzureDevOpsRepository repository, string organization, string organizationId)
  {
    ArgumentNullException.ThrowIfNull(repository);
    ArgumentNullException.ThrowIfNull(organization);
    ArgumentNullException.ThrowIfNull(organizationId);

    return new ForgeRepositoryInfo(
      ToText(repository.Id),
      organization,
      repository.Project.Name,
      repository.Name,
      repository.DefaultBranch is { Length: > 0 } branch ? ToBranchName(branch) : EmptyRepositoryBranch,
      !string.Equals(repository.Project.Visibility, "public", StringComparison.OrdinalIgnoreCase),
      organizationId
    );
  }

  /// <summary>Converts a commit.</summary>
  /// <param name="commit">The commit.</param>
  /// <returns>
  ///   The commit of the port. Git records no account of the forge, so the login of its author is
  ///   <see langword="null" />.
  /// </returns>
  /// <exception cref="ArgumentNullException"><paramref name="commit" /> is <see langword="null" />.</exception>
  /// <exception cref="JsonException">A SHA is malformed, or the commit has no date.</exception>
  public static ForgeCommitInfo ToForgeCommit(this AzureDevOpsCommit commit)
  {
    ArgumentNullException.ThrowIfNull(commit);

    return new ForgeCommitInfo(
      ToSha(commit.CommitId),
      commit.Parents is null ? [] : [.. commit.Parents.Select(ToSha)],
      commit.Comment ?? string.Empty,
      null,
      commit.Committer?.Date
      ?? commit.Author?.Date
      ?? throw new JsonException($"The commit '{commit.CommitId}' has no date.")
    );
  }

  /// <summary>Converts a pull request.</summary>
  /// <param name="pull">The pull request.</param>
  /// <returns>
  ///   The pull request of the port, whose head is <c>lastMergeSourceCommit</c>, the head of the source branch, never
  ///   the merge commit Azure DevOps computes.
  /// </returns>
  /// <exception cref="ArgumentNullException"><paramref name="pull" /> is <see langword="null" />.</exception>
  /// <exception cref="JsonException">A SHA is malformed, or the status is unknown.</exception>
  public static ForgePullInfo ToForgePull(this AzureDevOpsPullRequest pull)
  {
    ArgumentNullException.ThrowIfNull(pull);

    return new ForgePullInfo(
      pull.PullRequestId,
      ToForgePullState(pull.Status),
      pull.Title,
      pull.CreatedBy?.UniqueName,
      ToSha(pull.LastMergeSourceCommit.CommitId),
      ToBranchName(pull.SourceRefName),
      ToBranchName(pull.TargetRefName),
      ToSha(pull.LastMergeTargetCommit.CommitId),
      pull.LastMergeCommit is { } merge ? ToSha(merge.CommitId) : null
    );
  }

  /// <summary>Reads the head of a branch among the references Azure DevOps listed for it.</summary>
  /// <param name="refs">The references whose name starts with the one of the branch.</param>
  /// <param name="branch">The name of the branch.</param>
  /// <returns>The head of the branch, or <see langword="null" /> when no reference is the branch itself.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="refs" /> or <paramref name="branch" /> is <see langword="null" />.</exception>
  /// <exception cref="JsonException">The SHA of the branch is malformed.</exception>
  /// <remarks>
  ///   The filter of Azure DevOps is a prefix: <c>heads/main</c> also lists <c>heads/main-old</c>. The names are
  ///   compared without regard to case, as Azure DevOps compares the names of branches.
  /// </remarks>
  public static GitSha? ToBranchHead(this AzureDevOpsList<AzureDevOpsRef> refs, string branch)
  {
    ArgumentNullException.ThrowIfNull(refs);
    ArgumentNullException.ThrowIfNull(branch);

    var name = BranchPrefix + branch;
    var head = refs.Value.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));

    return head is null ? null : ToSha(head.ObjectId);
  }

  /// <summary>Removes the prefix of the full name of a branch.</summary>
  /// <param name="refName">The full name, <c>refs/heads/main</c>.</param>
  /// <returns>The name of the branch, <c>main</c>; a name without the prefix is returned as it is.</returns>
  private static string ToBranchName(string refName) =>
    refName.StartsWith(BranchPrefix, StringComparison.Ordinal) ? refName[BranchPrefix.Length..] : refName;

  /// <summary>Converts the status of a pull request.</summary>
  /// <param name="status">The status Azure DevOps reports.</param>
  /// <returns>The state of the port.</returns>
  /// <exception cref="JsonException">The status is unknown.</exception>
  private static ForgePullState ToForgePullState(string status) =>
    status switch
    {
      "active" => ForgePullState.Open,
      "completed" => ForgePullState.Merged,
      "abandoned" => ForgePullState.Closed,
      _ => throw new JsonException($"The pull request status '{status}' is unknown."),
    };

  /// <summary>Reads a SHA of a payload.</summary>
  /// <param name="value">The text of the SHA.</param>
  /// <returns>The SHA.</returns>
  /// <exception cref="JsonException">The text is not a full SHA.</exception>
  private static GitSha ToSha(string value) =>
    GitSha.TryParse(value, out var sha) ? sha : throw new JsonException($"'{value}' is not a full SHA.");

  /// <summary>Writes an identifier of Azure DevOps as the text the port carries.</summary>
  /// <param name="id">The identifier.</param>
  /// <returns>The text, in lower case and without braces.</returns>
  private static string ToText(Guid id) => id.ToString("D", CultureInfo.InvariantCulture);

  #endregion
}
