// ------------------------------------------------------------------------------------------------
// <copyright file="Installation.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Guards;

namespace CodeCoverage.Domain.Repositories;

/// <summary>An installation of the GitHub App on the account of an organization.</summary>
/// <remarks>
///   This row holds no business data: a webhook reads it to find its organization, before the scope has
///   entered one. The login and the kind of the account are those of the organization.
/// </remarks>
public sealed class Installation
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="Installation" /> class.</summary>
  /// <param name="id">The identifier GitHub gives the installation.</param>
  /// <param name="organizationId">The organization of the account.</param>
  /// <param name="createdAt">When the row was created, in UTC.</param>
  /// <param name="updatedAt">When the row last changed, in UTC.</param>
  private Installation(long id, long organizationId, DateTimeOffset createdAt, DateTimeOffset updatedAt)
  {
    Id = id;
    OrganizationId = organizationId;
    CreatedAt = createdAt;
    UpdatedAt = updatedAt;
  }

  #endregion

  #region Properties

  /// <summary>Gets the identifier GitHub gives the installation.</summary>
  public long Id { get; private set; }

  /// <summary>Gets the identifier of the organization of the account.</summary>
  public long OrganizationId { get; private set; }

  /// <summary>Gets when the installation was suspended, or <see langword="null" /> when it is not.</summary>
  public DateTimeOffset? SuspendedAt { get; private set; }

  /// <summary>Gets when the row was created, in UTC.</summary>
  public DateTimeOffset CreatedAt { get; private set; }

  /// <summary>Gets when the row last changed, in UTC.</summary>
  public DateTimeOffset UpdatedAt { get; private set; }

  #endregion

  #region Methods

  /// <summary>Records an installation of the GitHub App.</summary>
  /// <param name="id">The identifier GitHub gives the installation.</param>
  /// <param name="organizationId">The organization of the account.</param>
  /// <param name="at">The current time; converted to UTC.</param>
  /// <returns>The installation, not saved yet.</returns>
  /// <exception cref="ArgumentOutOfRangeException">
  ///   <paramref name="id" /> or <paramref name="organizationId" /> is not positive.
  /// </exception>
  public static Installation Create(long id, long organizationId, DateTimeOffset at)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(organizationId);

    var utc = at.ToUniversalTime();

    return new Installation(id, organizationId, utc, utc);
  }

  #endregion
}
