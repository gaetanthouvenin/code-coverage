// ------------------------------------------------------------------------------------------------
// <copyright file="Commit.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Guards;

namespace CodeCoverage.Domain.Commits;

/// <summary>A commit of a tracked repository and the coverage known for it.</summary>
public sealed class Commit
{
  #region Fields

  /// <summary>The maximum length of <see cref="Branch" />.</summary>
  public const int BranchMaxLength = Repositories.Branch.NameMaxLength;

  /// <summary>The maximum length of <see cref="ConfigSource" />.</summary>
  public const int ConfigSourceMaxLength = 64;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="Commit" /> class.</summary>
  /// <param name="repositoryId">The repository.</param>
  /// <param name="sha">The SHA of the commit.</param>
  /// <param name="createdAt">When the row was created, in UTC.</param>
  /// <param name="updatedAt">When the row last changed, in UTC.</param>
  private Commit(long repositoryId, GitSha sha, DateTimeOffset createdAt, DateTimeOffset updatedAt)
  {
    RepositoryId = repositoryId;
    Sha = sha;
    CreatedAt = createdAt;
    UpdatedAt = updatedAt;
  }

  #endregion

  #region Properties

  /// <summary>Gets the identifier of the row, assigned when it is saved.</summary>
  public long Id { get; private set; }

  /// <summary>Gets the identifier of the repository.</summary>
  public long RepositoryId { get; private set; }

  /// <summary>Gets the SHA of the commit.</summary>
  public GitSha Sha { get; private set; }

  /// <summary>Gets the branch of the commit, or <see langword="null" /> when none was reported.</summary>
  public string? Branch { get; private set; }

  /// <summary>Gets the number of its pull request, or <see langword="null" /> when none was reported.</summary>
  public int? PullNumber { get; private set; }

  /// <summary>Gets where the commit stands in the processing of its coverage.</summary>
  public CommitState State { get; private set; } = CommitState.Known;

  /// <summary>Gets the last session number handed out; only the repository increments it.</summary>
  public int UploadSequence { get; private set; }

  /// <summary>Gets the number of files of its report.</summary>
  public int Files { get; private set; }

  /// <summary>Gets the number of coverable lines.</summary>
  public int Lines { get; private set; }

  /// <summary>Gets the number of covered lines.</summary>
  public int Hits { get; private set; }

  /// <summary>Gets the number of lines that are not covered.</summary>
  public int Misses { get; private set; }

  /// <summary>Gets the number of partially covered lines.</summary>
  public int Partials { get; private set; }

  /// <summary>Gets the number of branch lines.</summary>
  public int Branches { get; private set; }

  /// <summary>Gets the number of method lines.</summary>
  public int Methods { get; private set; }

  /// <summary>Gets the coverage in percent, or <see langword="null" /> before the finalization or without a coverable line.</summary>
  public decimal? Coverage { get; private set; }

  /// <summary>Gets the number of sessions of its report, the carried-forward ones included.</summary>
  public int SessionCount { get; private set; }

  /// <summary>Gets the number of sessions that were uploaded for this commit.</summary>
  public int UploadedSessionCount { get; private set; }

  /// <summary>Gets the revision of its report, incremented by each finalization.</summary>
  public int ReportRevision { get; private set; }

  /// <summary>Gets what the finalization found for the carried-forward flags.</summary>
  public CarryforwardStatus CarryforwardStatus { get; private set; } = CarryforwardStatus.None;

  /// <summary>Gets where its configuration came from, or an empty text before its finalization.</summary>
  public string ConfigSource { get; private set; } = string.Empty;

  /// <summary>Gets when the last upload was received, or <see langword="null" />.</summary>
  public DateTimeOffset? LastUploadAt { get; private set; }

  /// <summary>Gets when it was last finalized, or <see langword="null" />.</summary>
  public DateTimeOffset? CompletedAt { get; private set; }

  /// <summary>Gets when it was last notified, or <see langword="null" />.</summary>
  public DateTimeOffset? NotifiedAt { get; private set; }

  /// <summary>Gets when the row was created, in UTC.</summary>
  public DateTimeOffset CreatedAt { get; private set; }

  /// <summary>Gets when the row last changed, in UTC.</summary>
  public DateTimeOffset UpdatedAt { get; private set; }

  /// <summary>Gets the version of the row, incremented by the changes that must detect concurrent writes.</summary>
  public int Version { get; private set; }

  #endregion

  #region Methods

  /// <summary>Records a commit that has no upload yet.</summary>
  /// <param name="repositoryId">The identifier of the repository.</param>
  /// <param name="sha">The SHA of the commit.</param>
  /// <param name="at">The current time; converted to UTC.</param>
  /// <returns>The commit, not saved yet.</returns>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="repositoryId" /> is not positive.</exception>
  /// <exception cref="ArgumentNullException"><paramref name="sha" /> is <see langword="null" />.</exception>
  public static Commit Create(long repositoryId, GitSha sha, DateTimeOffset at)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(repositoryId);
    ArgumentNullException.ThrowIfNull(sha);

    var utc = at.ToUniversalTime();

    return new Commit(repositoryId, sha, utc, utc);
  }

  /// <summary>Records that an upload was received for this commit.</summary>
  /// <param name="branch">The branch of the upload, or <see langword="null" />; kept only when none is known.</param>
  /// <param name="pullNumber">The pull request of the upload, or <see langword="null" />; kept only when none is known.</param>
  /// <param name="at">The current time; converted to UTC.</param>
  /// <remarks>
  ///   <para>Only a <see cref="CommitState.Known" /> commit becomes <see cref="CommitState.Pending" />.</para>
  ///   <para>
  ///     <see cref="Version" /> does not change, on purpose: the parallel uploads of a matrix all come through
  ///     here and must not refuse one another, whereas a save still detects a finalization that changed it.
  ///   </para>
  /// </remarks>
  /// <exception cref="ArgumentException"><paramref name="branch" /> is empty, white space or too long.</exception>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="pullNumber" /> is not positive.</exception>
  public void MarkPending(string? branch, int? pullNumber, DateTimeOffset at)
  {
    if (branch is not null)
    {
      ArgumentException.ThrowIfNullOrWhiteSpace(branch);

      ArgumentException.ThrowIf(
        branch.Length > BranchMaxLength,
        $"The value cannot be longer than {BranchMaxLength} characters.",
        nameof(branch));
    }

    if (pullNumber is { } number)
    {
      ArgumentOutOfRangeException.ThrowIfNegativeOrZero(number, nameof(pullNumber));
    }

    if (State is CommitState.Known)
    {
      State = CommitState.Pending;
    }

    Branch ??= branch;
    PullNumber ??= pullNumber;

    var utc = at.ToUniversalTime();
    LastUploadAt = utc;
    UpdatedAt = utc;
  }

  #endregion
}
