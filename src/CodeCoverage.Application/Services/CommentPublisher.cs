// ------------------------------------------------------------------------------------------------
// <copyright file="CommentPublisher.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using CodeCoverage.Application.Configuration;
using CodeCoverage.Application.Services.Comments;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Paths;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.PullRequests;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Services;

/// <summary>Publishes the one comment of a pull request and keeps it up to date (ARCHITECTURE §8.4).</summary>
/// <param name="gateways">The gateways of the forges.</param>
/// <param name="pullRequests">The repository of the pull requests.</param>
/// <param name="notifications">The settings of the notifications, read at each use.</param>
/// <param name="urls">The public addresses of the instance, read at each use.</param>
/// <param name="clock">The clock.</param>
/// <param name="logger">The logger.</param>
/// <remarks>
///   <para>
///     A pull request has one comment at most. Its identifier is kept on the pull request; when it is unknown, after
///     a restoration of the database or a save that lost a race, or when the comment it names was deleted, the
///     comments of the pull request are searched for the marker, among those this application wrote, before any new
///     one is written. The forge is always called first and the pull request saved after: a save that fails leaves a
///     comment the next run finds.
///   </para>
///   <para>
///     What the head was compared to is recorded on the pull request in the same save, whether a comment is
///     written or not: a base that receives its report later finds the pull requests to notify again by it.
///   </para>
/// </remarks>
internal sealed partial class CommentPublisher(
  IForgeGatewayProvider gateways,
  IPullRequestRepository pullRequests,
  IOptionsMonitor<NotificationOptions> notifications,
  IOptionsMonitor<UrlOptions> urls,
  IClock clock,
  ILogger<CommentPublisher> logger)
{
  #region Methods

  /// <summary>Publishes or updates the comment of the pull request a commit is the head of.</summary>
  /// <param name="repository">The repository.</param>
  /// <param name="forge">The forge of the repository.</param>
  /// <param name="commit">The commit that is notified.</param>
  /// <param name="config">The configuration of the commit.</param>
  /// <param name="context">The comparison of the commit with its base.</param>
  /// <param name="failedUploads">The number of uploads of the commit that failed.</param>
  /// <param name="configInvalid">Whether the configuration file of the commit was ignored.</param>
  /// <param name="enabled">Whether the organization publishes comments (<c>Notifications:CommentsEnabled</c>).</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>What became of the comment.</returns>
  /// <remarks>
  ///   A commit that is not the head of an open pull request never comments: a comment always describes the head.
  /// </remarks>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  public async Task<CommentOutcome> PublishAsync(
    TrackedRepository repository,
    Forge forge,
    Commit commit,
    CoverageConfig config,
    ComparisonContext context,
    int failedUploads,
    bool configInvalid,
    bool enabled,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);
    ArgumentNullException.ThrowIfNull(commit);
    ArgumentNullException.ThrowIfNull(config);
    ArgumentNullException.ThrowIfNull(context);

    // The pull request the base resolution read from the forge: its head tells whether this commit is stale.
    if (context.Resolution.PullRequest is not { } pull || pull.HeadSha != commit.Sha)
    {
      return CommentOutcome.Skipped;
    }

    var version = pull.Version;
    var outcome = CommentOutcome.Skipped;

    if (enabled && Accepts(pull, config.Comment, context))
    {
      var content = new CommentContent(
        CommentComposer.Marker,
        notifications.CurrentValue.CommentTitle,
        config,
        commit,
        pull,
        context,
        commit.Files,
        context.Resolution.ComparedTo?.Files,
        failedUploads,
        new WebLinks(urls.CurrentValue.WebPublicUrl, forge, repository),
        WrapDetails: true,
        configInvalid
      );

      outcome = await WriteAsync(repository.ToForgeRef(forge), pull, commit, config, context, CommentComposer.Compose(content), cancellationToken)
                  .ConfigureAwait(false);

      LogOutcome(logger, pull.Number, commit.Id, outcome);
    }

    var comparison = context.Comparison;

    pull.RecordBase(comparison.ForkPointSha, comparison.Result.Base is null ? null : comparison.ComparedToSha, clock.UtcNow);

    if (pull.Version != version)
    {
      await pullRequests.UpdateAsync(pull, cancellationToken).ConfigureAwait(false);
    }

    return outcome;
  }

  /// <summary>Tells whether the rule of the comment lets a pull request be commented, whatever the comment says.</summary>
  /// <param name="pull">The pull request.</param>
  /// <param name="rule">The rule of the comment.</param>
  /// <param name="context">The comparison of the head with its base.</param>
  /// <returns><see langword="true" /> when the comment may be written or updated.</returns>
  private static bool Accepts(PullRequest pull, CommentRule rule, ComparisonContext context)
  {
    // The sessions of the report, those carried forward included, as Codecov counts them for the comment.
    if (!pull.AcceptsComments || !rule.Enabled || context.HeadSessions.Count < rule.AfterNBuilds)
    {
      return false;
    }

    // The branches of the rule are those the pull request targets.
    return rule.Branches.Count == 0 || new PathPatternSet(rule.Branches).Includes(pull.BaseBranch);
  }

  /// <summary>Tells whether a body is one this application wrote: the marker is its first line.</summary>
  /// <param name="body">The body of a comment.</param>
  /// <returns><see langword="true" /> when the body starts with the line of the marker.</returns>
  private static bool HasMarker(string body) =>
    body.StartsWith(CommentComposer.Marker, StringComparison.Ordinal)
    && (body.Length == CommentComposer.Marker.Length || body[CommentComposer.Marker.Length] is '\n' or '\r');

  /// <summary>Gives the facts <c>comment.require_changes</c> is evaluated on.</summary>
  /// <param name="config">The configuration of the commit.</param>
  /// <param name="context">The comparison of the head with its base.</param>
  /// <returns>The facts, not rounded.</returns>
  /// <remarks>
  ///   The threshold of <c>coverage_drop</c> is the one of the default project status
  ///   (<c>coverage.status.project.default.threshold</c>), as ARCHITECTURE §8.4 retains it. Codecov reads
  ///   <c>coverage.status.project.threshold</c>, a key a valid file does not have: its threshold is zero in
  ///   practice.
  /// </remarks>
  private static RequireChangesFacts ToFacts(CoverageConfig config, ComparisonContext context)
  {
    var comparison = context.Comparison.Result;
    var threshold = config.BuildStatusRules([])
                          .Rules
                          .FirstOrDefault(static rule => rule is { Type: StatusType.Project, Name: "default" })
                          ?.Threshold ?? 0;

    return new RequireChangesFacts(
      comparison.IndirectChanges.Count > 0,
      comparison.Patch.Totals.Lines,
      comparison.Patch.Coverage,
      comparison.Head.Coverage,
      comparison.Base?.Coverage,
      threshold
    );
  }

  /// <summary>Writes a body on the forge: on the comment already known, on the one found by its marker, or as a new one.</summary>
  /// <param name="address">The address of the repository on its forge.</param>
  /// <param name="pull">The pull request, which records the comment that holds the body.</param>
  /// <param name="commit">The head that is commented.</param>
  /// <param name="config">The configuration of the commit.</param>
  /// <param name="context">The comparison of the head with its base.</param>
  /// <param name="body">The body of the comment.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>What became of the comment.</returns>
  private async Task<CommentOutcome> WriteAsync(
    ForgeRepositoryRef address,
    PullRequest pull,
    Commit commit,
    CoverageConfig config,
    ComparisonContext context,
    string body,
    CancellationToken cancellationToken)
  {
    var gateway = gateways.GetGateway(address.Forge);
    var hash = SHA256.HashData(Encoding.UTF8.GetBytes(body));

    // Whether a comment of this application existed and was deleted on the forge.
    var deleted = false;

    if (pull.CommentId is { } known)
    {
      if (pull.HasCommentBody(hash))
      {
        return CommentOutcome.Unchanged;
      }

      if (await gateway.UpdateIssueCommentAsync(address, known, body, cancellationToken).ConfigureAwait(false))
      {
        pull.RecordComment(known, hash, commit.Sha, clock.UtcNow);

        return CommentOutcome.Updated;
      }

      // Deleted on the forge. A run may have written it again and failed to save its identifier: the search below
      // finds that comment instead of writing a second one.
      deleted = true;
    }

    // Before anything is written, the comment this application may have left is looked for.
    var comments = await gateway.ListIssueCommentsAsync(address, pull.Number, cancellationToken).ConfigureAwait(false);
    var existing = comments.Where(static comment => comment.WrittenByThisApplication && HasMarker(comment.Body))
                           .OrderBy(static comment => comment.Id)
                           .FirstOrDefault();

    if (existing is not null)
    {
      if (string.Equals(existing.Body, body, StringComparison.Ordinal))
      {
        pull.RecordComment(existing.Id, hash, commit.Sha, clock.UtcNow);

        return CommentOutcome.Unchanged;
      }

      if (await gateway.UpdateIssueCommentAsync(address, existing.Id, body, cancellationToken).ConfigureAwait(false))
      {
        pull.RecordComment(existing.Id, hash, commit.Sha, clock.UtcNow);

        return CommentOutcome.Updated;
      }

      // Deleted between the listing and the update.
      deleted = true;

      if (config.Comment.Behavior is CommentBehavior.Once)
      {
        // Kept, so that the next notification does not take the pull request for one never commented.
        pull.RecordComment(existing.Id, SHA256.HashData(Encoding.UTF8.GetBytes(existing.Body)), commit.Sha, clock.UtcNow);
      }
    }
    else if (!deleted && !config.Comment.RequireChanges.Evaluate(ToFacts(config, context)))
    {
      // The first publication only: once a comment exists it is always kept up to date.
      return CommentOutcome.Skipped;
    }

    // Written again, a deleted comment is not a first publication: require_changes is not asked again.
    if (deleted && config.Comment.Behavior is CommentBehavior.Once)
    {
      return CommentOutcome.Gone;
    }

    return await CreateAsync(gateway, address, pull, commit, body, hash, cancellationToken).ConfigureAwait(false);
  }

  /// <summary>Writes a new comment and records it on the pull request.</summary>
  /// <param name="gateway">The gateway of the forge.</param>
  /// <param name="address">The address of the repository on its forge.</param>
  /// <param name="pull">The pull request.</param>
  /// <param name="commit">The head that is commented.</param>
  /// <param name="body">The body of the comment.</param>
  /// <param name="hash">The hash of the body.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>
  ///   <see cref="CommentOutcome.Created" />, or <see cref="CommentOutcome.Skipped" /> when the forge takes no comment
  ///   on this pull request: the statuses are published already, and nothing fails for a locked pull request.
  /// </returns>
  private async Task<CommentOutcome> CreateAsync(
    IForgeGateway gateway,
    ForgeRepositoryRef address,
    PullRequest pull,
    Commit commit,
    string body,
    byte[] hash,
    CancellationToken cancellationToken)
  {
    if (await gateway.CreateIssueCommentAsync(address, pull.Number, body, cancellationToken).ConfigureAwait(false) is not { } created)
    {
      return CommentOutcome.Skipped;
    }

    pull.RecordComment(created, hash, commit.Sha, clock.UtcNow);

    return CommentOutcome.Created;
  }

  [LoggerMessage(EventId = 1401, Level = LogLevel.Information, Message = "The comment of pull request {Number} for commit {CommitId}: {Outcome}")]
  private static partial void LogOutcome(ILogger logger, int number, long commitId, CommentOutcome outcome);

  #endregion
}
