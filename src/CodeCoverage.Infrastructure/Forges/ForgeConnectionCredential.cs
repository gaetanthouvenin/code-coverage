// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeConnectionCredential.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Infrastructure.Forges;

/// <summary>A forge connection of the organization of the scope, with its credential decrypted.</summary>
/// <param name="connectionId">The identifier of the connection.</param>
/// <param name="forge">The forge of the connection.</param>
/// <param name="kind">The kind of credential.</param>
/// <param name="baseAddress">The address of the API of the forge for the account of the connection.</param>
/// <param name="tenantId">The directory tenant that issues the credential, or <see langword="null" />.</param>
/// <param name="credential">The decrypted credential.</param>
/// <remarks>
///   A class rather than a record: its text never shows the credential. The caller uses it at once and never keeps it,
///   nor writes it to a log.
/// </remarks>
internal sealed class ForgeConnectionCredential(
  string connectionId,
  Forge forge,
  ForgeConnectionKind kind,
  Uri baseAddress,
  Guid? tenantId,
  string credential)
{
  #region Properties

  /// <summary>Gets the identifier of the connection.</summary>
  public string ConnectionId { get; } = connectionId;

  /// <summary>Gets the forge of the connection.</summary>
  public Forge Forge { get; } = forge;

  /// <summary>Gets the kind of credential.</summary>
  public ForgeConnectionKind Kind { get; } = kind;

  /// <summary>Gets the address of the API of the forge for the account of the connection.</summary>
  public Uri BaseAddress { get; } = baseAddress;

  /// <summary>Gets the directory tenant that issues the credential, or <see langword="null" /> when the forge has none.</summary>
  public Guid? TenantId { get; } = tenantId;

  /// <summary>Gets the decrypted credential, which is never persisted nor logged.</summary>
  public string Credential { get; } = credential;

  #endregion
}
