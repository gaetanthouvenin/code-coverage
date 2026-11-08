// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeRepositoryRef.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Guards;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Domain.Contracts.Forges;

/// <summary>The address of a repository on its forge, independent of its current name.</summary>
/// <remarks>
///   A reference is only valid with its connection: there is no default connection to fall back on, so a
///   reading can never reach the data of another organization by omission.
/// </remarks>
public sealed record ForgeRepositoryRef
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="ForgeRepositoryRef" /> class.</summary>
  /// <param name="forge">The forge of the repository.</param>
  /// <param name="connectionId">The connection that gives access to the repository.</param>
  /// <param name="externalId">The identifier the forge gives the repository.</param>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="connectionId" /> or <paramref name="externalId" /> is <see langword="null" />.
  /// </exception>
  /// <exception cref="ArgumentException">
  ///   <paramref name="connectionId" /> or <paramref name="externalId" /> is empty or white space.
  /// </exception>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="forge" /> is not a known forge.</exception>
  public ForgeRepositoryRef(Forge forge, string connectionId, string externalId)
  {
    ArgumentOutOfRangeException.ThrowIfUndefined(forge);
    ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
    ArgumentException.ThrowIfNullOrWhiteSpace(externalId);

    Forge = forge;
    ConnectionId = connectionId;
    ExternalId = externalId;
  }

  #endregion

  #region Properties

  /// <summary>Gets the forge of the repository.</summary>
  public Forge Forge { get; }

  /// <summary>Gets the connection that gives access to the repository: the installation identifier on GitHub.</summary>
  public string ConnectionId { get; }

  /// <summary>Gets the identifier the forge gives the repository, which survives a renaming.</summary>
  public string ExternalId { get; }

  #endregion
}
