// ------------------------------------------------------------------------------------------------
// <copyright file="CodecovImport.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Codecov;

/// <summary>
///   An import of the history of a repository from the API of Codecov, which holds the protected API token of Codecov
///   until the import ends (ARCHITECTURE §11.3).
/// </summary>
/// <remarks>
///   The token is protected by <c>ISecretProtector</c> before it reaches the import: the plain value is never kept. The
///   row is deleted when the import ends, fails for good, dies, or is older than <see cref="CredentialLifetime" />.
/// </remarks>
public sealed class CodecovImport
{
  #region Fields

  /// <summary>How long the credential of an import that never ended is kept, at most.</summary>
  public static readonly TimeSpan CredentialLifetime = TimeSpan.FromDays(7);

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="CodecovImport" /> class.</summary>
  /// <param name="repositoryId">The identifier of the repository.</param>
  /// <param name="protectedToken">The protected API token of Codecov.</param>
  /// <param name="createdAt">When the import was asked, in UTC.</param>
  private CodecovImport(long repositoryId, string protectedToken, DateTimeOffset createdAt)
  {
    RepositoryId = repositoryId;
    ProtectedToken = protectedToken;
    CreatedAt = createdAt;
    NextPage = 1;
  }

  #endregion

  #region Properties

  /// <summary>Gets the identifier of the row, assigned when it is saved.</summary>
  public long Id { get; private set; }

  /// <summary>Gets the identifier of the repository whose history is imported.</summary>
  public long RepositoryId { get; private set; }

  /// <summary>Gets the API token of Codecov, protected by <c>ISecretProtector</c>.</summary>
  public string ProtectedToken { get; private set; }

  /// <summary>Gets when the import was asked, in UTC.</summary>
  public DateTimeOffset CreatedAt { get; private set; }

  /// <summary>Gets the page of the commits of Codecov the import reads next, from 1.</summary>
  public int NextPage { get; private set; }

  /// <summary>Gets how many commits the import wrote so far.</summary>
  public int ImportedCommits { get; private set; }

  #endregion

  #region Methods

  /// <summary>Starts an import.</summary>
  /// <param name="repositoryId">The identifier of the repository.</param>
  /// <param name="protectedToken">The protected API token of Codecov, never the plain value.</param>
  /// <param name="at">The current time; converted to UTC.</param>
  /// <returns>The import, not saved yet.</returns>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="repositoryId" /> is not positive.</exception>
  /// <exception cref="ArgumentException"><paramref name="protectedToken" /> is <see langword="null" />, empty or white space.</exception>
  public static CodecovImport Start(long repositoryId, string protectedToken, DateTimeOffset at)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(repositoryId);
    ArgumentException.ThrowIfNullOrWhiteSpace(protectedToken);

    return new CodecovImport(repositoryId, protectedToken, at.ToUniversalTime());
  }

  /// <summary>Records that a page was read, so that a retry goes on from the next one.</summary>
  /// <param name="imported">How many commits of the page were written.</param>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="imported" /> is negative.</exception>
  public void RecordPage(int imported)
  {
    ArgumentOutOfRangeException.ThrowIfNegative(imported);

    NextPage++;
    ImportedCommits += imported;
  }

  /// <summary>Tells whether the credential is older than <see cref="CredentialLifetime" /> and is to be purged.</summary>
  /// <param name="now">The current time.</param>
  /// <returns><see langword="true" /> when the import is expired.</returns>
  public bool IsExpired(DateTimeOffset now) => now - CreatedAt > CredentialLifetime;

  #endregion
}
