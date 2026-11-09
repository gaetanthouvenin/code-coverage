// ------------------------------------------------------------------------------------------------
// <copyright file="UploadToken.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Guards;
using CodeCoverage.Domain.Operations;

namespace CodeCoverage.Domain.Tokens;

/// <summary>A token that authorizes uploads, kept by its hash only.</summary>
public sealed class UploadToken
{
  #region Fields

  /// <summary>The maximum length of <see cref="Label" />.</summary>
  public const int LabelMaxLength = 128;

  /// <summary>The maximum length of <see cref="Hint" />.</summary>
  public const int HintMaxLength = 8;

  /// <summary>The length of <see cref="Hash" />.</summary>
  public const int HashLength = 32;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="UploadToken" /> class.</summary>
  /// <param name="scope">What the token gives access to.</param>
  /// <param name="repositoryId">The repository of a token of that scope.</param>
  /// <param name="label">The name given to the token.</param>
  /// <param name="hint">The few characters shown to recognize it.</param>
  /// <param name="hash">The hash of its value.</param>
  /// <param name="imported">Whether its value was imported.</param>
  /// <param name="createdBy">Who created it.</param>
  /// <param name="createdAt">When it was created, in UTC.</param>
  private UploadToken(
    UploadTokenScope scope,
    long? repositoryId,
    string label,
    string hint,
    byte[] hash,
    bool imported,
    string createdBy,
    DateTimeOffset createdAt)
  {
    Scope = scope;
    RepositoryId = repositoryId;
    Label = label;
    Hint = hint;
    Hash = hash;
    Imported = imported;
    CreatedBy = createdBy;
    CreatedAt = createdAt;
  }

  #endregion

  #region Properties

  /// <summary>Gets the identifier of the row, assigned when it is saved.</summary>
  public long Id { get; private set; }

  /// <summary>Gets what the token gives access to.</summary>
  public UploadTokenScope Scope { get; private set; }

  /// <summary>Gets the repository of a token of the <see cref="UploadTokenScope.Repository" /> scope, or <see langword="null" />.</summary>
  public long? RepositoryId { get; private set; }

  /// <summary>Gets the name given to the token.</summary>
  public string Label { get; private set; }

  /// <summary>Gets the few characters shown to recognize the token.</summary>
  public string Hint { get; private set; }

  /// <summary>Gets the hash of its value.</summary>
  public byte[] Hash { get; private set; }

  /// <summary>Gets a value indicating whether its value was imported, a <c>CODECOV_TOKEN</c>.</summary>
  public bool Imported { get; private set; }

  /// <summary>Gets who created the token.</summary>
  public string CreatedBy { get; private set; }

  /// <summary>Gets when the token was created, in UTC.</summary>
  public DateTimeOffset CreatedAt { get; private set; }

  /// <summary>Gets when the token expires, in UTC, or <see langword="null" /> when it does not.</summary>
  public DateTimeOffset? ExpiresAt { get; private set; }

  /// <summary>Gets when the token was last used, in UTC, or <see langword="null" />.</summary>
  public DateTimeOffset? LastUsedAt { get; private set; }

  /// <summary>Gets when the token was revoked, in UTC, or <see langword="null" />.</summary>
  public DateTimeOffset? RevokedAt { get; private set; }

  /// <summary>Gets when its rotation was advised, in UTC, or <see langword="null" />.</summary>
  public DateTimeOffset? RotationAdvisedAt { get; private set; }

  #endregion

  #region Methods

  /// <summary>Creates a token.</summary>
  /// <param name="scope">What the token gives access to.</param>
  /// <param name="repositoryId">The repository, for the <see cref="UploadTokenScope.Repository" /> scope only.</param>
  /// <param name="label">The name given to the token.</param>
  /// <param name="hint">The few characters shown to recognize it.</param>
  /// <param name="hash">The hash of its value.</param>
  /// <param name="imported">Whether its value was imported.</param>
  /// <param name="createdBy">Who creates it.</param>
  /// <param name="at">The current time; converted to UTC.</param>
  /// <param name="expiresAt">When it expires, or <see langword="null" />; converted to UTC.</param>
  /// <returns>The token, not saved yet.</returns>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="scope" /> is not a defined value.</exception>
  /// <exception cref="ArgumentNullException">A required value is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException">
  ///   A text is empty, white space or too long, the hash does not have its length, or <paramref name="repositoryId" />
  ///   does not match the scope.
  /// </exception>
  public static UploadToken Create(
    UploadTokenScope scope,
    long? repositoryId,
    string label,
    string hint,
    byte[] hash,
    bool imported,
    string createdBy,
    DateTimeOffset at,
    DateTimeOffset? expiresAt)
  {
    ArgumentOutOfRangeException.ThrowIfUndefined(scope);
    ArgumentException.ThrowIfNullOrWhiteSpace(label);
    ArgumentException.ThrowIfNullOrWhiteSpace(hint);
    ArgumentNullException.ThrowIfNull(hash);
    ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);

    ThrowIfTooLong(label, LabelMaxLength, nameof(label));
    ThrowIfTooLong(hint, HintMaxLength, nameof(hint));
    ThrowIfTooLong(createdBy, AuditEntry.ActorMaxLength, nameof(createdBy));

    ArgumentException.ThrowIf(hash.Length != HashLength, $"A hash holds {HashLength} bytes.", nameof(hash));

    ArgumentException.ThrowIf(
      (scope is UploadTokenScope.Repository) != repositoryId is > 0,
      "Only a token of a repository names one.",
      nameof(repositoryId));

    return new UploadToken(scope, repositoryId, label, hint, hash, imported, createdBy, at.ToUniversalTime())
    {
      ExpiresAt = expiresAt?.ToUniversalTime(),
    };
  }

  /// <summary>Revokes the token; a token already revoked keeps its date.</summary>
  /// <param name="at">The current time; converted to UTC.</param>
  public void Revoke(DateTimeOffset at) => RevokedAt ??= at.ToUniversalTime();

  /// <summary>Refuses a text longer than its column.</summary>
  /// <param name="value">The text.</param>
  /// <param name="maxLength">The maximum length.</param>
  /// <param name="paramName">The name of the parameter.</param>
  /// <exception cref="ArgumentException"><paramref name="value" /> is too long.</exception>
  private static void ThrowIfTooLong(string value, int maxLength, string paramName) =>
    ArgumentException.ThrowIf(value.Length > maxLength, $"The value cannot be longer than {maxLength} characters.", paramName);

  #endregion
}
