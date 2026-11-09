// ------------------------------------------------------------------------------------------------
// <copyright file="CommitFlagTotals.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Guards;

namespace CodeCoverage.Domain.Commits;

/// <summary>The totals of a commit for one flag, or for the whole project.</summary>
/// <remarks>The repository, the branch and the date repeat the commit, so that the history of a flag reads this row alone.</remarks>
public sealed class CommitFlagTotals
{
  #region Fields

  /// <summary>The value of <see cref="Flag" /> on the row of the whole project.</summary>
  public const string ProjectFlag = "";

  /// <summary>The maximum length of <see cref="Flag" />.</summary>
  /// <remarks>
  ///   Codecov accepts 1024 characters. The flag is in the primary key, and a clustered key of SQL Server holds 900 bytes:
  ///   eight for the commit and two per character.
  /// </remarks>
  public const int FlagMaxLength = 446;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="CommitFlagTotals" /> class.</summary>
  /// <param name="commitId">The commit.</param>
  /// <param name="flag">The flag, or <see cref="ProjectFlag" />.</param>
  /// <param name="repositoryId">The repository of the commit.</param>
  /// <param name="completedAt">When the commit was finalized, in UTC.</param>
  private CommitFlagTotals(long commitId, string flag, long repositoryId, DateTimeOffset completedAt)
  {
    CommitId = commitId;
    Flag = flag;
    RepositoryId = repositoryId;
    CompletedAt = completedAt;
  }

  #endregion

  #region Properties

  /// <summary>Gets the identifier of the commit.</summary>
  public long CommitId { get; private set; }

  /// <summary>Gets the flag, or <see cref="ProjectFlag" /> for the whole project.</summary>
  public string Flag { get; private set; }

  /// <summary>Gets the identifier of the repository of the commit.</summary>
  public long RepositoryId { get; private set; }

  /// <summary>Gets the branch of the commit, or <see langword="null" /> when none was reported.</summary>
  public string? Branch { get; private set; }

  /// <summary>Gets when the commit was finalized, in UTC.</summary>
  public DateTimeOffset CompletedAt { get; private set; }

  /// <summary>Gets the number of coverable lines.</summary>
  public int Lines { get; private set; }

  /// <summary>Gets the number of covered lines.</summary>
  public int Hits { get; private set; }

  /// <summary>Gets the number of lines that are not covered.</summary>
  public int Misses { get; private set; }

  /// <summary>Gets the number of partially covered lines.</summary>
  public int Partials { get; private set; }

  /// <summary>Gets the coverage in percent, or <see langword="null" /> without a coverable line.</summary>
  public decimal? Coverage { get; private set; }

  /// <summary>Gets a value indicating whether every session of the flag was carried forward.</summary>
  public bool IsCarriedForward { get; private set; }

  /// <summary>Gets the SHA the flag was carried from, or <see langword="null" />.</summary>
  public GitSha? CarriedForwardFromSha { get; private set; }

  #endregion

  #region Methods

  /// <summary>Creates the totals of a flag.</summary>
  /// <param name="commit">The finalized commit.</param>
  /// <param name="flag">The flag, or <see cref="ProjectFlag" /> for the whole project.</param>
  /// <param name="totals">The counters of the flag.</param>
  /// <param name="isCarriedForward">Whether every session of the flag was carried forward.</param>
  /// <param name="carriedForwardFromSha">The SHA the flag was carried from, or <see langword="null" />.</param>
  /// <returns>The totals, not saved yet.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="commit" /> or <paramref name="flag" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException">
  ///   <paramref name="flag" /> is white space or too long, <paramref name="commit" /> has no finalization date, or
  ///   <paramref name="carriedForwardFromSha" /> is set on a flag that was not carried forward.
  /// </exception>
  public static CommitFlagTotals Create(
    Commit commit,
    string flag,
    CoverageTotals totals,
    bool isCarriedForward,
    GitSha? carriedForwardFromSha)
  {
    ArgumentNullException.ThrowIfNull(commit);
    ArgumentNullException.ThrowIfNull(flag);

    ArgumentException.ThrowIf(flag.Length > 0 && string.IsNullOrWhiteSpace(flag), "A flag cannot be white space.", nameof(flag));
    ArgumentException.ThrowIf(flag.Length > FlagMaxLength, $"The value cannot be longer than {FlagMaxLength} characters.", nameof(flag));
    ArgumentException.ThrowIf(carriedForwardFromSha is not null && !isCarriedForward, "Only a carried-forward flag has an origin.", nameof(carriedForwardFromSha));

    var completedAt = commit.CompletedAt ?? throw new ArgumentException("The commit has not been finalized.", nameof(commit));

    return new CommitFlagTotals(commit.Id, flag, commit.RepositoryId, completedAt)
    {
      Branch = commit.Branch,
      Lines = totals.Lines,
      Hits = totals.Hits,
      Misses = totals.Misses,
      Partials = totals.Partials,
      Coverage = totals.Coverage,
      IsCarriedForward = isCarriedForward,
      CarriedForwardFromSha = carriedForwardFromSha,
    };
  }

  #endregion
}
