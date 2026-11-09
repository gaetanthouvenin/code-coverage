// ------------------------------------------------------------------------------------------------
// <copyright file="Branch.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Guards;

namespace CodeCoverage.Domain.Repositories;

/// <summary>The latest known state of a branch of a tracked repository.</summary>
public sealed class Branch
{
  #region Fields

  /// <summary>The maximum length of <see cref="Name" />, the longest branch name a forge accepts.</summary>
  public const int NameMaxLength = 255;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="Branch" /> class.</summary>
  /// <param name="repositoryId">The repository.</param>
  /// <param name="name">The name of the branch.</param>
  /// <param name="headSha">The commit at the head of the branch.</param>
  /// <param name="headUpdatedAt">When the head last moved, in UTC.</param>
  private Branch(long repositoryId, string name, GitSha headSha, DateTimeOffset headUpdatedAt)
  {
    RepositoryId = repositoryId;
    Name = name;
    HeadSha = headSha;
    HeadUpdatedAt = headUpdatedAt;
  }

  #endregion

  #region Properties

  /// <summary>Gets the identifier of the repository.</summary>
  public long RepositoryId { get; private set; }

  /// <summary>Gets the name of the branch.</summary>
  public string Name { get; private set; }

  /// <summary>Gets the commit at the head of the branch.</summary>
  public GitSha HeadSha { get; private set; }

  /// <summary>Gets when the head last moved, in UTC.</summary>
  public DateTimeOffset HeadUpdatedAt { get; private set; }

  /// <summary>Gets the identifier of the closest covered commit, or <see langword="null" /> when none is known.</summary>
  public long? CoveredCommitId { get; private set; }

  /// <summary>Gets when <see cref="CoveredCommitId" /> was set, or <see langword="null" />.</summary>
  public DateTimeOffset? CoveredAt { get; private set; }

  /// <summary>Gets the version of the row, incremented by each change to detect concurrent writes.</summary>
  public int Version { get; private set; }

  #endregion

  #region Methods

  /// <summary>Records a branch of a repository.</summary>
  /// <param name="repositoryId">The identifier of the repository.</param>
  /// <param name="name">The name of the branch.</param>
  /// <param name="headSha">The commit at the head of the branch.</param>
  /// <param name="at">The current time; converted to UTC.</param>
  /// <returns>The branch, not saved yet.</returns>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="repositoryId" /> is not positive.</exception>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="name" /> or <paramref name="headSha" /> is <see langword="null" />.
  /// </exception>
  /// <exception cref="ArgumentException"><paramref name="name" /> is empty, white space or too long.</exception>
  public static Branch Create(long repositoryId, string name, GitSha headSha, DateTimeOffset at)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(repositoryId);
    ArgumentException.ThrowIfNullOrWhiteSpace(name);
    ArgumentNullException.ThrowIfNull(headSha);

    ArgumentException.ThrowIf(
      name.Length > NameMaxLength,
      $"The value cannot be longer than {NameMaxLength} characters.",
      nameof(name));

    return new Branch(repositoryId, name, headSha, at.ToUniversalTime());
  }

  /// <summary>Moves the head of the branch.</summary>
  /// <param name="headSha">The commit now at the head of the branch.</param>
  /// <param name="at">The current time; converted to UTC.</param>
  /// <exception cref="ArgumentNullException"><paramref name="headSha" /> is <see langword="null" />.</exception>
  public void MoveHead(GitSha headSha, DateTimeOffset at)
  {
    ArgumentNullException.ThrowIfNull(headSha);

    if (headSha == HeadSha)
    {
      return;
    }

    HeadSha = headSha;
    HeadUpdatedAt = at.ToUniversalTime();
    Version++;
  }

  #endregion
}
