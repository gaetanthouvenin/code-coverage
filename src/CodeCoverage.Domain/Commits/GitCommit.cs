// ------------------------------------------------------------------------------------------------
// <copyright file="GitCommit.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Guards;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Domain.Commits;

/// <summary>The metadata of a commit, as its forge reported them.</summary>
/// <remarks>
///   An immutable cache: the parents always come from the forge, never from the order of a webhook, and a row
///   is never written twice.
/// </remarks>
public sealed class GitCommit
{
  #region Fields

  /// <summary>The largest length kept of <see cref="Message" />.</summary>
  public const int MessageMaxLength = 1000;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="GitCommit" /> class.</summary>
  /// <param name="repositoryId">The repository.</param>
  /// <param name="sha">The SHA of the commit.</param>
  /// <param name="message">The message, within its limit.</param>
  /// <param name="authorLogin">The login of the author, or <see langword="null" />.</param>
  /// <param name="committedAt">When it was committed, in UTC.</param>
  /// <param name="fetchedAt">When the forge was read, in UTC.</param>
  private GitCommit(
    long repositoryId,
    GitSha sha,
    string message,
    string? authorLogin,
    DateTimeOffset committedAt,
    DateTimeOffset fetchedAt)
  {
    RepositoryId = repositoryId;
    Sha = sha;
    Message = message;
    AuthorLogin = authorLogin;
    CommittedAt = committedAt;
    FetchedAt = fetchedAt;
  }

  #endregion

  #region Properties

  /// <summary>Gets the identifier of the repository.</summary>
  public long RepositoryId { get; private set; }

  /// <summary>Gets the SHA of the commit.</summary>
  public GitSha Sha { get; private set; }

  /// <summary>Gets the SHAs of its parents, the first parent first.</summary>
  public IReadOnlyList<GitSha> ParentShas { get; private set; } = [];

  /// <summary>Gets the message of the commit, cut at <see cref="MessageMaxLength" /> characters.</summary>
  public string Message { get; private set; }

  /// <summary>Gets the login of its author, or <see langword="null" /> when the author matches no account of the forge.</summary>
  public string? AuthorLogin { get; private set; }

  /// <summary>Gets when it was committed according to its committer, in UTC.</summary>
  public DateTimeOffset CommittedAt { get; private set; }

  /// <summary>Gets when the forge was read, in UTC.</summary>
  public DateTimeOffset FetchedAt { get; private set; }

  #endregion

  #region Methods

  /// <summary>Records what a forge reported about a commit.</summary>
  /// <param name="repositoryId">The identifier of the repository.</param>
  /// <param name="commit">The commit, as the forge reported it.</param>
  /// <param name="fetchedAt">The current time; converted to UTC.</param>
  /// <returns>The metadata, not saved yet.</returns>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="repositoryId" /> is not positive.</exception>
  /// <exception cref="ArgumentNullException"><paramref name="commit" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException">The login of the author is too long.</exception>
  public static GitCommit Create(long repositoryId, ForgeCommitInfo commit, DateTimeOffset fetchedAt)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(repositoryId);
    ArgumentNullException.ThrowIfNull(commit);

    ArgumentException.ThrowIf(
      commit.AuthorLogin?.Length > Organization.LoginMaxLength,
      $"The value cannot be longer than {Organization.LoginMaxLength} characters.",
      nameof(commit));

    return new GitCommit(
      repositoryId,
      commit.Sha,
      Shorten(commit.Message),
      commit.AuthorLogin,
      commit.CommittedAt.ToUniversalTime(),
      fetchedAt.ToUniversalTime()
    )
    {
      ParentShas = [.. commit.ParentShas],
    };
  }

  /// <summary>Cuts a message at its limit, without splitting a surrogate pair that an engine would refuse.</summary>
  /// <param name="message">The message.</param>
  /// <returns>The message, within its limit.</returns>
  private static string Shorten(string message)
  {
    if (message.Length <= MessageMaxLength)
    {
      return message;
    }

    return message[..(char.IsHighSurrogate(message[MessageMaxLength - 1]) ? MessageMaxLength - 1 : MessageMaxLength)];
  }

  #endregion
}
