// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeQuota.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Domain.Operations;

/// <summary>The rate limit of a connection of a forge, or of the application itself, as its last answer reported it.</summary>
/// <remarks>
///   Operational data: a forge and a connection key, no organization, no business data. On GitHub, the connection is the
///   installation, or <see cref="AppConnection" /> for the GitHub App itself; on the other forges, it is the identifier of
///   the <see cref="ForgeConnection" />.
/// </remarks>
public sealed class ForgeQuota
{
  #region Fields

  /// <summary>The connection of the quota of the application itself.</summary>
  public const string AppConnection = "app";

  /// <summary>The longest connection key: the one of a <see cref="ForgeConnection" />.</summary>
  public const int ConnectionMaxLength = ForgeConnection.IdMaxLength;

  #endregion

  #region Ctors

  private ForgeQuota(Forge forge, string connection, long remaining, long? limit, DateTimeOffset? resetAt, DateTimeOffset observedAt)
  {
    Forge = forge;
    Connection = connection;
    Remaining = remaining;
    Limit = limit;
    ResetAt = resetAt;
    ObservedAt = observedAt;
  }

  #endregion

  #region Properties

  /// <summary>Gets the forge whose answer reported the quota.</summary>
  public Forge Forge { get; private set; }

  /// <summary>Gets the connection, or <see cref="AppConnection" /> for the application.</summary>
  public string Connection { get; private set; }

  /// <summary>Gets the requests left.</summary>
  public long Remaining { get; private set; }

  /// <summary>Gets the requests of the whole window, or <see langword="null" /> when the forge did not say.</summary>
  public long? Limit { get; private set; }

  /// <summary>Gets when the window starts again, or <see langword="null" /> when the forge did not say.</summary>
  public DateTimeOffset? ResetAt { get; private set; }

  /// <summary>Gets when the forge reported it.</summary>
  public DateTimeOffset ObservedAt { get; private set; }

  #endregion

  #region Methods

  /// <summary>Creates a quota.</summary>
  /// <param name="forge">The forge whose answer reported the quota.</param>
  /// <param name="connection">The connection, or <see cref="AppConnection" />.</param>
  /// <param name="remaining">The requests left.</param>
  /// <param name="limit">The requests of the whole window, or <see langword="null" />.</param>
  /// <param name="resetAt">When the window starts again, or <see langword="null" />; converted to UTC.</param>
  /// <param name="observedAt">When the forge reported it; converted to UTC.</param>
  /// <returns>The quota.</returns>
  /// <exception cref="ArgumentException"><paramref name="connection" /> is <see langword="null" />, empty or white space.</exception>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="connection" /> is longer than <see cref="ConnectionMaxLength" />.</exception>
  public static ForgeQuota Create(Forge forge, string connection, long remaining, long? limit, DateTimeOffset? resetAt, DateTimeOffset observedAt)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(connection);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(connection.Length, ConnectionMaxLength, nameof(connection));

    return new ForgeQuota(forge, connection, remaining, limit, resetAt?.ToUniversalTime(), observedAt.ToUniversalTime());
  }

  #endregion
}
