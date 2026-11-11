// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationMembership.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Organizations;

/// <summary>The link between a user and an organization of the instance, with the role the forge gives the user there.</summary>
/// <remarks>
///   <para>
///     The row is a copy of what the forge said when it was last asked, dated by <see cref="RefreshedAt" />. A copy
///     that is too old grants nothing: it is the reader that refuses it (<c>Access:HardExpiryMinutes</c>), so that a
///     refresh that never runs again ends in no access rather than in a stale one.
///   </para>
///   <para>The table is global: it is read to tell which organizations a user may enter, before a scope enters any.</para>
/// </remarks>
public sealed class OrganizationMembership
{
  #region Fields

  /// <summary>
  ///   How far ahead of the clock that reads it a refresh may be dated: the Api writes the date, the Web reads it,
  ///   each with its own clock.
  /// </summary>
  public static readonly TimeSpan MaxClockSkew = TimeSpan.FromSeconds(30);

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="OrganizationMembership" /> class.</summary>
  /// <param name="userId">The identifier of the user.</param>
  /// <param name="organizationId">The identifier of the organization.</param>
  /// <param name="role">The role of the user in the organization.</param>
  /// <param name="refreshedAt">When the forge was last asked, in UTC.</param>
  private OrganizationMembership(long userId, long organizationId, OrganizationRole role, DateTimeOffset refreshedAt)
  {
    UserId = userId;
    OrganizationId = organizationId;
    Role = role;
    RefreshedAt = refreshedAt;
  }

  #endregion

  #region Properties

  /// <summary>Gets the identifier of the row, assigned when it is saved.</summary>
  public long Id { get; private set; }

  /// <summary>Gets the identifier of the user.</summary>
  public long UserId { get; private set; }

  /// <summary>Gets the identifier of the organization.</summary>
  public long OrganizationId { get; private set; }

  /// <summary>Gets the role of the user in the organization.</summary>
  public OrganizationRole Role { get; private set; }

  /// <summary>Gets when the forge was last asked about this membership, in UTC.</summary>
  public DateTimeOffset RefreshedAt { get; private set; }

  #endregion

  #region Methods

  /// <summary>Records the membership of a user in an organization.</summary>
  /// <param name="userId">The identifier of the user.</param>
  /// <param name="organizationId">The identifier of the organization.</param>
  /// <param name="role">The role of the user in the organization.</param>
  /// <param name="at">When the forge was asked; converted to UTC.</param>
  /// <returns>The membership, not saved yet.</returns>
  /// <exception cref="ArgumentOutOfRangeException">An identifier is zero or negative, or the role is not defined.</exception>
  public static OrganizationMembership Create(long userId, long organizationId, OrganizationRole role, DateTimeOffset at)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(organizationId);
    ThrowIfUndefined(role);

    return new OrganizationMembership(userId, organizationId, role, at.ToUniversalTime());
  }

  /// <summary>Records what the forge says now.</summary>
  /// <param name="role">The role of the user in the organization.</param>
  /// <param name="at">When the forge was asked; converted to UTC.</param>
  /// <exception cref="ArgumentOutOfRangeException">The role is not defined.</exception>
  public void Refresh(OrganizationRole role, DateTimeOffset at)
  {
    ThrowIfUndefined(role);

    Role = role;
    RefreshedAt = at.ToUniversalTime();
  }

  /// <summary>Tells whether the membership is recent enough to grant its role.</summary>
  /// <param name="now">The current time.</param>
  /// <param name="hardExpiry">How long a membership grants its role after its last refresh.</param>
  /// <returns>
  ///   <see langword="false" /> once <paramref name="hardExpiry" /> has passed since <see cref="RefreshedAt" />, and
  ///   for a refresh dated more than <see cref="MaxClockSkew" /> in the future, which no clock that agrees with this
  ///   one wrote.
  /// </returns>
  /// <remarks>A hard expiry of zero or below grants nothing, whatever the date.</remarks>
  public bool IsFresh(DateTimeOffset now, TimeSpan hardExpiry) =>
    hardExpiry > TimeSpan.Zero && RefreshedAt <= now + MaxClockSkew && now - RefreshedAt < hardExpiry;

  /// <summary>Refuses a role that is not one of the enumeration.</summary>
  /// <param name="role">The role.</param>
  /// <exception cref="ArgumentOutOfRangeException">The role is not defined.</exception>
  private static void ThrowIfUndefined(OrganizationRole role)
  {
    if (!Enum.IsDefined(role))
    {
      throw new ArgumentOutOfRangeException(nameof(role), role, "The role is not known.");
    }
  }

  #endregion
}
