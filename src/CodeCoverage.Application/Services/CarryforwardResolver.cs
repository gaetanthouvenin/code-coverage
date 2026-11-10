// ------------------------------------------------------------------------------------------------
// <copyright file="CarryforwardResolver.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using CodeCoverage.Application.Configuration;
using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Diffs;
using CodeCoverage.Domain.Paths;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Services;

/// <summary>Finds what a commit inherits from its ancestors (ARCHITECTURE §6.5, step 4).</summary>
/// <param name="ancestry">The first parents of a commit.</param>
/// <param name="commits">The commits of the organization.</param>
/// <param name="reports">The sessions and the blobs of the ancestors.</param>
/// <param name="diffs">The changes between two commits.</param>
/// <param name="trees">The files of a repository at a SHA.</param>
/// <param name="codec">The encoding of the coverage.</param>
/// <param name="options">The processing settings.</param>
/// <param name="clock">The clock.</param>
/// <param name="logger">The logger.</param>
internal sealed partial class CarryforwardResolver(
  CommitAncestry ancestry,
  ICommitRepository commits,
  ICommitReportRepository reports,
  DiffProvider diffs,
  RepositoryTreeProvider trees,
  ICoverageReportCodec codec,
  IOptionsMonitor<ProcessingOptions> options,
  IClock clock,
  ILogger<CarryforwardResolver> logger)
{
  #region Fields

  /// <summary>How long to wait before looking at a pending ancestor again.</summary>
  private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(30);

  #endregion

  #region Methods

  /// <summary>Finds the sessions a commit inherits.</summary>
  /// <param name="commit">The commit.</param>
  /// <param name="address">The address of its repository on its forge.</param>
  /// <param name="config">The effective configuration.</param>
  /// <param name="uploadedFlags">The flags of the sessions uploaded for the commit.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>The sessions, numbered after those of the uploads, and what was found.</returns>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  /// <exception cref="RetryLaterException">A closer ancestor is pending and the wait is not over.</exception>
  public async Task<CarryforwardResolution> ResolveAsync(
    Commit commit,
    ForgeRepositoryRef address,
    CoverageConfig config,
    IReadOnlyCollection<string> uploadedFlags,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(commit);
    ArgumentNullException.ThrowIfNull(address);
    ArgumentNullException.ThrowIfNull(config);
    ArgumentNullException.ThrowIfNull(uploadedFlags);

    var declared = config.ListDeclaredFlags()
                         .Where(flag => config.ResolveFlag(flag).Carryforward && !uploadedFlags.Contains(flag, StringComparer.Ordinal))
                         .ToList();

    if (declared.Count == 0 && !config.CarriesForwardByDefault())
    {
      return new CarryforwardResolution([], CarryforwardStatus.None, []);
    }

    var (origins, pending) = await FindOriginsAsync(commit, address, config, uploadedFlags, declared, cancellationToken).ConfigureAwait(false);
    var waited = pending.Count > 0;

    if (origins.Count == 0)
    {
      return new CarryforwardResolution([], waited ? CarryforwardStatus.AncestorPending : CarryforwardStatus.NotFound, pending);
    }

    var sessions = new List<SessionCoverage>();
    var number = commit.UploadSequence;

    foreach (var origin in origins)
    {
      await CopyAsync(commit, origin, address, config, uploadedFlags, sessions, () => ++number, cancellationToken).ConfigureAwait(false);
    }

    return new CarryforwardResolution(sessions, waited ? CarryforwardStatus.AncestorPending : CarryforwardStatus.Applied, pending);
  }

  /// <summary>Tells whether the trees are needed to follow the files of a diff.</summary>
  /// <param name="diff">The diff, or <see langword="null" />.</param>
  /// <returns><see langword="true" /> when some files cannot be followed by their patch alone.</returns>
  private static bool NeedsTrees(DiffSet? diff) =>
    diff is null or { Truncated: true } || diff.Files.Any(static file => file.Status is not FileDiffStatus.Removed and not FileDiffStatus.Unchanged && !file.HasPatch);

  /// <summary>Gives the union of the paths of some flags.</summary>
  /// <param name="config">The effective configuration.</param>
  /// <param name="flags">The flags.</param>
  /// <returns>
  ///   The union; every path as soon as one flag has none, since such a flag covers every path (Codecov,
  ///   <c>get_paths_from_flags</c>).
  /// </returns>
  private static PathPatternSet UnionOfPaths(CoverageConfig config, IEnumerable<string> flags)
  {
    var lists = flags.Select(flag => config.ResolveFlag(flag).Paths).ToList();

    return lists.Exists(static paths => paths.Count == 0) ? new PathPatternSet([]) : new PathPatternSet(lists.SelectMany(static paths => paths));
  }

  [LoggerMessage(EventId = 1300, Level = LogLevel.Information, Message = "Carryforward of commit {CommitId}: {DroppedFiles} file(s) dropped, their lines could not be followed")]
  private static partial void LogDropped(ILogger logger, long commitId, int droppedFiles);

  /// <summary>Walks the ancestors, the nearest first.</summary>
  /// <param name="commit">The commit.</param>
  /// <param name="address">The address of its repository.</param>
  /// <param name="config">The effective configuration.</param>
  /// <param name="uploadedFlags">The flags uploaded for the commit.</param>
  /// <param name="declared">The declared flags that are missing.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>The origins, and the pending ancestors passed after the wait.</returns>
  private async Task<(List<CarryforwardOrigin> Origins, List<long> Pending)> FindOriginsAsync(
    Commit commit,
    ForgeRepositoryRef address,
    CoverageConfig config,
    IReadOnlyCollection<string> uploadedFlags,
    List<string> declared,
    CancellationToken cancellationToken)
  {
    var settings = options.CurrentValue;
    var chain = await ancestry.GetFirstParentChainAsync(commit.RepositoryId, address, commit.Sha, settings.CarryforwardMaxDepth, cancellationToken).ConfigureAwait(false);
    var origins = new List<CarryforwardOrigin>();
    var found = new HashSet<string>(StringComparer.Ordinal);
    var pending = new List<long>();

    foreach (var link in chain)
    {
      var ancestor = await commits.FindByShaAsync(commit.RepositoryId, link.Sha, cancellationToken).ConfigureAwait(false);

      if (ancestor is { State: CommitState.Pending })
      {
        if (clock.UtcNow - (commit.LastUploadAt ?? commit.CreatedAt) < TimeSpan.FromMinutes(settings.CarryforwardWaitMinutes))
        {
          throw new RetryLaterException(RetryDelay, $"The ancestor {ancestor.Sha.Value} still has uploads in progress.");
        }

        pending.Add(ancestor.Id);

        continue;
      }

      if (ancestor is not { State: CommitState.Complete })
      {
        continue;
      }

      var sessions = await reports.ListSessionsAsync(ancestor.Id, cancellationToken).ConfigureAwait(false);
      string[] fresh =
      [
        .. sessions.SelectMany(static session => session.Flags)
                   .Distinct(StringComparer.Ordinal)
                   .Where(flag => !uploadedFlags.Contains(flag, StringComparer.Ordinal) && !found.Contains(flag) && config.ResolveFlag(flag).Carryforward),
      ];

      if (fresh.Length > 0)
      {
        origins.Add(new CarryforwardOrigin(ancestor, sessions, fresh));
        found.UnionWith(fresh);
      }

      if (declared.Count > 0 && !config.CarriesForwardByDefault() && declared.TrueForAll(found.Contains))
      {
        break;
      }
    }

    return (origins, pending);
  }

  /// <summary>Copies the sessions of an origin onto the commit.</summary>
  /// <param name="commit">The commit.</param>
  /// <param name="origin">The origin.</param>
  /// <param name="address">The address of its repository.</param>
  /// <param name="config">The effective configuration.</param>
  /// <param name="uploadedFlags">The flags uploaded for the commit.</param>
  /// <param name="sessions">Receives the sessions.</param>
  /// <param name="nextNumber">Gives the number of the next session.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>A task that completes when the sessions are copied.</returns>
  private async Task CopyAsync(
    Commit commit,
    CarryforwardOrigin origin,
    ForgeRepositoryRef address,
    CoverageConfig config,
    IReadOnlyCollection<string> uploadedFlags,
    List<SessionCoverage> sessions,
    Func<int> nextNumber,
    CancellationToken cancellationToken)
  {
    var plan = CarryforwardBuilder.Plan(origin.Flags, uploadedFlags);
    var paths = UnionOfPaths(config, origin.Flags);
    var diff = await diffs.GetCompareDiffAsync(commit.RepositoryId, address, origin.Ancestor.Sha, commit.Sha, cancellationToken).ConfigureAwait(false);
    ForgeTree? ancestorTree = null;
    ForgeTree? commitTree = null;

    if (NeedsTrees(diff))
    {
      ancestorTree = await trees.GetAsync(commit.RepositoryId, address, origin.Ancestor.Sha, cancellationToken).ConfigureAwait(false);
      commitTree = await trees.GetAsync(commit.RepositoryId, address, commit.Sha, cancellationToken).ConfigureAwait(false);
    }

    var dropped = 0;

    foreach (var session in origin.Sessions)
    {
      if (!plan.Selects(session.Flags) || await ReadReportAsync(session, cancellationToken).ConfigureAwait(false) is not { } report)
      {
        continue;
      }

      var uploadId = session.DataUploadId ?? session.UploadId;
      var source = new CarryforwardSource(session.Id, origin.Ancestor.Sha, session.CarriedForwardFromSha, uploadId, session.Flags, session.Name, report);
      var carried = CarryforwardBuilder.Build(plan, source, diff, ancestorTree, commitTree, paths)!;

      dropped += carried.DroppedFiles.Count;
      sessions.Add(new SessionCoverage(ToSession(commit, session, carried, nextNumber()), carried.Report));
    }

    if (dropped > 0)
    {
      LogDropped(logger, commit.Id, dropped);
    }
  }

  /// <summary>Reads the coverage of a session of an ancestor.</summary>
  /// <param name="session">The session.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>Its coverage, or <see langword="null" /> when its blob no longer exists.</returns>
  private async Task<CoverageReport?> ReadReportAsync(Session session, CancellationToken cancellationToken)
  {
    if (session.Data is { } data)
    {
      return codec.Decode(data);
    }

    if ((session.DataUploadId ?? session.UploadId) is not { } uploadId)
    {
      return null;
    }

    var blob = await reports.GetUploadCoverageAsync(uploadId, cancellationToken).ConfigureAwait(false);

    return blob is null ? null : codec.Decode(blob);
  }

  /// <summary>Builds the session of the commit.</summary>
  /// <param name="commit">The commit.</param>
  /// <param name="source">The session of the ancestor.</param>
  /// <param name="carried">What the builder gave.</param>
  /// <param name="number">The number of the session.</param>
  /// <returns>The session.</returns>
  private Session ToSession(Commit commit, Session source, CarriedSession carried, int number) =>
    Session.CarriedForward(
      commit.Id,
      number,
      carried.CarriedForwardFromSha,
      carried.SourceSessionId,
      carried.DataUploadId,
      carried.DataUploadId is null ? codec.Encode(carried.Report) : null,
      source.Flags,
      carried.Name,
      source.CiService,
      source.BuildUrl,
      carried.Report.Totals
    );

  #endregion
}
