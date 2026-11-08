// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationContext.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Guards;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Infrastructure.Identity;

/// <summary>The organization of the current DI scope, set once.</summary>
/// <remarks>
///   The class and <see cref="Enter" /> are <see langword="internal" />: the compiler keeps the
///   Application and the hosts from entering an organization by its identifier.
/// </remarks>
internal sealed class OrganizationContext : ICurrentOrganization
{
  #region Fields

  /// <summary>The identifier of the organization, or <see langword="null" /> before the entry.</summary>
  private long? _id;

  #endregion

  #region Methods

  /// <summary>Enters an organization; entering the same one again has no effect.</summary>
  /// <param name="organizationId">The identifier of the organization.</param>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="organizationId" /> is not positive.</exception>
  /// <exception cref="InvalidOperationException">The scope has already entered another organization.</exception>
  internal void Enter(long organizationId)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(organizationId);

    InvalidOperationException.ThrowIf(
      _id is not null && _id != organizationId,
      "The scope has already entered another organization."
    );

    _id = organizationId;
  }

  #endregion

  #region Implementation of ICurrentOrganization

  /// <inheritdoc />
  public long Id => _id ?? throw new InvalidOperationException("The scope has not entered an organization.");

  /// <inheritdoc />
  public bool IsKnown => _id is not null;

  #endregion
}
