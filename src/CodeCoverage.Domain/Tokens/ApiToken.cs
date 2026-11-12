// ------------------------------------------------------------------------------------------------
// <copyright file="ApiToken.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Guards;

namespace CodeCoverage.Domain.Tokens;

/// <summary>
///   A personal token of the management API (<c>cvp_…</c>), owned by a user and bound at its creation to one organization
///   or to the instance. Only the hash of its value is kept.
/// </summary>
/// <remarks>
///   The token grants nothing beyond the rights of its user: every access check runs for the user, with the GitHub token
///   stored for it since its sign-in on the Web. A token of an organization enters that organization only; a token of
///   the instance enters none.
/// </remarks>
public sealed class ApiToken
{
  #region Fields

  /// <summary>The maximum length of <see cref="Name" />.</summary>
  public const int NameMaxLength = 128;

  /// <summary>The maximum length of <see cref="Hint" />.</summary>
  public const int HintMaxLength = 12;

  /// <summary>The length of <see cref="Hash" />, a SHA-256.</summary>
  public const int HashLength = 32;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="ApiToken" /> class.</summary>
  /// <param name="userId">The identifier of the user who owns the token.</param>
  /// <param name="organizationId">The organization of the token, or <see langword="null" /> for a token of the instance.</param>
  /// <param name="name">The name given to the token.</param>
  /// <param name="hint">The beginning of the value, to recognize it.</param>
  /// <param name="hash">The hash of the value.</param>
  /// <param name="createdAt">When it was created, in UTC.</param>
  private ApiToken(long userId, long? organizationId, string name, string hint, byte[] hash, DateTimeOffset createdAt)
  {
    UserId = userId;
    OrganizationId = organizationId;
    Name = name;
    Hint = hint;
    Hash = hash;
    CreatedAt = createdAt;
  }

  #endregion

  #region Properties

  /// <summary>Gets the identifier of the row, assigned when it is saved.</summary>
  public long Id { get; private set; }

  /// <summary>Gets the identifier of the user who owns the token.</summary>
  public long UserId { get; private set; }

  /// <summary>Gets the organization of the token, or <see langword="null" /> for a token of the instance.</summary>
  public long? OrganizationId { get; private set; }

  /// <summary>Gets the name given to the token.</summary>
  public string Name { get; private set; }

  /// <summary>Gets the beginning of the value (the prefix and a few characters), to recognize it.</summary>
  public string Hint { get; private set; }

  /// <summary>Gets the SHA-256 of the value.</summary>
  public byte[] Hash { get; private set; }

  /// <summary>Gets when the token was created, in UTC.</summary>
  public DateTimeOffset CreatedAt { get; private set; }

  /// <summary>Gets when the token expires, in UTC, or <see langword="null" /> when it does not.</summary>
  public DateTimeOffset? ExpiresAt { get; private set; }

  /// <summary>Gets when the token was last used, in UTC, at most once a minute, or <see langword="null" />.</summary>
  public DateTimeOffset? LastUsedAt { get; private set; }

  /// <summary>Gets when the token was revoked, in UTC, or <see langword="null" />.</summary>
  public DateTimeOffset? RevokedAt { get; private set; }

  /// <summary>Gets a value indicating whether the token belongs to the instance rather than to an organization.</summary>
  public bool IsInstanceToken => OrganizationId is null;

  #endregion

  #region Methods

  /// <summary>Creates a token.</summary>
  /// <param name="userId">The identifier of the user who owns the token.</param>
  /// <param name="organizationId">The organization of the token, or <see langword="null" /> for a token of the instance.</param>
  /// <param name="name">The name given to the token.</param>
  /// <param name="hint">The beginning of the value, to recognize it.</param>
  /// <param name="hash">The hash of the value.</param>
  /// <param name="at">The current time; converted to UTC.</param>
  /// <param name="expiresAt">When it expires, or <see langword="null" />.</param>
  /// <returns>The token, not saved yet.</returns>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="userId" /> or <paramref name="organizationId" /> is not positive.</exception>
  /// <exception cref="ArgumentNullException"><paramref name="name" />, <paramref name="hint" /> or <paramref name="hash" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException">
  ///   <paramref name="name" /> or <paramref name="hint" /> is empty, white space or too long, <paramref name="hash" /> is not
  ///   32 bytes long, or <paramref name="expiresAt" /> is not after <paramref name="at" />.
  /// </exception>
  public static ApiToken Create(long userId, long? organizationId, string name, string hint, byte[] hash, DateTimeOffset at, DateTimeOffset? expiresAt)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);

    if (organizationId is { } organization)
    {
      ArgumentOutOfRangeException.ThrowIfNegativeOrZero(organization, nameof(organizationId));
    }

    ArgumentException.ThrowIfNullOrWhiteSpace(name);
    ArgumentException.ThrowIfNullOrWhiteSpace(hint);
    ArgumentNullException.ThrowIfNull(hash);
    ArgumentException.ThrowIf(name.Length > NameMaxLength, $"The value cannot be longer than {NameMaxLength} characters.", nameof(name));
    ArgumentException.ThrowIf(hint.Length > HintMaxLength, $"The value cannot be longer than {HintMaxLength} characters.", nameof(hint));
    ArgumentException.ThrowIf(hash.Length != HashLength, $"A hash holds {HashLength} bytes.", nameof(hash));
    ArgumentException.ThrowIf(expiresAt <= at, "A token expires after its creation.", nameof(expiresAt));

    return new ApiToken(userId, organizationId, name, hint, hash, at.ToUniversalTime()) { ExpiresAt = expiresAt?.ToUniversalTime() };
  }

  /// <summary>Tells whether the token may still authenticate a request.</summary>
  /// <param name="at">The current time.</param>
  /// <returns><see langword="true" /> when the token is neither revoked nor expired.</returns>
  public bool IsUsableAt(DateTimeOffset at) => RevokedAt is null && (ExpiresAt is null || ExpiresAt > at);

  /// <summary>Revokes the token; a revoked token stays revoked at its first date.</summary>
  /// <param name="at">The current time; converted to UTC.</param>
  public void Revoke(DateTimeOffset at) => RevokedAt ??= at.ToUniversalTime();

  #endregion
}
