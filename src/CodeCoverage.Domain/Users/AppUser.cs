// ------------------------------------------------------------------------------------------------
// <copyright file="AppUser.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Guards;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Domain.Users;

/// <summary>A person who signed in through a forge.</summary>
/// <remarks>
///   The row is global and holds no business data. Its tokens stay on the server and only ever hold protected values:
///   <see cref="ReplaceTokens" /> is the only way to set them.
/// </remarks>
public sealed class AppUser
{
  #region Fields

  /// <summary>The maximum length of <see cref="ExternalId" />.</summary>
  public const int ExternalIdMaxLength = Organization.ExternalAccountIdMaxLength;

  /// <summary>The maximum length of <see cref="Login" />.</summary>
  public const int LoginMaxLength = Organization.LoginMaxLength;

  /// <summary>The maximum length of <see cref="AvatarUrl" />.</summary>
  public const int AvatarUrlMaxLength = 512;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="AppUser" /> class.</summary>
  /// <param name="forge">The forge of the account.</param>
  /// <param name="externalId">The identifier the forge gives the account, in lower case.</param>
  /// <param name="login">The login of the account.</param>
  private AppUser(Forge forge, string externalId, string login)
  {
    Forge = forge;
    ExternalId = externalId;
    Login = login;
  }

  #endregion

  #region Properties

  /// <summary>Gets the identifier of the row, assigned when it is saved.</summary>
  public long Id { get; private set; }

  /// <summary>Gets the forge of the account.</summary>
  public Forge Forge { get; private set; }

  /// <summary>Gets the identifier the forge gives the account, in lower case, which never changes.</summary>
  public string ExternalId { get; private set; }

  /// <summary>Gets the login of the account.</summary>
  public string Login { get; private set; }

  /// <summary>Gets the address of its avatar, or <see langword="null" />.</summary>
  public string? AvatarUrl { get; private set; }

  /// <summary>Gets the protected access token, or <see langword="null" />.</summary>
  public string? AccessTokenProtected { get; private set; }

  /// <summary>Gets the protected refresh token, or <see langword="null" />.</summary>
  public string? RefreshTokenProtected { get; private set; }

  /// <summary>Gets when the access token expires, in UTC, or <see langword="null" />.</summary>
  public DateTimeOffset? AccessTokenExpiresAt { get; private set; }

  /// <summary>Gets when the refresh token expires, in UTC, or <see langword="null" />.</summary>
  public DateTimeOffset? RefreshTokenExpiresAt { get; private set; }

  /// <summary>Gets a value indicating whether the user is an administrator of the instance.</summary>
  public bool IsAdmin { get; private set; }

  /// <summary>Gets when the user last signed in, in UTC, or <see langword="null" />.</summary>
  public DateTimeOffset? LastLoginAt { get; private set; }

  /// <summary>Gets the version of the row, incremented by each change to detect concurrent writes.</summary>
  public int Version { get; private set; }

  #endregion

  #region Methods

  /// <summary>Creates a user from the account a forge reports.</summary>
  /// <param name="forge">The forge of the account.</param>
  /// <param name="externalId">The identifier the forge gives the account.</param>
  /// <param name="login">The login of the account.</param>
  /// <param name="avatarUrl">The address of its avatar, or <see langword="null" />.</param>
  /// <returns>The user, not saved yet.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="externalId" /> or <paramref name="login" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException"><paramref name="externalId" /> or <paramref name="login" /> is empty, white space or too long.</exception>
  public static AppUser Create(Forge forge, string externalId, string login, string? avatarUrl)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(externalId);
    ArgumentException.ThrowIf(externalId.Length > ExternalIdMaxLength, $"The value cannot be longer than {ExternalIdMaxLength} characters.", nameof(externalId));
    ThrowIfInvalidLogin(login);

    return new AppUser(forge, Organization.NormalizeExternalAccountId(externalId), login) { AvatarUrl = ShortenAvatar(avatarUrl) };
  }

  /// <summary>Records a sign-in and what the forge reports of the account now.</summary>
  /// <param name="login">The login of the account.</param>
  /// <param name="avatarUrl">The address of its avatar, or <see langword="null" />.</param>
  /// <param name="at">The current time; converted to UTC.</param>
  /// <exception cref="ArgumentNullException"><paramref name="login" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException"><paramref name="login" /> is empty, white space or too long.</exception>
  public void RecordSignIn(string login, string? avatarUrl, DateTimeOffset at)
  {
    ThrowIfInvalidLogin(login);

    Login = login;
    AvatarUrl = ShortenAvatar(avatarUrl);
    LastLoginAt = at.ToUniversalTime();
    Version++;
  }

  /// <summary>Replaces the tokens of the user, keeping only their protected values.</summary>
  /// <param name="protector">The protector of the stored secrets.</param>
  /// <param name="accessToken">The access token, in clear.</param>
  /// <param name="accessTokenExpiresAt">When it expires, or <see langword="null" /> when it does not.</param>
  /// <param name="refreshToken">The refresh token, in clear, or <see langword="null" /> when the forge gives none.</param>
  /// <param name="refreshTokenExpiresAt">When it expires, or <see langword="null" />.</param>
  /// <exception cref="ArgumentNullException"><paramref name="protector" /> or <paramref name="accessToken" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException"><paramref name="accessToken" /> is empty or white space.</exception>
  public void ReplaceTokens(
    ISecretProtector protector,
    string accessToken,
    DateTimeOffset? accessTokenExpiresAt,
    string? refreshToken,
    DateTimeOffset? refreshTokenExpiresAt)
  {
    ArgumentNullException.ThrowIfNull(protector);
    ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

    AccessTokenProtected = protector.Protect(accessToken);
    AccessTokenExpiresAt = accessTokenExpiresAt?.ToUniversalTime();
    RefreshTokenProtected = refreshToken is null ? null : protector.Protect(refreshToken);
    RefreshTokenExpiresAt = refreshToken is null ? null : refreshTokenExpiresAt?.ToUniversalTime();
    Version++;
  }

  /// <summary>Records whether the user is an administrator of the instance.</summary>
  /// <param name="isAdmin">Whether the user is one.</param>
  public void SetInstanceAdmin(bool isAdmin)
  {
    if (isAdmin != IsAdmin)
    {
      IsAdmin = isAdmin;
      Version++;
    }
  }

  /// <summary>Drops an address that cannot fit its column: only a display, it never refuses a user.</summary>
  /// <param name="avatarUrl">The address, or <see langword="null" />.</param>
  /// <returns>The address that is kept.</returns>
  private static string? ShortenAvatar(string? avatarUrl) => avatarUrl?.Length > AvatarUrlMaxLength ? null : avatarUrl;

  /// <summary>Checks a login.</summary>
  /// <param name="login">The login.</param>
  /// <exception cref="ArgumentNullException"><paramref name="login" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException"><paramref name="login" /> is empty, white space or too long.</exception>
  private static void ThrowIfInvalidLogin(string login)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(login);
    ArgumentException.ThrowIf(login.Length > LoginMaxLength, $"The value cannot be longer than {LoginMaxLength} characters.", nameof(login));
  }

  #endregion
}
