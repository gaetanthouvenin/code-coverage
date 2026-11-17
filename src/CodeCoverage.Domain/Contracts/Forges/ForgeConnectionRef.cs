// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeConnectionRef.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Guards;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Domain.Contracts.Forges;

/// <summary>The address of the connection of an organization to its forge, through which the forge is reached.</summary>
/// <remarks>
///   A connection is the installation of the App on GitHub, whose identifier is written as text, and a forge connection
///   on the other forges (ADR-017): the forge tells how to read <see cref="ConnectionId" />.
/// </remarks>
public sealed record ForgeConnectionRef
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="ForgeConnectionRef" /> class.</summary>
  /// <param name="forge">The forge of the connection.</param>
  /// <param name="connectionId">The identifier of the connection: the installation identifier in text on GitHub.</param>
  /// <exception cref="ArgumentNullException"><paramref name="connectionId" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException"><paramref name="connectionId" /> is empty or white space.</exception>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="forge" /> is not a known forge.</exception>
  public ForgeConnectionRef(Forge forge, string connectionId)
  {
    ArgumentOutOfRangeException.ThrowIfUndefined(forge);
    ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);

    Forge = forge;
    ConnectionId = connectionId;
  }

  #endregion

  #region Properties

  /// <summary>Gets the forge of the connection.</summary>
  public Forge Forge { get; }

  /// <summary>Gets the identifier of the connection: the installation identifier in text on GitHub.</summary>
  public string ConnectionId { get; }

  #endregion
}
