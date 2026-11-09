// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubGatewayMapper.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Text.Json;

using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Git;
using CodeCoverage.Infrastructure.GitHub.Models;

namespace CodeCoverage.Infrastructure.GitHub;

/// <summary>Converts the payloads of the GitHub API into the contracts of the forge port.</summary>
/// <remarks>A value the port cannot carry raises a <see cref="JsonException" />: the payload is unreadable.</remarks>
internal static class GitHubGatewayMapper
{
  #region Fields

  /// <summary>The number of changed files above which GitHub cuts a comparison.</summary>
  private const int CompareFileLimit = 300;

  #endregion

  #region Methods

  /// <summary>Converts an installation and the repository it serves.</summary>
  /// <param name="installation">The installation.</param>
  /// <param name="repository">The repository.</param>
  /// <returns>The connection.</returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="installation" /> or <paramref name="repository" /> is <see langword="null" />.
  /// </exception>
  public static ForgeConnectionInfo ToForgeConnection(
    this GitHubInstallationResponse installation,
    GitHubRepositoryResponse repository)
  {
    ArgumentNullException.ThrowIfNull(installation);
    ArgumentNullException.ThrowIfNull(repository);

    return new ForgeConnectionInfo(ToText(installation.Id), ToText(installation.Account.Id), ToText(repository.Id));
  }

  /// <summary>Converts a repository.</summary>
  /// <param name="repository">The repository.</param>
  /// <returns>The repository of the port; GitHub has no level between the account and the repository.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="repository" /> is <see langword="null" />.</exception>
  public static ForgeRepositoryInfo ToForgeRepository(this GitHubRepositoryResponse repository)
  {
    ArgumentNullException.ThrowIfNull(repository);

    return new ForgeRepositoryInfo(
      ToText(repository.Id),
      repository.Owner.Login,
      string.Empty,
      repository.Name,
      repository.DefaultBranch,
      repository.Private
    );
  }

  /// <summary>Converts a commit.</summary>
  /// <param name="commit">The commit.</param>
  /// <returns>The commit of the port.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="commit" /> is <see langword="null" />.</exception>
  /// <exception cref="JsonException">A SHA is malformed, or the commit has no date.</exception>
  public static ForgeCommitInfo ToForgeCommit(this GitHubCommitResponse commit)
  {
    ArgumentNullException.ThrowIfNull(commit);

    return new ForgeCommitInfo(
      ToSha(commit.Sha),
      [.. commit.Parents.Select(static parent => ToSha(parent.Sha))],
      commit.Commit.Message,
      commit.Author?.Login,
      commit.Commit.Committer?.Date
      ?? commit.Commit.Author?.Date
      ?? throw new JsonException($"The commit '{commit.Sha}' has no date.")
    );
  }

  /// <summary>Converts a comparison.</summary>
  /// <param name="compare">The comparison.</param>
  /// <returns>The comparison of the port.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="compare" /> is <see langword="null" />.</exception>
  /// <exception cref="JsonException">A SHA is malformed, or the status of a file is unknown.</exception>
  /// <remarks>
  ///   GitHub has no flag for a cut comparison: a list that reaches the limit, or that is missing, is reported as
  ///   truncated, so a comparison of exactly 300 files is treated as incomplete rather than wrongly as complete.
  /// </remarks>
  public static ForgeCompareResult ToForgeCompare(this GitHubCompareResponse compare)
  {
    ArgumentNullException.ThrowIfNull(compare);

    return new ForgeCompareResult(
      ToSha(compare.MergeBaseCommit.Sha),
      compare.AheadBy,
      compare.BehindBy,
      compare.Files is null ? [] : [.. compare.Files.Select(ToForgeChangedFile)],
      compare.Files is not { Count: < CompareFileLimit }
    );
  }

  /// <summary>Converts a pull request.</summary>
  /// <param name="pull">The pull request.</param>
  /// <returns>The pull request of the port, whose head is the head of the branch, never the merge commit.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="pull" /> is <see langword="null" />.</exception>
  /// <exception cref="JsonException">A SHA is malformed, or the state is unknown.</exception>
  public static ForgePullInfo ToForgePull(this GitHubPullResponse pull)
  {
    ArgumentNullException.ThrowIfNull(pull);

    return new ForgePullInfo(
      pull.Number,
      ToForgePullState(pull),
      pull.Title,
      pull.User?.Login,
      ToSha(pull.Head.Sha),
      pull.Head.Ref,
      pull.Base.Ref,
      ToSha(pull.Base.Sha),
      pull.MergeCommitSha is { } merge ? ToSha(merge) : null
    );
  }

  /// <summary>Converts a tree, leaving out its submodules.</summary>
  /// <param name="tree">The tree.</param>
  /// <param name="sha">The SHA that was read, which GitHub replaces by the one of the tree when given a commit.</param>
  /// <returns>The tree of the port.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="tree" /> or <paramref name="sha" /> is <see langword="null" />.</exception>
  /// <exception cref="JsonException">The SHA of an entry is malformed.</exception>
  public static ForgeTree ToForgeTree(this GitHubTreeResponse tree, GitSha sha)
  {
    ArgumentNullException.ThrowIfNull(tree);
    ArgumentNullException.ThrowIfNull(sha);

    return new ForgeTree(
      sha,
      [
        .. tree.Tree
               .Where(static entry => entry.Type is "blob" or "tree")
               .Select(static entry => new ForgeTreeEntry(entry.Path, ToSha(entry.Sha), entry.Type is "tree")),
      ],
      tree.Truncated
    );
  }

  /// <summary>Converts a changed file.</summary>
  /// <param name="file">The file.</param>
  /// <returns>The changed file of the port.</returns>
  /// <exception cref="JsonException">The status is unknown.</exception>
  public static ForgeChangedFile ToForgeChangedFile(GitHubCompareFile file)
  {
    var status = file.Status switch
    {
      "added" => ForgeChangedFileStatus.Added,
      "modified" or "changed" => ForgeChangedFileStatus.Modified,
      "removed" => ForgeChangedFileStatus.Removed,
      "renamed" => ForgeChangedFileStatus.Renamed,
      "copied" => ForgeChangedFileStatus.Copied,
      "unchanged" => ForgeChangedFileStatus.Unchanged,
      _ => throw new JsonException($"The file status '{file.Status}' is unknown."),
    };

    return new ForgeChangedFile(file.Filename, file.PreviousFilename, status, file.Patch);
  }

  /// <summary>Works out the state of a pull request: GitHub reports a merged one as closed.</summary>
  /// <param name="pull">The pull request.</param>
  /// <returns>The state of the port.</returns>
  /// <exception cref="JsonException">The state is unknown.</exception>
  private static ForgePullState ToForgePullState(GitHubPullResponse pull) =>
    pull.Merged
      ? ForgePullState.Merged
      : pull.State switch
      {
        "open" => ForgePullState.Open,
        "closed" => ForgePullState.Closed,
        _ => throw new JsonException($"The pull request state '{pull.State}' is unknown."),
      };

  /// <summary>Reads a SHA of a payload.</summary>
  /// <param name="value">The text of the SHA.</param>
  /// <returns>The SHA.</returns>
  /// <exception cref="JsonException">The text is not a full SHA.</exception>
  private static GitSha ToSha(string value) =>
    GitSha.TryParse(value, out var sha) ? sha : throw new JsonException($"'{value}' is not a full SHA.");

  /// <summary>Writes a numeric identifier of GitHub as the text the port carries.</summary>
  /// <param name="id">The identifier.</param>
  /// <returns>The text.</returns>
  private static string ToText(long id) => id.ToString(CultureInfo.InvariantCulture);

  #endregion
}
