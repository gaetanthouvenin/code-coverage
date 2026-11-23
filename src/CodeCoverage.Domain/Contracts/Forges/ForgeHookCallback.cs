// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeHookCallback.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Guards;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Domain.Contracts.Forges;

/// <summary>Where the hooks of a forge connection deliver their events, and the secret that authenticates them.</summary>
/// <remarks>
///   The address is the route of the connection, <c>/api/v1/{forge}/webhooks/{connectionId}</c> under the public address of
///   the Api (ADR-017). The secret is the current webhook secret of the connection, in clear for the time of the call: it
///   is never written by <see cref="ToString" />.
/// </remarks>
public sealed class ForgeHookCallback
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="ForgeHookCallback" /> class.</summary>
  /// <param name="address">The absolute address the hooks deliver to.</param>
  /// <param name="secret">The webhook secret of the connection, in clear.</param>
  private ForgeHookCallback(Uri address, string secret)
  {
    Address = address;
    Secret = secret;
  }

  #endregion

  #region Properties

  /// <summary>Gets the absolute address the hooks deliver to.</summary>
  public Uri Address { get; }

  /// <summary>Gets the webhook secret of the connection, in clear; never logged.</summary>
  public string Secret { get; }

  #endregion

  #region Methods

  /// <summary>Builds the callback of a forge connection.</summary>
  /// <param name="apiPublicUrl">The absolute public address of the Api.</param>
  /// <param name="forge">The forge of the connection.</param>
  /// <param name="connectionId">The identifier of the connection.</param>
  /// <param name="secret">The current webhook secret of the connection, in clear.</param>
  /// <returns>The callback, on the route <c>/api/v1/{forge}/webhooks/{connectionId}</c> of the connection.</returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="apiPublicUrl" />, <paramref name="connectionId" /> or <paramref name="secret" /> is
  ///   <see langword="null" />.
  /// </exception>
  /// <exception cref="ArgumentException">
  ///   <paramref name="apiPublicUrl" /> is not absolute, <paramref name="connectionId" /> is not well formed, or
  ///   <paramref name="secret" /> is empty or white space.
  /// </exception>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="forge" /> is not a known forge.</exception>
  public static ForgeHookCallback For(Uri apiPublicUrl, Forge forge, string connectionId, string secret)
  {
    ArgumentNullException.ThrowIfNull(apiPublicUrl);
    ArgumentException.ThrowIf(!apiPublicUrl.IsAbsoluteUri, "The public address of the Api must be absolute.", nameof(apiPublicUrl));
    ArgumentNullException.ThrowIfNull(connectionId);
    ArgumentException.ThrowIf(!ForgeConnection.IsWellFormedId(connectionId), $"The connection identifier '{connectionId}' is not well formed.", nameof(connectionId));
    ArgumentException.ThrowIfNullOrWhiteSpace(secret);

    var address = new Uri($"{apiPublicUrl.AbsoluteUri.TrimEnd('/')}/api/v1/{ForgeSegments.ToSegment(forge)}/webhooks/{connectionId}", UriKind.Absolute);

    return new ForgeHookCallback(address, secret);
  }

  /// <inheritdoc />
  public override string ToString() => $"{nameof(ForgeHookCallback)} {{ Address = {Address} }}";

  #endregion
}
