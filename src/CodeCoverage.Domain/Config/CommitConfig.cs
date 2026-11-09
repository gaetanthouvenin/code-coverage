// ------------------------------------------------------------------------------------------------
// <copyright file="CommitConfig.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Guards;

namespace CodeCoverage.Domain.Config;

/// <summary>What was found as the configuration of a commit, kept for good: a SHA does not change.</summary>
public sealed class CommitConfig
{
  #region Fields

  /// <summary>The maximum length of <see cref="Path" />.</summary>
  public const int PathMaxLength = 64;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="CommitConfig" /> class.</summary>
  /// <param name="repositoryId">The repository.</param>
  /// <param name="sha">The SHA of the commit.</param>
  /// <param name="source">Where the configuration came from.</param>
  /// <param name="fetchedAt">When it was read, in UTC.</param>
  private CommitConfig(long repositoryId, GitSha sha, ConfigSource source, DateTimeOffset fetchedAt)
  {
    RepositoryId = repositoryId;
    Sha = sha;
    Source = source;
    FetchedAt = fetchedAt;
  }

  #endregion

  #region Properties

  /// <summary>Gets the identifier of the repository.</summary>
  public long RepositoryId { get; private set; }

  /// <summary>Gets the SHA of the commit.</summary>
  public GitSha Sha { get; private set; }

  /// <summary>Gets where the configuration came from.</summary>
  public ConfigSource Source { get; private set; }

  /// <summary>Gets the path of the file of the commit, or <see langword="null" /> for another source.</summary>
  public string? Path { get; private set; }

  /// <summary>Gets the YAML of the commit or of the default branch, or <see langword="null" /> for another source.</summary>
  public string? RawYaml { get; private set; }

  /// <summary>Gets what the reading of <see cref="RawYaml" /> did not apply as written.</summary>
  public IReadOnlyList<ConfigWarning> Warnings { get; private set; } = [];

  /// <summary>Gets why a file was refused on the way to this source.</summary>
  public IReadOnlyList<string> Errors { get; private set; } = [];

  /// <summary>Gets when the configuration was read, in UTC.</summary>
  public DateTimeOffset FetchedAt { get; private set; }

  #endregion

  #region Methods

  /// <summary>Records what was found for a commit.</summary>
  /// <param name="repositoryId">The identifier of the repository.</param>
  /// <param name="sha">The SHA of the commit.</param>
  /// <param name="source">Where the configuration came from.</param>
  /// <param name="path">The path of the file of the commit, or <see langword="null" />.</param>
  /// <param name="rawYaml">The YAML, or <see langword="null" />.</param>
  /// <param name="warnings">What the reading of the YAML did not apply as written.</param>
  /// <param name="errors">Why a file was refused on the way to this source.</param>
  /// <param name="at">The current time; converted to UTC.</param>
  /// <returns>The row, not saved yet.</returns>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="repositoryId" /> is not positive, or <paramref name="source" /> is not a defined value.</exception>
  /// <exception cref="ArgumentNullException"><paramref name="sha" />, <paramref name="warnings" /> or <paramref name="errors" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException"><paramref name="path" /> is too long.</exception>
  public static CommitConfig Create(
    long repositoryId,
    GitSha sha,
    ConfigSource source,
    string? path,
    string? rawYaml,
    IReadOnlyList<ConfigWarning> warnings,
    IReadOnlyList<string> errors,
    DateTimeOffset at)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(repositoryId);
    ArgumentNullException.ThrowIfNull(sha);
    ArgumentOutOfRangeException.ThrowIfUndefined(source);
    ArgumentNullException.ThrowIfNull(warnings);
    ArgumentNullException.ThrowIfNull(errors);

    ArgumentException.ThrowIf(path?.Length > PathMaxLength, $"The value cannot be longer than {PathMaxLength} characters.", nameof(path));

    return new CommitConfig(repositoryId, sha, source, at.ToUniversalTime())
    {
      Path = path,
      RawYaml = rawYaml,
      Warnings = [.. warnings],
      Errors = [.. errors],
    };
  }

  #endregion
}
