// ------------------------------------------------------------------------------------------------
// <copyright file="AzureDevOpsEventTranslator.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.Json;

using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Ports;

using static CodeCoverage.Infrastructure.AzureDevOps.Events.AzureDevOpsServiceHookPayload;

namespace CodeCoverage.Infrastructure.AzureDevOps.Events;

/// <summary>Reads the service hooks of Azure DevOps into neutral events.</summary>
/// <remarks>
///   <para>The events this application follows, named by the <c>eventType</c> the delivery was stored with:</para>
///   <list type="bullet">
///     <item>
///       <c>git.push</c> gives a <see cref="ForgePushEvent" /> for the default branch when the push updates it, else
///       for the first branch it updates; a push of tags only is ignored. The body lists no changed file, so a push that keeps its branch may always have changed the
///       configuration file;
///     </item>
///     <item>
///       <c>git.pullrequest.created</c>, <c>git.pullrequest.updated</c> when a push updated its source branch
///       (<c>PushNotification</c>) and <c>git.pullrequest.merged</c> when the merge succeeded give a
///       <see cref="ForgePullRequestEvent" />;
///     </item>
///     <item>
///       <c>git.repo.created</c>, <c>git.repo.renamed</c> and <c>git.repo.deleted</c> give a
///       <see cref="ForgeRepositoryEvent" />.
///     </item>
///   </list>
///   <para>
///     Azure DevOps does not write the notification type of <c>git.pullrequest.updated</c> in the body: the subscription
///     filters it, and the translator also reads the message Azure DevOps writes for a push to the source branch, so an
///     update of the reviewers, of a vote or of the status is ignored even when another subscription sends it.
///   </para>
///   <para>
///     The body is <c>Minimal</c> and never believed (ADR-014): only identifiers are read from it, the state is read
///     again from the API. Any other event, or a body that lacks what its event needs, is ignored, never a failure.
///   </para>
/// </remarks>
internal sealed class AzureDevOpsEventTranslator : IForgeEventTranslator
{
  #region Fields

  /// <summary>The event of a push.</summary>
  public const string PushEvent = "git.push";

  /// <summary>The event of a new pull request.</summary>
  public const string PullRequestCreatedEvent = "git.pullrequest.created";

  /// <summary>The event of an updated pull request.</summary>
  public const string PullRequestUpdatedEvent = "git.pullrequest.updated";

  /// <summary>The event of a merge attempt of a pull request.</summary>
  public const string PullRequestMergedEvent = "git.pullrequest.merged";

  /// <summary>The event of a new repository.</summary>
  public const string RepositoryCreatedEvent = "git.repo.created";

  /// <summary>The event of a renamed repository.</summary>
  public const string RepositoryRenamedEvent = "git.repo.renamed";

  /// <summary>The event of a deleted repository.</summary>
  public const string RepositoryDeletedEvent = "git.repo.deleted";

  /// <summary>The words of the message Azure DevOps writes when a push updates the source branch of a pull request.</summary>
  public const string SourceBranchUpdatedMessage = "updated the source branch";

  /// <summary>The merge status of a merge attempt that succeeded.</summary>
  private const string MergeSucceeded = "succeeded";

  /// <summary>The object of a reference that does not exist, before its creation or after its deletion.</summary>
  private const string NullObjectId = "0000000000000000000000000000000000000000";

  #endregion

  #region Methods

  /// <summary>Reads a push.</summary>
  /// <param name="payload">The body.</param>
  /// <returns>The push of its default branch or of its first branch, or <see langword="null" />.</returns>
  /// <remarks>
  ///   A push that does not say where the branch was is translated without its previous head: the branch is then read
  ///   again from the API.
  /// </remarks>
  private static ForgePushEvent? ToPush(AzureDevOpsServiceHookPayload payload)
  {
    var resource = payload.Resource;

    if (RepositoryId(resource) is not { } repositoryId || GetArray(resource, "refUpdates") is not { } updates)
    {
      return null;
    }

    if (ChooseBranchUpdate(updates, GetObject(resource, "repository") is { } repository ? GetString(repository, "defaultBranch") : null)
        is not var (branch, update))
    {
      return null;
    }

    var oldObjectId = GetString(update, "oldObjectId");
    var newObjectId = GetString(update, "newObjectId");
    var deleted = newObjectId is NullObjectId;
    GitSha? head = null;
    GitSha? before = null;

    if ((!deleted && !GitSha.TryParse(newObjectId, out head))
        || (oldObjectId is not (null or NullObjectId) && !GitSha.TryParse(oldObjectId, out before)))
    {
      return null;
    }

    return new ForgePushEvent(repositoryId, branch[AzureDevOpsGatewayMapper.BranchPrefix.Length..], head, deleted, !deleted, before);
  }

  /// <summary>Chooses the branch a push is translated for.</summary>
  /// <param name="updates">The <c>refUpdates</c> of the push.</param>
  /// <param name="defaultBranch">The reference of the default branch of the repository, or <see langword="null" />.</param>
  /// <returns>
  ///   The reference and the update of the default branch when the push updated it, else of the first branch it updated,
  ///   or <see langword="null" /> when it updated no branch.
  /// </returns>
  /// <remarks>
  ///   One push can update several branches: the default branch comes first, so its head and its configuration file are
  ///   never left behind by an update of another branch listed before it.
  /// </remarks>
  private static (string Branch, JsonElement Update)? ChooseBranchUpdate(JsonElement updates, string? defaultBranch)
  {
    (string Branch, JsonElement Update)? chosen = null;

    foreach (var update in updates.EnumerateArray())
    {
      if (GetString(update, "name") is not { } name || !name.StartsWith(AzureDevOpsGatewayMapper.BranchPrefix, StringComparison.Ordinal))
      {
        continue;
      }

      if (string.Equals(name, defaultBranch, StringComparison.OrdinalIgnoreCase))
      {
        return (name, update);
      }

      chosen ??= (name, update);
    }

    return chosen;
  }

  /// <summary>Reads a change of a pull request.</summary>
  /// <param name="payload">The body.</param>
  /// <returns>The pull request, or <see langword="null" />.</returns>
  private static ForgePullRequestEvent? ToPullRequest(AzureDevOpsServiceHookPayload payload) =>
    RepositoryId(payload.Resource) is { } repositoryId && GetPositiveInt32(payload.Resource, "pullRequestId") is { } number
      ? new ForgePullRequestEvent(repositoryId, number)
      : null;

  /// <summary>Reads a change of a repository.</summary>
  /// <param name="payload">The body.</param>
  /// <param name="kind">What happened to the repository.</param>
  /// <returns>The change, or <see langword="null" /> when the body does not name the repository.</returns>
  /// <remarks>
  ///   A deleted repository is described by <c>repositoryId</c>, <c>repositoryName</c> and <c>project</c>, the others by
  ///   a <c>repository</c> object. The body names the Azure DevOps organization by its identifier only: the login of the
  ///   owner stays empty, and the reconciliation reads it from the API.
  /// </remarks>
  private static ForgeRepositoryEvent? ToRepository(AzureDevOpsServiceHookPayload payload, ForgeRepositoryEventKind kind)
  {
    var resource = payload.Resource;

    if (RepositoryId(resource) is not { } repositoryId)
    {
      return null;
    }

    var repository = GetObject(resource, "repository");
    var described = repository ?? resource;
    var project = GetObject(described, "project");
    var defaultBranch = repository is { } read ? GetString(read, "defaultBranch") : null;

    return new ForgeRepositoryEvent(
      kind,
      new ForgeRepositoryInfo(
        repositoryId,
        string.Empty,
        project is { } p ? GetString(p, "name") ?? string.Empty : string.Empty,
        GetString(described, repository is null ? "repositoryName" : "name") ?? string.Empty,
        defaultBranch is { Length: > 0 } ? AzureDevOpsGatewayMapper.ToBranchName(defaultBranch) : AzureDevOpsGatewayMapper.EmptyRepositoryBranch,
        !(project is { } visible && string.Equals(GetString(visible, "visibility"), "public", StringComparison.OrdinalIgnoreCase)),
        payload.AccountId
      )
    );
  }

  /// <summary>Reads the identifier of the repository of a resource.</summary>
  /// <param name="resource">The resource.</param>
  /// <returns>
  ///   The identifier of its <c>repository</c> object, or of its <c>repositoryId</c> member when it has none, or
  ///   <see langword="null" /> when neither is an identifier.
  /// </returns>
  private static string? RepositoryId(JsonElement resource) =>
    ToIdentifier(GetObject(resource, "repository") is { } repository ? GetString(repository, "id") : GetString(resource, "repositoryId"));

  #endregion

  #region Implementation of IForgeEventTranslator

  /// <inheritdoc />
  public ForgeEvent? Translate(ForgeDelivery delivery)
  {
    ArgumentNullException.ThrowIfNull(delivery);

    if (Read(delivery.Payload) is not { } payload)
    {
      return null;
    }

    return delivery.Event switch
    {
      PushEvent => ToPush(payload),
      PullRequestCreatedEvent => ToPullRequest(payload),
      PullRequestUpdatedEvent when payload.MessageText?.Contains(SourceBranchUpdatedMessage, StringComparison.OrdinalIgnoreCase) is true =>
        ToPullRequest(payload),
      PullRequestMergedEvent when string.Equals(GetString(payload.Resource, "mergeStatus"), MergeSucceeded, StringComparison.OrdinalIgnoreCase) =>
        ToPullRequest(payload),
      RepositoryCreatedEvent => ToRepository(payload, ForgeRepositoryEventKind.Created),
      RepositoryRenamedEvent => ToRepository(payload, ForgeRepositoryEventKind.Renamed),
      RepositoryDeletedEvent => ToRepository(payload, ForgeRepositoryEventKind.Deleted),
      _ => null,
    };
  }

  #endregion
}
