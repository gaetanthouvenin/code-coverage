// ------------------------------------------------------------------------------------------------
// <copyright file="RotateConnectionSecretCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Application.Features.Connections.Commands.RotateConnectionSecret;

/// <summary>
///   Replaces the credential or the secret of the webhooks of a connection of an organization, without losing a
///   delivery: the previous secret of the webhooks stays accepted until the next rotation (ADR-017).
/// </summary>
/// <param name="Forge">The forge of the organization.</param>
/// <param name="Login">The login of the organization.</param>
/// <param name="ConnectionId">The identifier of the connection, which must belong to the organization.</param>
/// <param name="Kind">The secret to replace.</param>
/// <param name="Credential">
///   The new credential in clear, verified by the forge before it is protected and stored, for
///   <see cref="ConnectionSecretKind.Credential" />; <see langword="null" /> for
///   <see cref="ConnectionSecretKind.WebhookSecret" />, whose new value the instance draws.
/// </param>
public sealed record RotateConnectionSecretCommand(Forge Forge, string Login, string ConnectionId, ConnectionSecretKind Kind, string? Credential)
  : ICommand, IOrganizationScoped
{
  #region Properties

  /// <inheritdoc />
  public OrganizationRole RequiredRole => OrganizationRole.Admin;

  #endregion

  #region Methods

  /// <inheritdoc />
  /// <remarks>Never the credential: an instance can reach a log.</remarks>
  public override string ToString() =>
    $"{nameof(RotateConnectionSecretCommand)} {{ Forge = {Forge}, Login = {Login}, ConnectionId = {ConnectionId}, Kind = {Kind} }}";

  #endregion
}
